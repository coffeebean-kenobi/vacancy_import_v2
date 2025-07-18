using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VacancyImport.Logging;

/// <summary>
/// パフォーマンスログを記録するためのユーティリティクラス
/// </summary>
public class PerformanceLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, object> _customProperties;
    private readonly bool _logStartAndEnd;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    private PerformanceLogger(ILogger logger, string operationName, bool logStartAndEnd, Dictionary<string, object>? properties = null)
    {
        _logger = logger;
        _operationName = operationName;
        _stopwatch = new Stopwatch();
        _customProperties = properties ?? new Dictionary<string, object>();
        _logStartAndEnd = logStartAndEnd;
    }

    /// <summary>
    /// パフォーマンスログ記録を開始
    /// </summary>
    public static PerformanceLogger Start(ILogger logger, string operationName, bool logStartAndEnd = true, Dictionary<string, object>? properties = null)
    {
        var perfLogger = new PerformanceLogger(logger, operationName, logStartAndEnd, properties);
        perfLogger.StartMeasurement();
        return perfLogger;
    }

    /// <summary>
    /// カスタムプロパティを追加
    /// </summary>
    public PerformanceLogger WithProperty(string key, object value)
    {
        _customProperties[key] = value;
        return this;
    }

    /// <summary>
    /// 計測を開始
    /// </summary>
    private void StartMeasurement()
    {
        _stopwatch.Start();
        
        if (_logStartAndEnd)
        {
            _logger.LogDebug("操作開始: {OperationName}", _operationName);
        }
    }

    /// <summary>
    /// 計測を停止して結果をログに記録
    /// </summary>
    public void Stop()
    {
        _stopwatch.Stop();
        var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
        
        using (_logger.BeginScope(_customProperties))
        {
            _logger.LogInformation("パフォーマンス測定: {OperationName} - {ElapsedMilliseconds}ms", _operationName, elapsedMs);
            
            if (_logStartAndEnd)
            {
                _logger.LogDebug("操作終了: {OperationName} - {ElapsedMilliseconds}ms", _operationName, elapsedMs);
            }
        }
    }

    /// <summary>
    /// 関数を実行してパフォーマンスを測定
    /// </summary>
    public static T Measure<T>(ILogger logger, string operationName, Func<T> func, Dictionary<string, object>? properties = null)
    {
        using var perfLogger = Start(logger, operationName, false, properties);
        try
        {
            var result = func();
            return result;
        }
        finally
        {
            perfLogger.Stop();
        }
    }

    /// <summary>
    /// 非同期関数を実行してパフォーマンスを測定
    /// </summary>
    public static async Task<T> MeasureAsync<T>(ILogger logger, string operationName, Func<Task<T>> func, Dictionary<string, object>? properties = null)
    {
        using var perfLogger = Start(logger, operationName, false, properties);
        try
        {
            var result = await func();
            return result;
        }
        finally
        {
            perfLogger.Stop();
        }
    }

    /// <summary>
    /// 非同期関数を実行してパフォーマンスを測定（戻り値なし）
    /// </summary>
    public static async Task MeasureAsync(ILogger logger, string operationName, Func<Task> func, Dictionary<string, object>? properties = null)
    {
        using var perfLogger = Start(logger, operationName, false, properties);
        try
        {
            await func();
        }
        finally
        {
            perfLogger.Stop();
        }
    }

    /// <summary>
    /// リソースの破棄
    /// </summary>
    public void Dispose()
    {
        if (_stopwatch.IsRunning)
        {
            Stop();
        }
    }
}

/// <summary>
/// ロガー拡張メソッド
/// </summary>
public static class PerformanceLoggerExtensions
{
    /// <summary>
    /// パフォーマンスログ記録を開始
    /// </summary>
    public static PerformanceLogger StartPerformanceLog(this ILogger logger, string operationName, bool logStartAndEnd = true)
    {
        return PerformanceLogger.Start(logger, operationName, logStartAndEnd);
    }

    /// <summary>
    /// 関数を実行してパフォーマンスを測定
    /// </summary>
    public static T MeasurePerformance<T>(this ILogger logger, string operationName, Func<T> func)
    {
        return PerformanceLogger.Measure(logger, operationName, func);
    }

    /// <summary>
    /// 非同期関数を実行してパフォーマンスを測定
    /// </summary>
    public static Task<T> MeasurePerformanceAsync<T>(this ILogger logger, string operationName, Func<Task<T>> func)
    {
        return PerformanceLogger.MeasureAsync(logger, operationName, func);
    }

    /// <summary>
    /// 非同期関数を実行してパフォーマンスを測定（戻り値なし）
    /// </summary>
    public static Task MeasurePerformanceAsync(this ILogger logger, string operationName, Func<Task> func)
    {
        return PerformanceLogger.MeasureAsync(logger, operationName, func);
    }
} 