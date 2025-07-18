using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Hosting;
using VacancyImport.Configuration;
using VacancyImport.Services;
using VacancyImport.Utilities;

namespace VacancyImport;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // コマンドライン引数の解析
            var commandLineOptions = ParseCommandLineArgs(args);
            
            // ヘルプ表示
            if (commandLineOptions.ShowHelp)
            {
                ShowHelp();
                return;
            }
            
            // 実行モードの決定
            bool runAsConsole = commandLineOptions.RunAsConsole || Environment.UserInteractive;
            
            // 環境変数の取得
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            
            // 設定の読み込み
            var configuration = BuildConfiguration(environment);
            
            // Serilogの初期化
            InitializeSerilog(configuration);
            
            Log.Information("🚀 予約管理システム連携ツールを開始します (モード: {Mode})", runAsConsole ? "Console" : "Windows Service");
            
            if (runAsConsole)
            {
                await RunAsConsoleAsync(args, configuration);
            }
            else
            {
                await RunAsServiceAsync(configuration);
            }
        }
        catch (Exception ex)
        {
            if (Log.Logger != null && Log.Logger != Serilog.Core.Logger.None)
            {
                Log.Fatal(ex, "❌ アプリケーションが予期せず終了しました");
            }
            else
            {
                Console.WriteLine($"アプリケーションエラー: {ex}");
            }
            
            Environment.Exit(1);
        }
        finally
        {
            Log.Information("📝 ログをフラッシュしています...");
            await Log.CloseAndFlushAsync();
        }
    }

    private static CommandLineOptions ParseCommandLineArgs(string[] args)
    {
        var options = new CommandLineOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--console":
                case "-c":
                    options.RunAsConsole = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    break;
                case "--verbose":
                case "-v":
                    options.VerboseLogging = true;
                    break;
                case "--environment":
                case "-e":
                    if (i + 1 < args.Length)
                    {
                        options.Environment = args[++i];
                    }
                    break;
            }
        }
        
        return options;
    }

    private static async Task RunAsConsoleAsync(string[] args, IConfiguration configuration)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              予約管理システム連携ツール (Console Mode)              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        var builder = CreateHostBuilder(args, configuration)
            .UseConsoleLifetime(); // コンソール用のライフタイム管理

        var host = builder.Build();
        
        // Ctrl+C ハンドリング
        Console.CancelKeyPress += (sender, e) =>
        {
            Log.Information("⏹️ 停止要求を受信しました...");
            e.Cancel = true;
        };

        try
        {
            Log.Information("📋 コンソールモードで実行を開始します");
            await host.RunAsync();
        }
        catch (OperationCanceledException)
        {
            Log.Information("⏹️ ユーザーにより停止されました");
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("✅ アプリケーションが正常に終了しました");
        }
    }

    private static async Task RunAsServiceAsync(IConfiguration configuration)
    {
        var builder = CreateHostBuilder(Array.Empty<string>(), configuration)
            .UseWindowsService(options =>
            {
                options.ServiceName = "VacancyImportService";
            });

        var host = builder.Build();
        
        Log.Information("🔧 Windows Serviceモードで実行を開始します");
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                // .NET 8.0の現代的な並行サービス開始機能
                services.Configure<HostOptions>(options =>
                {
                    options.ServicesStartConcurrently = true;
                    options.ServicesStopConcurrently = true;
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
                });

                // 設定の登録
                services.Configure<AppSettings>(configuration);
                services.Configure<ServiceSettings>(configuration.GetSection("ServiceSettings"));
                services.Configure<ProofListSettings>(configuration.GetSection("ProofListSettings"));
                services.AddSingleton<IConfiguration>(configuration);

                // カスタム設定管理
                services.AddSingleton<VacancyImport.Configuration.ConfigurationManager>();
                
                // セキュリティマネージャー
                services.AddSingleton<SecurityManager>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<SecurityManager>>();
                    return new SecurityManager(logger, "keys/encryption.key");
                });

                // パフォーマンス監視
                services.AddSingleton<PerformanceMonitor>();

                // イベントログとヘルスチェック
                services.AddSingleton<EventLogService>();
                services.AddSingleton<HealthCheckService>();

                // HTTPクライアント
                services.AddHttpClient();

                // アプリケーションサービス
                services.AddSingleton<ExcelService>();
                services.AddSingleton<SupabaseService>();
                services.AddSingleton<LineWorksService>();
                services.AddSingleton<ProofListService>();

                // 現代的なサービスホストの登録（VacancyImportWorkerの代替）
                services.AddHostedService<ServiceHost>();
                
                // 従来のVacancyImportWorkerも保持（後方互換性）
                // services.AddHostedService<VacancyImportWorker>();
            })
            .ConfigureLogging((hostContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
                
                // 環境に応じたログ設定
                if (Environment.UserInteractive)
                {
                    logging.AddConsole();
                }
                else
                {
                    logging.AddEventLog(settings =>
                    {
                        settings.SourceName = "VacancyImportService";
                    });
                }
            });
    }

    private static IConfiguration BuildConfiguration(string environment)
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("VACANCY_IMPORT_")
            .Build();
    }

    private static void InitializeSerilog(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/vacancy-import-.log", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {NewLine}{Exception}")
            .WriteTo.EventLog(
                source: "VacancyImportService", 
                manageEventSource: true,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
            .CreateLogger();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("予約管理システム連携ツール");
        Console.WriteLine("Excel予約管理シートからデータを抽出し、Supabaseデータベースに同期、LINE WORKSで通知を行います");
        Console.WriteLine();
        Console.WriteLine("使用方法:");
        Console.WriteLine("  VacancyImport.exe [オプション]");
        Console.WriteLine();
        Console.WriteLine("オプション:");
        Console.WriteLine("  --console, -c           コンソールアプリケーションとして実行");
        Console.WriteLine("  --help, -h              このヘルプを表示");
        Console.WriteLine("  --verbose, -v           詳細ログを有効にする");
        Console.WriteLine("  --environment, -e <env> 環境を指定 (Development, Staging, Production)");
        Console.WriteLine();
        Console.WriteLine("Windows Serviceとして実行する場合:");
        Console.WriteLine("  # サービスのインストール");
        Console.WriteLine("  sc create VacancyImportService binPath=\"C:\\path\\to\\VacancyImport.exe\"");
        Console.WriteLine("  sc config VacancyImportService start=auto");
        Console.WriteLine("  sc description VacancyImportService \"予約管理システム連携サービス\"");
        Console.WriteLine();
        Console.WriteLine("  # サービスの管理");
        Console.WriteLine("  sc start VacancyImportService");
        Console.WriteLine("  sc stop VacancyImportService");
        Console.WriteLine("  sc delete VacancyImportService");
        Console.WriteLine();
        Console.WriteLine("例:");
        Console.WriteLine("  VacancyImport.exe --console                  # コンソールモードで実行");
        Console.WriteLine("  VacancyImport.exe --console --verbose        # 詳細ログ付きで実行");
        Console.WriteLine("  VacancyImport.exe --environment Development  # 開発環境で実行");
    }

    private class CommandLineOptions
    {
        public bool RunAsConsole { get; set; }
        public bool ShowHelp { get; set; }
        public bool VerboseLogging { get; set; }
        public string Environment { get; set; } = string.Empty;
    }
} 