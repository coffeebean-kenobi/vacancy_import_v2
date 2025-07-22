using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;
using VacancyImport.Exceptions;
using VacancyImport.Utilities;
using VacancyImport.Services;

namespace VacancyImport.Services;

/// <summary>
/// Windows ServiceとConsoleアプリケーション共通のホストロジック
/// .NET 8.0のIHostedLifecycleServiceパターンを活用した現代的な実装
/// </summary>
public class ServiceHost : BackgroundService, IHostedLifecycleService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceHost> _logger;
    private readonly ServiceSettings _serviceSettings;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly EventLogService _eventLogService;
    private readonly HealthCheckService _healthCheckService;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    
    // エラーハンドリング関連フィールド
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private int _consecutiveErrors = 0;
    private DateTime _lastErrorNotification = DateTime.MinValue;

    public ServiceHost(
        IServiceProvider serviceProvider,
        ILogger<ServiceHost> logger,
        IOptions<ServiceSettings> serviceSettings,
        EventLogService eventLogService,
        HealthCheckService healthCheckService,
        PerformanceMonitor performanceMonitor)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serviceSettings = serviceSettings.Value;
        _eventLogService = eventLogService;
        _healthCheckService = healthCheckService;
        _performanceMonitor = performanceMonitor;
    }

    #region IHostedLifecycleService Implementation

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 {ServiceName} の開始準備中...", _serviceSettings.ServiceDisplayName);
        
        // 設定検証
        await ValidateConfigurationAsync();
        
        _logger.LogInformation("📋 開始準備が完了しました");
    }

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("✅ {ServiceName} が正常に開始されました", _serviceSettings.ServiceDisplayName);
        
        // イベントログに記録
        _eventLogService.WriteServiceStart();
        
        // セキュリティ監査ログ
        using var scope = _serviceProvider.CreateScope();
        var securityManager = scope.ServiceProvider.GetRequiredService<SecurityManager>();
        securityManager.LogAuditEvent("system", "service_host_started", _serviceSettings.ServiceName);
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("⏹️ {ServiceName} の停止処理を開始します...", _serviceSettings.ServiceDisplayName);
        
        // Graceful Shutdown シグナル
        _shutdownTokenSource.Cancel();
        
        // 最大30秒待機
        var timeout = TimeSpan.FromSeconds(30);
        try
        {
            await Task.Delay(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("停止処理が完了しました");
        }
    }

    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 {ServiceName} が正常に停止しました", _serviceSettings.ServiceDisplayName);
        
        // イベントログに記録
        _eventLogService.WriteServiceStop();
        
        // セキュリティ監査ログ
        using var scope = _serviceProvider.CreateScope();
        var securityManager = scope.ServiceProvider.GetRequiredService<SecurityManager>();
        securityManager.LogAuditEvent("system", "service_host_stopped", _serviceSettings.ServiceName);
        
        // リソースクリーンアップ
        _shutdownTokenSource.Dispose();
    }

    #endregion

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 メインループを開始します (ポーリング間隔: {Interval}分)", _serviceSettings.PollingIntervalMinutes);

        var pollingInterval = TimeSpan.FromMinutes(_serviceSettings.PollingIntervalMinutes);
        var retryInterval = TimeSpan.FromMinutes(_serviceSettings.RetryIntervalMinutes);

        // 複合キャンセレーショントークン
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _shutdownTokenSource.Token);

        while (!combinedCts.Token.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // 定期ヘルスチェック
                await PerformPeriodicHealthCheckAsync();
                
                // メインビジネスロジック実行
                using var measurement = _performanceMonitor.MeasureOperation("main_loop");
                await ExecuteBusinessLogicAsync(combinedCts.Token);
                
                // 成功時の処理
                HandleExecutionSuccess();
                
                // 詳細ログ（設定で有効化された場合のみ）
                if (_serviceSettings.EnableVerboseLogging)
                {
                    _logger.LogDebug("⚡ メインループ実行時間: {Duration}ms", stopwatch.ElapsedMilliseconds);
                }
                
                // 通常の待機
                await Task.Delay(pollingInterval, combinedCts.Token);
            }
            catch (OperationCanceledException) when (combinedCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("⏹️ 停止要求を受信しました");
                break;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // エラーハンドリング
                var shouldContinue = await HandleExecutionErrorAsync(ex);
                
                if (!shouldContinue)
                {
                    throw; // サービス停止
                }
                
                // エラー時は短い間隔でリトライ
                try
                {
                    await Task.Delay(retryInterval, combinedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        _logger.LogInformation("✅ メインループを終了します");
    }

    /// <summary>
    /// 定期ヘルスチェック実行
    /// </summary>
    private async Task PerformPeriodicHealthCheckAsync()
    {
        var now = DateTime.Now;
        
        // 1時間に1回実行
        if ((now - _lastHealthCheck).TotalHours >= 1)
        {
            try
            {
                _logger.LogInformation("定期ヘルスチェックを開始します");
                
                var healthResult = await _healthCheckService.PerformHealthCheckAsync();
                
                if (!healthResult.IsHealthy)
                {
                    _eventLogService.WriteWarning($"ヘルスチェック警告: {string.Join(", ", healthResult.FailedChecks)}", 2002);
                    
                    // 重要なエラーの場合は通知
                    var criticalErrors = healthResult.FailedChecks.Where(f => 
                        f.Contains("Supabase") || 
                        f.Contains("Excel") || 
                        f.Contains("ディスク容量")).ToList();
                    
                    if (criticalErrors.Any())
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
                        await lineWorksService.SendErrorNotificationAsync(
                            $"システム警告: {string.Join(", ", criticalErrors)}", 
                            1
                        );
                    }
                }
                
                _lastHealthCheck = now;
                _logger.LogInformation("定期ヘルスチェックが完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "定期ヘルスチェック中にエラーが発生しました");
            }
        }
    }

    /// <summary>
    /// エラーハンドリングとリトライロジック
    /// </summary>
    private async Task<bool> HandleExecutionErrorAsync(Exception ex)
    {
        _consecutiveErrors++;
        
        // パフォーマンス監視にエラーを記録
        _performanceMonitor.RecordError(ex);
        
        // イベントログに記録
        _eventLogService.WriteError($"実行エラー (連続{_consecutiveErrors}回)", ex, 3020);
        
        _logger.LogError(ex, $"メインループでエラーが発生しました (連続エラー数: {_consecutiveErrors})");
        
        // エラー通知の頻度制限（10分に1回）
        var now = DateTime.Now;
        if ((now - _lastErrorNotification).TotalMinutes >= 10)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
                await lineWorksService.SendErrorNotificationAsync(ex.Message, _consecutiveErrors);
                _lastErrorNotification = now;
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "エラー通知の送信に失敗しました");
            }
        }
        
        // 最大試行回数チェック
        if (_consecutiveErrors >= _serviceSettings.MaxRetryAttempts)
        {
            _eventLogService.WriteError($"最大試行回数({_serviceSettings.MaxRetryAttempts})に達しました。サービスを停止します。", 3030);
            
            // 緊急通知
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
                await lineWorksService.SendErrorNotificationAsync(
                    $"🚨 重大エラー: 連続{_consecutiveErrors}回のエラーによりサービスを停止します", 
                    _consecutiveErrors
                );
            }
            catch
            {
                // 通知失敗は無視
            }
            
            return false; // サービス停止
        }
        
        return true; // 継続
    }

    /// <summary>
    /// 成功時の処理
    /// </summary>
    private void HandleExecutionSuccess()
    {
        if (_consecutiveErrors > 0)
        {
            _logger.LogInformation($"エラー状態から回復しました (連続エラー数: {_consecutiveErrors} → 0)");
            _eventLogService.WriteInformation($"エラー状態から回復しました (連続エラー数: {_consecutiveErrors})", 1030);
            _consecutiveErrors = 0;
        }
    }

    private async Task ValidateConfigurationAsync()
    {
        _logger.LogInformation("🔍 設定の検証を開始します");
        
        using var scope = _serviceProvider.CreateScope();
        var configManager = scope.ServiceProvider.GetRequiredService<VacancyImport.Configuration.ConfigurationManager>();
        
        try
        {
            configManager.ValidateCurrentConfiguration();
            _logger.LogInformation("✅ 設定の検証が完了しました");
        }
        catch (ConfigurationException ex)
        {
            _logger.LogCritical(ex, "❌ 設定の検証に失敗しました");
            throw;
        }
    }

    private async Task ExecuteBusinessLogicAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var excelService = scope.ServiceProvider.GetRequiredService<ExcelService>();
        var proofListService = scope.ServiceProvider.GetRequiredService<ProofListService>();
        
        // ファイル更新チェック
        var hasUpdates = await excelService.CheckFileUpdatesAsync();

        if (hasUpdates)
        {
            _logger.LogInformation("📊 ファイル更新を検出しました。データ処理を開始します");
            var processingStart = DateTime.Now;

            // 月別予約データを抽出
            var monthlyReservations = await excelService.ExtractMonthlyReservationsAsync();

            // Supabaseにデータを送信し、変更情報を取得
            var supabaseService = scope.ServiceProvider.GetRequiredService<SupabaseService>();
            var changes = await supabaseService.UpdateMonthlyReservationsAsync(monthlyReservations);

            // プルーフリストを生成（変更がある場合のみ）
            if (changes.Any())
            {
                var proofListPath = await proofListService.GenerateProofListAsync(changes);
                var summary = proofListService.GenerateSummary(changes);
                
                // LINE WORKSに通知
                var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
                await lineWorksService.SendNotificationAsync($"{summary}\n📄 プルーフリスト: {Path.GetFileName(proofListPath)}");
                
                _logger.LogInformation("📄 プルーフリストを生成しました: {Path}", proofListPath);
                
                // イベントログに記録
                var processingTime = DateTime.Now - processingStart;
                _eventLogService.WriteDataProcessingComplete(changes.Count(), processingTime);
            }
            else
            {
                _logger.LogInformation("変更がないため、プルーフリストは生成されませんでした");
            }

            _logger.LogInformation("✅ データ処理が完了しました");
        }
        else
        {
            if (_serviceSettings.EnableVerboseLogging)
            {
                _logger.LogDebug("📋 ファイル更新はありませんでした");
            }
        }
        
        // 定期的なクリーンアップ（1日1回実行）
        await PerformPeriodicCleanupAsync();
    }

    private DateTime _lastCleanupDate = DateTime.MinValue;

    private async Task PerformPeriodicCleanupAsync()
    {
        var today = DateTime.Today;
        
        // 1日1回実行
        if (_lastCleanupDate < today)
        {
            try
            {
                _logger.LogInformation("プルーフリストの定期クリーンアップを開始します");
                
                using var scope = _serviceProvider.CreateScope();
                var proofListService = scope.ServiceProvider.GetRequiredService<ProofListService>();
                await proofListService.CleanupOldProofListsAsync();
                
                _lastCleanupDate = today;
                _logger.LogInformation("プルーフリストの定期クリーンアップが完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "プルーフリストのクリーンアップ中にエラーが発生しました");
            }
        }
    }

    private async Task SendCriticalAlertAsync(int errorCount, Exception lastException)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
            
            var alertMessage = $"🚨 **重大エラー発生**\n" +
                              $"連続{errorCount}回のエラーによりサービスを停止します\n" +
                              $"最後のエラー: {lastException.Message}\n" +
                              $"時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            
            await lineWorksService.SendNotificationAsync(alertMessage);
            
            // 非同期操作を追加
            await Task.Delay(100); // 通知送信の完了を確保
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "緊急通知の送信に失敗しました");
            // 通知失敗は無視してサービス停止を継続
        }
    }

    public override void Dispose()
    {
        _shutdownTokenSource?.Dispose();
        base.Dispose();
    }
} 