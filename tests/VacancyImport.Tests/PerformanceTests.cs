using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using VacancyImport.Configuration;
using VacancyImport.Models;
using VacancyImport.Services;
using VacancyImport.Utilities;

namespace VacancyImport.Tests
{
    /// <summary>
    /// パフォーマンステスト
    /// </summary>
    public class PerformanceTests
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly AppSettings _appSettings;

        public PerformanceTests()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<PerformanceMonitor>();
            
            _appSettings = new AppSettings
            {
                PerformanceSettings = new PerformanceSettings
                {
                    EnablePerformanceMonitoring = true,
                    ReportingIntervalSeconds = 10,
                    AlertThresholdMs = 1000
                }
            };
        }

        [Fact]
        public async Task PerformanceMonitor_MeasureOperation_ShouldCompleteWithinThreshold()
        {
            // Arrange
            var options = Options.Create(_appSettings);
            using var performanceMonitor = new PerformanceMonitor(_logger, options);
            const int thresholdMs = 100;

            // Act
            var stopwatch = Stopwatch.StartNew();
            using (performanceMonitor.MeasureOperation("test_operation"))
            {
                // シミュレートされた処理
                await Task.Delay(50);
            }
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < thresholdMs, 
                $"処理時間が閾値を超えました: {stopwatch.ElapsedMilliseconds}ms > {thresholdMs}ms");
        }

        [Fact]
        public void ProofListService_GenerateProofList_ShouldHandleLargeDataSet()
        {
            // Arrange
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ProofListService>();
            var settings = Options.Create(_appSettings);
            var proofListService = new ProofListService(logger, settings);

            // 大量データセットの作成
            var changes = GenerateLargeReservationChangeSet(1000);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var summary = proofListService.GenerateSummary(changes);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
                $"大量データ処理時間が閾値を超えました: {stopwatch.ElapsedMilliseconds}ms");
            Assert.NotNull(summary);
            Assert.Contains("1000", summary);
        }

        [Fact]
        public void MemoryUsage_ShouldRemainWithinLimits()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ProofListService>();
            var settings = Options.Create(_appSettings);
            var proofListService = new ProofListService(logger, settings);

            // Act
            for (int i = 0; i < 100; i++)
            {
                var changes = GenerateLargeReservationChangeSet(100);
                var summary = proofListService.GenerateSummary(changes);
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert - メモリ増加量が10MB以下であることを確認
            const long maxMemoryIncreaseMB = 10 * 1024 * 1024;
            Assert.True(memoryIncrease < maxMemoryIncreaseMB,
                $"メモリ使用量の増加が許容値を超えました: {memoryIncrease / 1024 / 1024}MB");
        }

        private List<ReservationChange> GenerateLargeReservationChangeSet(int count)
        {
            var changes = new List<ReservationChange>();
            var random = new Random(42); // 固定シードで再現可能

            for (int i = 0; i < count; i++)
            {
                changes.Add(new ReservationChange
                {
                    ChangeType = i % 3 == 0 ? "New" : (i % 3 == 1 ? "Changed" : "Deleted"),
                    StoreId = $"Store{random.Next(1, 11):D2}",
                    Date = DateTime.Today.AddDays(random.Next(1, 30)),
                    TimeSlot = $"{random.Next(9, 22):D2}:00-{random.Next(9, 22):D2}:30",
                    OldRemain = random.Next(0, 10),
                    NewRemain = random.Next(0, 10),
                    UpdatedAt = DateTime.Now.AddSeconds(-random.Next(0, 3600))
                });
            }

            return changes;
        }
    }
} 