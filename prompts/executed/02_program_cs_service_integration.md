# Program.cs Service統合とDI対応プロンプト

## 📖 概要
現在のProgram.csをWindows ServiceとConsoleアプリケーション両方に対応するように修正し、VacancyImportServiceクラスでのDependency Injectionを適切に実装する。

## 🎯 実装対象
- Program.csの Windows Service / Console 両対応化
- VacancyImportServiceクラスのDI統合修正
- ServiceHostクラスの作成
- コマンドライン引数による実行モード切り替え

## 📋 詳細仕様

### 1. Program.cs完全書き換え

**ファイル**: `src/VacancyImport/Program.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using VacancyImport.Configuration;
using VacancyImport.Services;
using VacancyImport.Utilities;

namespace VacancyImport
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                // 実行モードの判定
                bool isService = !(args.Contains("--console") || args.Contains("-c") || Environment.UserInteractive);
                
                if (isService)
                {
                    // Windows Serviceモード
                    RunAsService();
                }
                else
                {
                    // Consoleアプリケーションモード
                    RunAsConsole(args).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                // 最上位レベルのエラーハンドリング
                if (Log.Logger != null && Log.Logger != Serilog.Core.Logger.None)
                {
                    Log.Fatal(ex, "アプリケーションが予期せず終了しました");
                    Log.CloseAndFlush();
                }
                else
                {
                    Console.WriteLine($"アプリケーションエラー: {ex.Message}");
                }
                
                Environment.Exit(1);
            }
        }

        private static void RunAsService()
        {
            // Windows Serviceとして実行
            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();
            
            var servicesToRun = new ServiceBase[]
            {
                new VacancyImportService(serviceProvider)
            };
            
            ServiceBase.Run(servicesToRun);
        }

        private static async Task RunAsConsole(string[] args)
        {
            Console.WriteLine("=== 予約管理システム連携ツール (Console Mode) ===");
            
            // ヘルプ表示
            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }
            
            // サービス設定
            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();
            
            // Serilog初期化
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("コンソールモードで開始します");
            
            try
            {
                // ServiceHostを使用してメインロジックを実行
                var serviceHost = serviceProvider.GetRequiredService<ServiceHost>();
                
                // Ctrl+C ハンドリング
                Console.CancelKeyPress += (sender, e) =>
                {
                    logger.LogInformation("停止要求を受信しました...");
                    e.Cancel = true;
                    serviceHost.RequestStop();
                };
                
                await serviceHost.RunAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "コンソール実行中にエラーが発生しました");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IServiceCollection ConfigureServices()
        {
            // 環境変数の取得
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            // 設定ファイルの読み込み
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("VACANCY_IMPORT_")
                .Build();

            // Serilogの初期化
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.Console()
                .WriteTo.File("logs/vacancy-import-.log", rollingInterval: RollingInterval.Day)
                .WriteTo.EventLog("VacancyImportService", manageEventSource: true)
                .CreateLogger();

            var services = new ServiceCollection();

            // 設定の登録
            services.Configure<AppSettings>(configuration);
            services.AddSingleton<IConfiguration>(configuration);

            // 環境情報の登録
            services.AddSingleton<IHostEnvironment>(provider =>
            {
                return new SimpleHostEnvironment(environment, "VacancyImport", Directory.GetCurrentDirectory());
            });

            // 設定クラスの登録
            services.Configure<ServiceSettings>(configuration.GetSection("ServiceSettings"));
            services.Configure<ProofListSettings>(configuration.GetSection("ProofListSettings"));

            // GCの設定
            var performanceSettings = new AppSettings();
            configuration.GetSection("PerformanceSettings").Bind(performanceSettings.PerformanceSettings);
            if (performanceSettings.PerformanceSettings?.GCSettings != null)
            {
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = 
                    performanceSettings.PerformanceSettings.GCSettings.LargeObjectHeapCompaction
                        ? System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce
                        : System.Runtime.GCLargeObjectHeapCompactionMode.Default;
            }

            // ロギングの設定
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog();
                builder.AddConsole();
                builder.AddEventLog(settings =>
                {
                    settings.SourceName = "VacancyImportService";
                });
            });

            // HTTPクライアントの登録
            services.AddHttpClient();

            // ユーティリティサービスの登録
            services.AddSingleton<VacancyImport.Configuration.ConfigurationManager>();
            services.AddSingleton<SecurityManager>(provider => {
                var logger = provider.GetRequiredService<ILogger<SecurityManager>>();
                return new SecurityManager(logger, "keys/encryption.key");
            });
            services.AddSingleton<PerformanceMonitor>();

            // ビジネスサービスの登録
            services.AddSingleton<ExcelService>();
            services.AddSingleton<SupabaseService>();
            services.AddSingleton<LineWorksService>();
            services.AddSingleton<ProofListService>();

            // Service Host の登録
            services.AddSingleton<ServiceHost>();

            return services;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("予約管理システム連携ツール");
            Console.WriteLine("");
            Console.WriteLine("使用方法:");
            Console.WriteLine("  VacancyImport.exe [オプション]");
            Console.WriteLine("");
            Console.WriteLine("オプション:");
            Console.WriteLine("  --console, -c    コンソールアプリケーションとして実行");
            Console.WriteLine("  --help, -h       このヘルプを表示");
            Console.WriteLine("");
            Console.WriteLine("Windows Serviceとして実行する場合:");
            Console.WriteLine("  installutil.exe VacancyImport.exe");
            Console.WriteLine("  sc start VacancyImportService");
            Console.WriteLine("");
        }
    }
}
```

