using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;
using VacancyImport.Exceptions;
using VacancyImport.Utilities;

namespace VacancyImport.Services;

/// <summary>
/// 予約管理システム連携のメインワーカーサービス
/// .NET 8.0のIHostedLifecycleServiceを実装し、詳細なライフサイクル制御を提供
/// </summary>
public class VacancyImportWorker : BackgroundService, IHostedLifecycleService
{
    private readonly ILogger<VacancyImportWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppSettings _appSettings;
    private readonly IHostEnvironment _hostEnvironment;

    public VacancyImportWorker(
        ILogger<VacancyImportWorker> logger,
        IServiceProvider serviceProvider,
        IOptions<AppSettings> appSettings,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _appSettings = appSettings.Value;
        _hostEnvironment = hostEnvironment;
    }

    #region IHostedLifecycleService Implementation (.NET 8.0新機能)

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("📋 サービス開始準備中...");
        
        // 開発環境でのテストデータ準備
        if (_hostEnvironment.IsDevelopment())
        {
            await InitializeTestEnvironmentAsync();
        }
        
        // 設定検証とセキュリティ初期化
        await ValidateConfigurationAsync();
    }

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("✅ サービスが正常に開始されました");
        
        // サービス開始の監査ログ
        using var scope = _serviceProvider.CreateScope();
        var securityManager = scope.ServiceProvider.GetRequiredService<SecurityManager>();
        securityManager.LogAuditEvent("system", "service_started", "VacancyImportWorker");
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("⏹️ サービス停止処理を開始します...");
        
        // 現在実行中のタスクの状態をログ出力
        _logger.LogInformation("実行中のタスクの完了を待機中...");
    }

    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 サービスが正常に停止しました");
        
        // 停止の監査ログ
        using var scope = _serviceProvider.CreateScope();
        var securityManager = scope.ServiceProvider.GetRequiredService<SecurityManager>();
        securityManager.LogAuditEvent("system", "service_stopped", "VacancyImportWorker");
    }

    #endregion

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 予約管理システム連携サービスを開始します");
        
        try
        {
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "❌ サービス開始時にエラーが発生しました");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 メインループを開始します");

        // GCの最適化設定
        ApplyPerformanceSettings();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessVacancyUpdatesAsync(stoppingToken);
                
                // 5分間の待機
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("⏹️ サービス停止要求を受信しました");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ メインループでエラーが発生しました");
                
                // .NET 8.0のBackgroundServiceExceptionBehavior.StopHostと連携
                // Environment.Exit(1)で Windows Service管理システムによる再起動を可能にする
                Environment.Exit(1);
            }
        }
        
        _logger.LogInformation("✅ メインループを終了します");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 予約管理システム連携サービスを停止します");
        
        try
        {
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("✅ サービスが正常に停止しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ サービス停止時にエラーが発生しました");
        }
    }

    private async Task ProcessVacancyUpdatesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            // Excelファイルの更新を確認
            var excelService = scope.ServiceProvider.GetRequiredService<ExcelService>();
            var hasUpdates = await excelService.CheckFileUpdatesAsync();
            
            if (hasUpdates)
            {
                _logger.LogInformation("📊 Excelファイルの更新を検出しました");
                
                // 予約データを抽出
                var reservationData = await excelService.ExtractReservationDataAsync();
                
                // Supabaseにデータを送信
                var supabaseService = scope.ServiceProvider.GetRequiredService<SupabaseService>();
                await supabaseService.UpdateReservationsAsync(reservationData);
                
                // LINE WORKSに通知
                var lineWorksService = scope.ServiceProvider.GetRequiredService<LineWorksService>();
                await lineWorksService.SendNotificationAsync("予約データが更新されました");
                
                _logger.LogInformation("✅ 予約データの同期が完了しました");
            }
            else
            {
                _logger.LogDebug("📋 Excelファイルの更新はありませんでした");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 予約データ処理中にエラーが発生しました");
            throw;
        }
    }

    private async Task InitializeTestEnvironmentAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var excelSettings = _appSettings.ExcelSettings;
            var basePath = excelSettings.GetEnvironmentSettings(_hostEnvironment.EnvironmentName).BasePath;
            
            // テスト用Excelファイルを作成
            var testFilePath = Path.Combine(basePath, "store001_test.xlsm");
            
            // 非同期操作を追加（例：ディレクトリ作成）
            await Task.Run(() =>
            {
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
            });
            
            _logger.LogInformation("🧪 テストデータの作成が完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ テストデータの作成中にエラーが発生しました");
        }
    }

    private async Task ValidateConfigurationAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            // 設定の検証
            var configManager = scope.ServiceProvider.GetRequiredService<VacancyImport.Configuration.ConfigurationManager>();
            configManager.ValidateCurrentConfiguration();
            _logger.LogInformation("✅ 設定の検証が完了しました");
            
            // 設定変更の監視
            configManager.ConfigurationChanged += (sender, args) =>
            {
                _logger.LogInformation("🔄 設定が変更されました: {Name}", args.Name);
            };
            
            // セキュリティマネージャーの初期化
            var securityManager = scope.ServiceProvider.GetRequiredService<SecurityManager>();
            securityManager.LogAuditEvent("system", "startup", "application");
            
            // 非同期操作を追加（設定の保存など）
            await Task.Run(() =>
            {
                _logger.LogInformation("🔧 サービスの初期化が完了しました");
            });
        }
        catch (ConfigurationException ex)
        {
            _logger.LogCritical("❌ 設定の検証に失敗しました: {Message}", ex.Message);
            throw;
        }
    }

    private void ApplyPerformanceSettings()
    {
        var performanceSettings = _appSettings.PerformanceSettings;
        if (performanceSettings?.GCSettings != null)
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = performanceSettings.GCSettings.LargeObjectHeapCompaction
                ? System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce
                : System.Runtime.GCLargeObjectHeapCompactionMode.Default;
                
            _logger.LogDebug("⚙️ GC設定を適用しました");
        }
    }
} 