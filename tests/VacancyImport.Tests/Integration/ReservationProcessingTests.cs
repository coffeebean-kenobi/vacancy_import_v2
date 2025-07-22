using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using VacancyImport.Configuration;
using VacancyImport.Models;
using VacancyImport.Services;
using VacancyImport.Utilities;

namespace VacancyImport.Tests.Integration;

/// <summary>
/// 予約処理の統合テスト
/// 注：このテストは完全な統合テストではなく、サービス間の連携を模擬したテストです。
/// 実際の統合テストでは、実際のExcelファイルとSupabaseを使用する必要があります。
/// </summary>
public class ReservationProcessingTests
{
    private readonly Mock<IOptions<AppSettings>> _mockOptions;
    private readonly Mock<ILogger<ExcelService>> _mockExcelLogger;
    private readonly Mock<ILogger<SupabaseService>> _mockSupabaseLogger;
    private readonly Mock<ILogger<LineWorksService>> _mockLineWorksLogger;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly Mock<HttpClient> _mockHttpClient;
    private readonly Mock<PerformanceMonitor> _mockPerformanceMonitor;
    private readonly AppSettings _appSettings;

    public ReservationProcessingTests()
    {
        // 設定のセットアップ
        _appSettings = new AppSettings
        {
            ExcelSettings = new ExcelSettings
            {
                PollingIntervalMinutes = 5,
                RetryCount = 3,
                Environments = new Dictionary<string, ExcelEnvironmentSettings>
                {
                    ["Development"] = new ExcelEnvironmentSettings
                    {
                        BasePath = Path.Combine(Path.GetTempPath(), "TestExcelFiles"),
                        SheetName = "予約表",
                        ColumnName = "日付",
                        ColumnIndex = 2
                    }
                }
            },
            SupabaseSettings = new SupabaseSettings
            {
                Url = "https://test-supabase.com",
                Key = "test-key",
                TableName = "reservations"
            },
            LineWorksSettings = new LineWorksSettings
            {
                BotId = "test-bot-id",
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                TokenUrl = "https://auth.worksmobile.com/oauth2/v2.0/token",
                MessageUrl = "https://www.worksapis.com/v1.0/bots/{0}/messages"
            },
            PerformanceSettings = new PerformanceSettings
            {
                EnablePerformanceMonitoring = true,
                ReportingIntervalSeconds = 60
            }
        };

        _mockOptions = new Mock<IOptions<AppSettings>>();
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        
        _mockExcelLogger = new Mock<ILogger<ExcelService>>();
        _mockSupabaseLogger = new Mock<ILogger<SupabaseService>>();
        _mockLineWorksLogger = new Mock<ILogger<LineWorksService>>();
        
        _mockEnvironment = new Mock<IHostEnvironment>();
        _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Development");
        
        _mockHttpClient = new Mock<HttpClient>();
        
        // PerformanceMonitorのモック設定
        _mockPerformanceMonitor = new Mock<PerformanceMonitor>(_mockExcelLogger.Object, _mockOptions);
        var mockDisposable = new Mock<IDisposable>();
        _mockPerformanceMonitor.Setup(x => x.MeasureOperation(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(mockDisposable.Object);
    }

    [Fact(Skip = "This is a test template that requires actual implementation")]
    public async Task ProcessReservationData_EndToEnd_Test()
    {
        // このテストは実際にはスキップされます。
        // 実際の統合テストでは、以下の流れでテストを行います：
        
        // 1. テスト用のExcelファイルを準備
        var testExcelDir = Path.Combine(Path.GetTempPath(), "TestExcelFiles");
        Directory.CreateDirectory(testExcelDir);
        
        try
        {
            // 2. モックサービスを作成
            // 注：実際の統合テストでは、実サービスと通信するか、または
            // より精巧なモックを使用する必要があります。
            var excelService = new ExcelService(_mockOptions.Object, _mockExcelLogger.Object, _mockEnvironment.Object, _mockPerformanceMonitor.Object);
            
            // モックのSupabaseServiceを使用
            var mockSupabaseService = new Mock<ISupabaseService>();
            mockSupabaseService
                .Setup(x => x.GetCurrentMonthlyReservationsAsync())
                .ReturnsAsync(new List<FacilityMonthlyReservation>());
            
            var mockLineWorksService = new Mock<ILineWorksService>();
            mockLineWorksService
                .Setup(x => x.SendNotificationAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // 3. サービスを使用して予約データを処理
            // Excelファイルから予約データを抽出
            var reservations = await excelService.ExtractMonthlyReservationsAsync();
            
            // 現在の予約データを取得し、変更を計算
            var currentReservations = await mockSupabaseService.Object.GetCurrentMonthlyReservationsAsync();
            var changes = CalculateChanges(currentReservations.ToList(), reservations.ToList());
            
            // 変更をSupabaseに反映
            await mockSupabaseService.Object.UpdateMonthlyReservationsAsync(changes);
            
            // 変更がある場合は通知を送信
            if (changes.Any())
            {
                var message = $"{changes.Count()}件の予約データが更新されました。";
                await mockLineWorksService.Object.SendNotificationAsync(message);
            }
            
            // プルーフリストを保存
            await excelService.SaveProofListAsync(changes);
            
            // 4. 結果を検証
            // 処理が正常に完了したことを確認
            mockSupabaseService.Verify(x => x.UpdateMonthlyReservationsAsync(It.IsAny<IEnumerable<FacilityMonthlyReservation>>()), Times.Once);
            if (changes.Any())
            {
                mockLineWorksService.Verify(x => x.SendNotificationAsync(It.IsAny<string>()), Times.Once);
            }
        }
        finally
        {
            // クリーンアップ
            if (Directory.Exists(testExcelDir))
            {
                Directory.Delete(testExcelDir, true);
            }
        }
    }
    
    // ヘルパーメソッド：変更を計算
    private static List<FacilityMonthlyReservation> CalculateChanges(
        List<FacilityMonthlyReservation> currentReservations, 
        List<FacilityMonthlyReservation> newReservations)
    {
        var changes = new List<FacilityMonthlyReservation>();
        
        // 新規追加または変更された予約
        foreach (var newReservation in newReservations)
        {
            var current = currentReservations.FirstOrDefault(r => 
                r.TenantId == newReservation.TenantId && 
                r.FacilityId == newReservation.FacilityId &&
                r.Year == newReservation.Year &&
                r.Month == newReservation.Month);
                
            if (current == null)
            {
                // 新規追加
                changes.Add(new FacilityMonthlyReservation
                {
                    TenantId = newReservation.TenantId,
                    FacilityId = newReservation.FacilityId,
                    Year = newReservation.Year,
                    Month = newReservation.Month,
                    ReservationCounts = newReservation.ReservationCounts
                });
            }
            else if (!current.ReservationCounts.SequenceEqual(newReservation.ReservationCounts))
            {
                // 変更
                changes.Add(new FacilityMonthlyReservation
                {
                    TenantId = newReservation.TenantId,
                    FacilityId = newReservation.FacilityId,
                    Year = newReservation.Year,
                    Month = newReservation.Month,
                    ReservationCounts = newReservation.ReservationCounts
                });
            }
        }
        
        // 削除された予約
        foreach (var current in currentReservations)
        {
            var stillExists = newReservations.Any(r => 
                r.TenantId == current.TenantId && 
                r.FacilityId == current.FacilityId &&
                r.Year == current.Year &&
                r.Month == current.Month);
                
            if (!stillExists)
            {
                // 削除（空の配列で表現）
                changes.Add(new FacilityMonthlyReservation
                {
                    TenantId = current.TenantId,
                    FacilityId = current.FacilityId,
                    Year = current.Year,
                    Month = current.Month,
                    ReservationCounts = Array.Empty<string>()
                });
            }
        }
        
        return changes;
    }
} 