### 2. VacancyImportServiceクラス修正

**ファイル**: `src/VacancyImport/Services/VacancyImportService.cs`

```csharp
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VacancyImport.Services
{
    /// <summary>
    /// 予約管理システム連携のWindows Service
    /// </summary>
    public partial class VacancyImportService : ServiceBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VacancyImportService> _logger;
        private ServiceHost _serviceHost;

        public VacancyImportService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<VacancyImportService>>();
            _serviceHost = serviceProvider.GetRequiredService<ServiceHost>();
            
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _logger.LogInformation("予約管理システム連携サービスを開始しています...");
            
            try
            {
                // ServiceHostを使用してサービス開始
                Task.Run(async () =>
                {
                    try
                    {
                        await _serviceHost.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "サービス実行中にエラーが発生しました");
                        ExitCode = 1;
                        Stop();
                    }
                });
                
                _logger.LogInformation("予約管理システム連携サービスが開始されました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービス開始中にエラーが発生しました");
                ExitCode = 1;
                throw;
            }
        }

        protected override void OnStop()
        {
            _logger.LogInformation("予約管理システム連携サービスを停止しています...");
            
            try
            {
                _serviceHost?.RequestStop();
                
                // 最大30秒待機
                var timeout = TimeSpan.FromSeconds(30);
                if (!_serviceHost.WaitForStop(timeout))
                {
                    _logger.LogWarning("サービスの停止がタイムアウトしました");
                }
                
                _logger.LogInformation("予約管理システム連携サービスが停止されました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービス停止中にエラーが発生しました");
            }
        }

        private void InitializeComponent()
        {
            this.ServiceName = "VacancyImportService";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
        }
    }
}
```

### 3. ServiceHostクラス作成

