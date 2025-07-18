using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Utilities;

/// <summary>
/// パフォーマンスモニタリングを提供するクラス
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly Timer _reportingTimer;
    private readonly ConcurrentDictionary<string, ConcurrentBag<OperationMetrics>> _metrics = new();
    private readonly TimeSpan _reportingInterval;
    private readonly Process _currentProcess;
    private readonly bool _isEnabled;
    private readonly string _applicationName;
    private readonly int _alertThresholdMs;
    private const int DefaultReportingIntervalSeconds = 60;
    private const int DefaultAlertThresholdMs = 5000;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public PerformanceMonitor(ILogger<PerformanceMonitor> logger, IOptions<AppSettings> settings)
    {
        _logger = logger;
        var performanceSettings = settings?.Value?.PerformanceSettings;
        _isEnabled = performanceSettings?.EnablePerformanceMonitoring ?? true;
        _applicationName = settings?.Value?.ApplicationName ?? "VacancyImport";
        _reportingInterval = TimeSpan.FromSeconds(performanceSettings?.ReportingIntervalSeconds ?? DefaultReportingIntervalSeconds);
        _alertThresholdMs = performanceSettings?.AlertThresholdMs ?? DefaultAlertThresholdMs;
        
        _currentProcess = Process.GetCurrentProcess();
        
        if (_isEnabled)
        {
            _reportingTimer = new Timer(ReportPerformanceMetrics, null, _reportingInterval, _reportingInterval);
            logger.LogInformation("パフォーマンスモニタリングを開始しました (間隔: {Interval}秒)", _reportingInterval.TotalSeconds);
        }
        else
        {
            _reportingTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            logger.LogInformation("パフォーマンスモニタリングは無効化されています");
        }
    }

    /// <summary>
    /// 操作のパフォーマンスを計測
    /// </summary>
    public virtual IDisposable MeasureOperation(string operationName, Dictionary<string, object>? properties = null)
    {
        return new OperationMeasurement(this, operationName, properties);
    }

    /// <summary>
    /// 操作の測定結果を記録
    /// </summary>
    internal void RecordOperationMetrics(string operationName, TimeSpan duration, Dictionary<string, object>? properties = null)
    {
        if (!_isEnabled) return;

        var metrics = new OperationMetrics
        {
            OperationName = operationName,
            Duration = duration,
            Timestamp = DateTime.UtcNow,
            Properties = properties ?? new Dictionary<string, object>()
        };

        if (!_metrics.TryGetValue(operationName, out var operationMetrics))
        {
            operationMetrics = new ConcurrentBag<OperationMetrics>();
            _metrics[operationName] = operationMetrics;
        }

        operationMetrics.Add(metrics);

        // 閾値を超える処理時間の場合はすぐにアラートログを出力
        if (duration.TotalMilliseconds > _alertThresholdMs)
        {
            _logger.LogWarning("処理時間が閾値を超えました: {Operation} - {Duration}ms (閾値: {Threshold}ms)",
                operationName, duration.TotalMilliseconds, _alertThresholdMs);
        }
    }

    /// <summary>
    /// パフォーマンスメトリクスをレポート
    /// </summary>
    private void ReportPerformanceMetrics(object? state)
    {
        try
        {
            var memoryUsage = _currentProcess.WorkingSet64 / 1024 / 1024; // MB単位
            var cpuTime = _currentProcess.TotalProcessorTime;
            var threadCount = Process.GetCurrentProcess().Threads.Count;

            _logger.LogInformation(
                "システムリソース使用状況: メモリ={MemoryUsageMB}MB, CPUタイム={CpuTime}, スレッド数={ThreadCount}",
                memoryUsage, cpuTime, threadCount);

            foreach (var entry in _metrics.ToArray())
            {
                var operationName = entry.Key;
                var operationMetricsList = entry.Value.ToArray();

                if (operationMetricsList.Length == 0) continue;

                var avgDuration = operationMetricsList.Average(m => m.Duration.TotalMilliseconds);
                var maxDuration = operationMetricsList.Max(m => m.Duration.TotalMilliseconds);
                var minDuration = operationMetricsList.Min(m => m.Duration.TotalMilliseconds);
                var count = operationMetricsList.Length;

                _logger.LogInformation(
                    "操作パフォーマンス: {Operation}, 平均={AvgMs}ms, 最大={MaxMs}ms, 最小={MinMs}ms, 件数={Count}",
                    operationName, avgDuration, maxDuration, minDuration, count);

                // レポート後はメトリクスをクリア
                _metrics[operationName] = new ConcurrentBag<OperationMetrics>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "パフォーマンスメトリクスのレポート中にエラーが発生しました");
        }
    }

    /// <summary>
    /// 指定された操作名のパフォーマンスメトリクスを取得
    /// </summary>
    public OperationPerformanceSummary GetPerformanceSummary(string operationName)
    {
        if (!_metrics.TryGetValue(operationName, out var metrics) || !metrics.Any())
        {
            return new OperationPerformanceSummary
            {
                OperationName = operationName,
                AverageDurationMs = 0,
                MaxDurationMs = 0,
                MinDurationMs = 0,
                Count = 0
            };
        }

        var metricsList = metrics.ToList();
        return new OperationPerformanceSummary
        {
            OperationName = operationName,
            AverageDurationMs = metricsList.Average(m => m.Duration.TotalMilliseconds),
            MaxDurationMs = metricsList.Max(m => m.Duration.TotalMilliseconds),
            MinDurationMs = metricsList.Min(m => m.Duration.TotalMilliseconds),
            Count = metricsList.Count
        };
    }

    /// <summary>
    /// エラーを記録
    /// </summary>
    public void RecordError(Exception exception)
    {
        if (!_isEnabled) return;

        _logger.LogError(exception, "パフォーマンス監視: エラーが記録されました - {ErrorType}: {ErrorMessage}", 
            exception.GetType().Name, exception.Message);
    }

    /// <summary>
    /// リソースの破棄
    /// </summary>
    public void Dispose()
    {
        _reportingTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 単一操作のパフォーマンスメトリクス
    /// </summary>
    private class OperationMetrics
    {
        public string OperationName { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// 操作の計測を行うクラス
    /// </summary>
    private class OperationMeasurement : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Dictionary<string, object>? _properties;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public OperationMeasurement(PerformanceMonitor monitor, string operationName, Dictionary<string, object>? properties)
        {
            _monitor = monitor;
            _operationName = operationName;
            _properties = properties;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopwatch.Stop();
            _monitor.RecordOperationMetrics(_operationName, _stopwatch.Elapsed, _properties);
        }
    }
}

/// <summary>
/// 操作のパフォーマンス概要
/// </summary>
public class OperationPerformanceSummary
{
    public string OperationName { get; set; } = string.Empty;
    public double AverageDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public int Count { get; set; }
} 