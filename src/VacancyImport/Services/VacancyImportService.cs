using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VacancyImport.Services;

namespace VacancyImport.Services;

/// <summary>
/// 予約管理システム連携のWindows Service
/// </summary>
public partial class VacancyImportService : ServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VacancyImportService> _logger;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _mainTask;

    public VacancyImportService(IServiceProvider serviceProvider, ILogger<VacancyImportService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        InitializeComponent();
    }

    protected override void OnStart(string[] args)
    {
        _logger.LogInformation("予約管理システム連携サービスを開始しています...");
        
        _cancellationTokenSource = new CancellationTokenSource();
        _mainTask = RunMainLoopAsync(_cancellationTokenSource.Token);
        
        _logger.LogInformation("予約管理システム連携サービスが開始されました");
    }

    protected override void OnStop()
    {
        _logger.LogInformation("予約管理システム連携サービスを停止しています...");
        
        _cancellationTokenSource?.Cancel();
        
        try
        {
            _mainTask?.Wait(TimeSpan.FromSeconds(30));
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning(ex, "サービス停止時にタスクの待機でエラーが発生しました");
        }
        
        _logger.LogInformation("予約管理システム連携サービスが停止されました");
    }

    private async Task RunMainLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // メインのビジネスロジック実行
                var excelService = _serviceProvider.GetRequiredService<ExcelService>();
                var hasUpdates = await excelService.CheckFileUpdatesAsync();

                if (hasUpdates)
                {
                    _logger.LogInformation("ファイル更新を検出しました。データ処理を開始します");

                    // 予約データを抽出
                    var reservationData = await excelService.ExtractMonthlyReservationsAsync();

                    // Supabaseにデータを送信し、変更情報を取得
                    var supabaseService = _serviceProvider.GetRequiredService<SupabaseService>();
                    var changes = await supabaseService.UpdateMonthlyReservationsAsync(reservationData);

                    // LINE WORKSに通知
                    var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
                    await lineWorksService.SendNotificationAsync("予約データが更新されました");
                    
                    _logger.LogInformation("データ処理が完了しました");
                }

                // 5分間待機
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 正常なキャンセル
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "メインループでエラーが発生しました");
                
                // エラー時は1分待機してリトライ
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
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