**ファイル**: `src/VacancyImport/Services/ServiceHost.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Services
{
    /// <summary>
    /// ServiceとConsoleアプリケーション共通のホストロジック
    /// </summary>
    public class ServiceHost
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServiceHost> _logger;
        private readonly ServiceSettings _serviceSettings;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(false);

        public ServiceHost(IServiceProvider serviceProvider, ILogger<ServiceHost> logger, IOptions<ServiceSettings> serviceSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _serviceSettings = serviceSettings.Value;
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("サービスホストを開始します");
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                // 初期設定検証
                await ValidateConfigurationAsync();
                
                // メインループ実行
                await RunMainLoopAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("サービスホストが停止要求により終了しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービスホスト実行中にエラーが発生しました");
                throw;
            }
            finally
            {
                _stopEvent.Set();
            }
        }

        public void RequestStop()
        {
            _logger.LogInformation("サービスホストの停止を要求します");
            _cancellationTokenSource?.Cancel();
        }

        public bool WaitForStop(TimeSpan timeout)
        {
            return _stopEvent.Wait(timeout);
        }

        private async Task ValidateConfigurationAsync()
        {
            _logger.LogInformation("設定の検証を開始します");
            
            var configManager = _serviceProvider.GetRequiredService<VacancyImport.Configuration.ConfigurationManager>();
            try
            {
                configManager.ValidateCurrentConfiguration();
                _logger.LogInformation("設定の検証が完了しました");
            }
            catch (ConfigurationException ex)
            {
                _logger.LogError(ex, "設定の検証に失敗しました");
                throw;
            }
        }

        private async Task RunMainLoopAsync(CancellationToken cancellationToken)
        {
            var pollingInterval = TimeSpan.FromMinutes(_serviceSettings.PollingIntervalMinutes);
            var retryInterval = TimeSpan.FromMinutes(_serviceSettings.RetryIntervalMinutes);
            int consecutiveErrors = 0;

            _logger.LogInformation($"メインループを開始します (ポーリング間隔: {pollingInterval})");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // メインのビジネスロジック実行
                    await ExecuteBusinessLogicAsync();
                    
                    // 成功時はエラーカウンターをリセット
                    consecutiveErrors = 0;
                    
                    // 通常の待機
                    await Task.Delay(pollingInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常なキャンセル
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    _logger.LogError(ex, $"メインループでエラーが発生しました (連続エラー数: {consecutiveErrors})");
                    
                    // 最大試行回数チェック
                    if (consecutiveErrors >= _serviceSettings.MaxRetryAttempts)
                    {
                        _logger.LogCritical("最大試行回数に達しました。サービスを停止します。");
                        
                        // LINE WORKSに緊急通知
                        try
                        {
                            var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
                            await lineWorksService.SendNotificationAsync($"🚨 重大エラー: 連続{consecutiveErrors}回のエラーによりサービスを停止します");
                        }
                        catch
                        {
                            // 通知失敗は無視
                        }
                        
                        throw;
                    }
                    
                    // エラー時は短い間隔でリトライ
                    try
                    {
                        await Task.Delay(retryInterval, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ExecuteBusinessLogicAsync()
        {
            var excelService = _serviceProvider.GetRequiredService<ExcelService>();
            var proofListService = _serviceProvider.GetRequiredService<ProofListService>();
            
            // ファイル更新チェック
            var hasUpdates = await excelService.CheckFileUpdatesAsync();

            if (hasUpdates)
            {
                _logger.LogInformation("ファイル更新を検出しました。データ処理を開始します");

                // 月別予約データを抽出
                var reservationData = await excelService.ExtractMonthlyReservationsAsync();

                // Supabaseにデータを送信し、変更情報を取得
                var supabaseService = _serviceProvider.GetRequiredService<SupabaseService>();
                var changes = await supabaseService.UpdateMonthlyReservationsAsync(reservationData);

                // プルーフリストを生成
                if (changes.Any())
                {
                    var proofListPath = await proofListService.GenerateProofListAsync(changes);
                    var summary = proofListService.GenerateSummary(changes);
                    
                    // LINE WORKSに通知
                    var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
                    await lineWorksService.SendNotificationAsync($"{summary}\n📄 プルーフリスト: {Path.GetFileName(proofListPath)}");
                }

                _logger.LogInformation("データ処理が完了しました");
            }
        }
    }
}
```

### 4. 設定クラス追加

**ファイル**: `src/VacancyImport/Configuration/ServiceSettings.cs`

```csharp
namespace VacancyImport.Configuration
{
    public class ServiceSettings
    {
        public string ServiceName { get; set; } = "VacancyImportService";
        public string ServiceDisplayName { get; set; } = "予約管理システム連携サービス";
        public string ServiceDescription { get; set; } = "";
        public int PollingIntervalMinutes { get; set; } = 5;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryIntervalMinutes { get; set; } = 1;
    }
}
```

**ファイル**: `src/VacancyImport/Configuration/ProofListSettings.cs`

```csharp
namespace VacancyImport.Configuration
{
    public class ProofListSettings
    {
        public string OutputDirectory { get; set; } = "./proof";
        public int RetentionDays { get; set; } = 180;
        public bool EnableAutoCleanup { get; set; } = true;
    }
}
```

## 🔍 検証手順

1. **コンソールモードテスト**:
   ```bash
   dotnet run --configuration Debug -- --console
   ```

2. **ヘルプ表示テスト**:
   ```bash
   dotnet run -- --help
   ```

3. **サービスモードビルド**:
   ```bash
   dotnet build --configuration Release
   ```

## 📚 参考ドキュメント

- [.NET Framework Windows サービス](https://learn.microsoft.com/ja-jp/dotnet/framework/windows-services/)
- [.NET の Dependency Injection](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/dependency-injection)

## 🎯 完了条件

- [ ] Program.csがService/Console両対応になっている
- [ ] VacancyImportServiceクラスがDI統合されている
- [ ] ServiceHostクラスが作成されている
- [ ] 設定クラス（ServiceSettings、ProofListSettings）が作成されている
- [ ] コンソールモードで正常に動作する
- [ ] コマンドライン引数の処理が正しく動作する 