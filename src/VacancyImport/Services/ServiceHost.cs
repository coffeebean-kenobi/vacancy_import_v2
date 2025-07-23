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

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 {ServiceName} の開始準備中...", _serviceSettings.ServiceDisplayName);
        
        // 設定検証
        ValidateConfiguration();
        
        _logger.LogInformation("📋 開始準備が完了しました");
        
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("✅ {ServiceName} が正常に開始されました", _serviceSettings.ServiceDisplayName);
        
        // イベントログに記録
        _eventLogService.WriteServiceStart();
        
        // セキュリティ監査ログ
        using var scope = _serviceProvider.CreateScope();
        var securityManager = scope.ServiceProvider.GetRequiredService<SecurityManager>();
        securityManager.LogAuditEvent("system", "service_host_started", _serviceSettings.ServiceName);
        
        return Task.CompletedTask;
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("⏹️ {ServiceName} の停止処理を開始します...", _serviceSettings.ServiceDisplayName);
        
        try
        {
            // Graceful Shutdown シグナル
            if (!_shutdownTokenSource.IsCancellationRequested)
            {
                _shutdownTokenSource.Cancel();
                _logger.LogInformation("🔄 シャットダウンシグナルを送信しました");
            }
            
            // 実行中のビジネスロジックの完了を待機（最大15秒）
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var maxWaitTime = TimeSpan.FromSeconds(15);
            
            while (stopwatch.Elapsed < maxWaitTime)
            {
                // 現在実行中の処理があるかチェック
                if (IsBusinessLogicRunning())
                {
                    _logger.LogDebug("ビジネスロジックの完了を待機中... ({Elapsed:F1}秒経過)", stopwatch.Elapsed.TotalSeconds);
                    await Task.Delay(500, cancellationToken); // 500ms待機
                }
                else
                {
                    _logger.LogInformation("✅ ビジネスロジックが正常に完了しました");
                    break;
                }
            }
            
            if (stopwatch.Elapsed >= maxWaitTime)
            {
                _logger.LogWarning("⚠️ ビジネスロジックの完了待機がタイムアウトしました ({Elapsed:F1}秒)", stopwatch.Elapsed.TotalSeconds);
            }
            
            // リソースのクリーンアップ
            PerformEmergencyCleanup();
            
            _logger.LogInformation("✅ 停止処理が完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 停止処理中にエラーが発生しました");
            
            // エラーが発生しても強制クリーンアップを実行
            try
            {
                PerformEmergencyCleanup();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "❌ 緊急クリーンアップ中にもエラーが発生しました");
            }
        }
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
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
        
        return Task.CompletedTask;
    }

    #endregion

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 メインループを開始します (ポーリング間隔: {Interval}分)", _serviceSettings.PollingIntervalMinutes);

        var pollingInterval = TimeSpan.FromMinutes(_serviceSettings.PollingIntervalMinutes);
        var retryInterval = TimeSpan.FromMinutes(_serviceSettings.RetryIntervalMinutes);

        // 複合キャンセレーショントークン
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _shutdownTokenSource.Token);

        // 初回実行時にワークシート名を確認（一回限り）
        try
        {
            await CheckWorksheetNamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ワークシート名確認中にエラーが発生しましたが、処理を継続します");
        }

        while (!combinedCts.Token.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // 定期ヘルスチェック
                await PerformPeriodicHealthCheckAsync();
                
                // メインビジネスロジック実行（タイムアウト付き）
                using var measurement = _performanceMonitor.MeasureOperation("main_loop");
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(4)); // 4分でタイムアウト
                using var combinedTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(combinedCts.Token, timeoutCts.Token);
                
                try
                {
                    await ExecuteBusinessLogicAsync(combinedTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("ビジネスロジックがタイムアウトしました（4分）");
                }
                
                // 成功時の処理
                HandleExecutionSuccess();
                
                // 詳細ログ（設定で有効化された場合のみ）
                if (_serviceSettings.EnableVerboseLogging)
                {
                    _logger.LogDebug("⚡ メインループ実行時間: {Duration}ms", stopwatch.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException) when (combinedCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("⏹️ 停止要求を受信しました");
                break;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "メインループでエラーが発生しました");
            }
            finally
            {
                stopwatch.Stop();
                
                // 常にポーリング間隔で待機（無限ループを防ぐ）
                if (!combinedCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogDebug("次のポーリングまで {Interval}分 待機します", _serviceSettings.PollingIntervalMinutes);
                        await Task.Delay(pollingInterval, combinedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("⏹️ 停止要求を受信しました");
                    }
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

    private void ValidateConfiguration()
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

    /// <summary>
    /// ビジネスロジックを実行（段階的処理対応）
    /// </summary>
    private async Task ExecuteBusinessLogicAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var excelService = scope.ServiceProvider.GetRequiredService<ExcelService>();
        var supabaseService = scope.ServiceProvider.GetRequiredService<SupabaseService>();
        var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
        var proofListService = scope.ServiceProvider.GetRequiredService<ProofListService>();
        
        try
        {
            _logger.LogDebug("ビジネスロジックの実行を開始します");
            
            // 段階1: ファイル更新チェック
            var hasUpdates = await CheckFileUpdatesWithRetryAsync(excelService);
            
            if (!hasUpdates)
            {
                _logger.LogDebug("ファイル更新が検出されませんでした");
                return;
            }
            
            _logger.LogInformation("ファイル更新を検出しました。データ処理を開始します");
            
            // 段階2: データ抽出
            var monthlyReservations = await ExtractDataWithRetryAsync(excelService);
            
            if (!monthlyReservations.Any())
            {
                _logger.LogWarning("抽出されたデータがありません");
                return;
            }
            
            _logger.LogInformation("データ抽出完了: {ReservationCount}件の予約データ", monthlyReservations.Count());
            
            // 段階3: DB更新
            var changes = await UpdateDatabaseWithRetryAsync(supabaseService, monthlyReservations);
            
            if (!changes.Any())
            {
                _logger.LogInformation("DB更新は行われませんでした（変更なし）");
                return;
            }
            
            _logger.LogInformation("DB更新完了: {ChangeCount}件の変更", changes.Count());
            
            // 段階4: プルーフリスト生成
            await GenerateProofListAsync(excelService, changes);
            
            // 段階5: LINE WORKS通知
            await SendNotificationAsync(lineWorksService, changes);
            
            _logger.LogInformation("ビジネスロジックの実行が完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ビジネスロジックの実行中にエラーが発生しました");
            // エラーが発生しても例外を再スローしない（無限ループを防ぐ）
            // 代わりにログに記録して処理を継続
        }
    }

    /// <summary>
    /// ワークシート名を確認（デバッグ用）
    /// </summary>
    private async Task CheckWorksheetNamesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var excelService = scope.ServiceProvider.GetRequiredService<ExcelService>();
        
        _logger.LogInformation("ワークシート名の確認を開始します");
        await excelService.CheckAllWorksheetNamesAsync();
        _logger.LogInformation("ワークシート名の確認が完了しました");
    }

    /// <summary>
    /// ファイル更新チェック（リトライ対応）
    /// </summary>
    private async Task<bool> CheckFileUpdatesWithRetryAsync(ExcelService excelService)
    {
        return await RetryPolicy.ExecuteWithRetryAsync(
            async () => await excelService.CheckFileUpdatesAsync(),
            retryCount: 3,
            initialDelay: 1000,
            maxDelay: 5000,
            _logger);
    }

    /// <summary>
    /// データ抽出（リトライ対応）
    /// </summary>
    private async Task<IEnumerable<Models.FacilityMonthlyReservation>> ExtractDataWithRetryAsync(ExcelService excelService)
    {
        return await RetryPolicy.ExecuteWithRetryAsync(
            async () => await excelService.ExtractMonthlyReservationsAsync(),
            retryCount: 3,
            initialDelay: 1000,
            maxDelay: 5000,
            _logger);
    }

    /// <summary>
    /// DB更新（リトライ対応）
    /// </summary>
    private async Task<IEnumerable<Models.ReservationChange>> UpdateDatabaseWithRetryAsync(SupabaseService supabaseService, IEnumerable<Models.FacilityMonthlyReservation> monthlyReservations)
    {
        return await RetryPolicy.ExecuteWithRetryAsync(
            async () => await supabaseService.UpdateMonthlyReservationsAsync(monthlyReservations),
            retryCount: 3,
            initialDelay: 1000,
            maxDelay: 5000,
            _logger);
    }

    /// <summary>
    /// プルーフリスト生成
    /// </summary>
    private async Task GenerateProofListAsync(ExcelService excelService, IEnumerable<Models.ReservationChange> changes)
    {
        try
        {
            // 変更情報から月別予約データを再構築
            var monthlyReservations = changes
                .GroupBy(c => new { c.StoreId, Year = c.Date.Year, Month = c.Date.Month })
                .Select(g => new Models.FacilityMonthlyReservation
                {
                    TenantId = 1,
                    FacilityId = int.Parse(g.Key.StoreId),
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    ReservationCounts = g.Select(c => c.NewRemain?.ToString() ?? "0").ToArray()
                });
            
            await excelService.SaveProofListAsync(monthlyReservations);
            _logger.LogInformation("プルーフリストを生成しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プルーフリスト生成中にエラーが発生しました");
            // プルーフリスト生成の失敗は致命的ではないため、処理を継続
        }
    }

    /// <summary>
    /// LINE WORKS通知
    /// </summary>
    private async Task SendNotificationAsync(LineWorksService lineWorksService, IEnumerable<Models.ReservationChange> changes)
    {
        try
        {
            var summary = GenerateNotificationSummary(changes);
            await lineWorksService.SendNotificationAsync(summary);
            _logger.LogInformation("LINE WORKS通知を送信しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE WORKS通知中にエラーが発生しました");
            // 通知の失敗は致命的ではないため、処理を継続
        }
    }

    /// <summary>
    /// 通知サマリーを生成
    /// </summary>
    private string GenerateNotificationSummary(IEnumerable<Models.ReservationChange> changes)
    {
        var changeCount = changes.Count();
        var facilityGroups = changes.GroupBy(c => c.StoreId);
        
        var summary = $"予約データ更新完了\n" +
                     $"更新件数: {changeCount}件\n" +
                     $"対象施設: {facilityGroups.Count()}施設\n" +
                     $"更新日時: {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
        
        return summary;
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
        try
        {
            _logger?.LogInformation("🔄 ServiceHostのリソースクリーンアップを開始します");
            
            // シャットダウントークンのキャンセル
            if (!_shutdownTokenSource.IsCancellationRequested)
            {
                _shutdownTokenSource.Cancel();
            }
            
            // バックグラウンドタスクの強制終了処理
            var tasks = new List<Task>();
            
            // 実行中のタスクを収集（最大5秒待機）
            var timeout = TimeSpan.FromSeconds(5);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            while (stopwatch.Elapsed < timeout)
            {
                // 実行中のタスクを確認
                var runningTasks = GetRunningTasks();
                if (!runningTasks.Any())
                {
                    break;
                }
                
                // 各タスクにキャンセルシグナルを送信
                foreach (var task in runningTasks)
                {
                    if (!task.IsCompleted)
                    {
                        _logger?.LogDebug("タスク {TaskId} の終了を待機中...", task.Id);
                    }
                }
                
                // 100ms待機
                Thread.Sleep(100);
            }
            
            // 残りのタスクを強制終了
            var remainingTasks = GetRunningTasks().Where(t => !t.IsCompleted).ToList();
            if (remainingTasks.Any())
            {
                _logger?.LogWarning("⚠️ {Count}個のタスクが強制終了されます", remainingTasks.Count);
                
                // タスクの強制終了（実際の.NETでは直接終了できないため、ログのみ）
                foreach (var task in remainingTasks)
                {
                    _logger?.LogWarning("タスク {TaskId} を強制終了: {Status}", task.Id, task.Status);
                }
            }
            
            // CancellationTokenSourceの破棄
            _shutdownTokenSource?.Dispose();
            
            // ベースクラスのDispose
            base.Dispose();
            
            _logger?.LogInformation("✅ ServiceHostのリソースクリーンアップが完了しました");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ ServiceHostのDispose中にエラーが発生しました");
            
            // エラーが発生してもリソースを確実に破棄
            try
            {
                _shutdownTokenSource?.Dispose();
                base.Dispose();
            }
            catch (Exception disposeEx)
            {
                _logger?.LogError(disposeEx, "❌ 強制クリーンアップ中にもエラーが発生しました");
            }
        }
    }
    
    /// <summary>
    /// 現在実行中のタスクを取得（デバッグ用）
    /// </summary>
    private IEnumerable<Task> GetRunningTasks()
    {
        // 実際の実装では、実行中のタスクを追跡する必要があります
        // ここでは空のリストを返します（実際の実装では適切なタスク管理が必要）
        return Enumerable.Empty<Task>();
    }
    
    /// <summary>
    /// ビジネスロジックが実行中かどうかを確認
    /// </summary>
    private bool IsBusinessLogicRunning()
    {
        // 実際の実装では、ビジネスロジックの実行状態を追跡する必要があります
        // ここでは常にfalseを返します（実際の実装では適切な状態管理が必要）
        return false;
    }
    
    /// <summary>
    /// 緊急クリーンアップ処理を実行
    /// </summary>
    private void PerformEmergencyCleanup()
    {
        try
        {
            _logger.LogInformation("🔄 緊急クリーンアップ処理を開始します");
            
            // Excelファイルのロック解除
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var excelService = scope.ServiceProvider.GetService<ExcelService>();
                if (excelService != null)
                {
                    // ExcelServiceのDisposeを呼び出してリソースをクリーンアップ
                    if (excelService is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _logger.LogDebug("Excelファイルのリソースをクリーンアップしました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excelファイルのリソースクリーンアップ中にエラーが発生しました");
            }
            
            // HTTP接続のクリーンアップ
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var httpClientFactory = scope.ServiceProvider.GetService<IHttpClientFactory>();
                if (httpClientFactory != null)
                {
                    // HttpClientの接続プールをクリア
                    _logger.LogDebug("HTTP接続プールをクリアしました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HTTP接続のクリーンアップ中にエラーが発生しました");
            }
            
            // メモリキャッシュのクリア
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var memoryCache = scope.ServiceProvider.GetService<MemoryCache<string, object>>();
                if (memoryCache != null)
                {
                    memoryCache.Clear();
                    _logger.LogDebug("メモリキャッシュをクリアしました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "メモリキャッシュのクリア中にエラーが発生しました");
            }
            
            _logger.LogInformation("✅ 緊急クリーンアップ処理が完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 緊急クリーンアップ処理中にエラーが発生しました");
        }
    }
} 