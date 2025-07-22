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

namespace VacancyImport.Tests.Services;

public class ExcelServiceTests
{
    private readonly Mock<IOptions<AppSettings>> _mockOptions;
    private readonly Mock<ILogger<ExcelService>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly AppSettings _appSettings;
    private readonly ExcelService _service;

    public ExcelServiceTests()
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
            PerformanceSettings = new PerformanceSettings
            {
                EnablePerformanceMonitoring = false,  // テスト時は無効化
                ReportingIntervalSeconds = 60
            }
        };

        _mockOptions = new Mock<IOptions<AppSettings>>();
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        
        _mockLogger = new Mock<ILogger<ExcelService>>();
        
        _mockEnvironment = new Mock<IHostEnvironment>();
        _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Development");
        
        // PerformanceMonitorの実インスタンスを作成（パフォーマンス監視は無効化済み）
        var mockPerfLogger = new Mock<ILogger<PerformanceMonitor>>();
        var performanceMonitor = new PerformanceMonitor(mockPerfLogger.Object, _mockOptions.Object);
        
        _service = new ExcelService(_mockOptions.Object, _mockLogger.Object, _mockEnvironment.Object, performanceMonitor);
    }

    [Fact]
    public async Task CheckFileUpdatesAsync_NoFiles_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "TestExcelFiles");
        Directory.CreateDirectory(tempDir);
        try
        {
            // テスト用のディレクトリを空にする
            foreach (var file in Directory.GetFiles(tempDir, "*.xlsm"))
            {
                File.Delete(file);
            }

            // Act
            var result = await _service.CheckFileUpdatesAsync(string.Empty);

            // Assert
            Assert.False(result);
        }
        finally
        {
            // クリーンアップ
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task SaveProofListAsync_ValidChanges_CreatesFile()
    {
        // Arrange
        var changes = new List<FacilityMonthlyReservation>
        {
            new FacilityMonthlyReservation
            {
                TenantId = 1,
                FacilityId = 1,
                Year = 2023,
                Month = 12,
                ReservationCounts = new string[] { "5", "3", "2" }
            }
        };

        // Act
        await _service.SaveProofListAsync(changes);

        // Assert
        // プルーフファイルが作成されたことを確認
        var proofDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proofs");
        Assert.True(Directory.Exists(proofDir));
        
        var files = Directory.GetFiles(proofDir, "proof_*.csv");
        Assert.NotEmpty(files);

        // ファイルの内容を確認
        var content = await File.ReadAllLinesAsync(files.First());
        Assert.Equal(2, content.Length); // ヘッダー + 1行のデータ
        Assert.Contains("FacilityId,Year,Month,ReservationCounts", content[0]);
        Assert.Contains("1,2023,12,5,3,2", content[1]);

        // クリーンアップ
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    // このテストはモックを使用してファイルシステムとの相互作用をテスト
    [Fact]
    public async Task ExtractMonthlyReservationsAsync_NoFiles_ReturnsEmptyList()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "TestExcelFiles");
        Directory.CreateDirectory(tempDir);
        try
        {
            // テスト用のディレクトリを空にする
            foreach (var file in Directory.GetFiles(tempDir, "*.xlsm"))
            {
                File.Delete(file);
            }

            // Act
            var result = await _service.ExtractMonthlyReservationsAsync();

            // Assert
            Assert.Empty(result);
        }
        finally
        {
            // クリーンアップ
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
} 