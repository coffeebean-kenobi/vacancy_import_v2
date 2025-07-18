using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace VacancyImport.Logging;

/// <summary>
/// ログ分析を提供するクラス
/// </summary>
public class LogAnalyzer
{
    private readonly ILogger<LogAnalyzer> _logger;
    private readonly LoggingSettings _settings;
    private readonly string _logDirectory;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public LogAnalyzer(ILogger<LogAnalyzer> logger, IOptions<LoggingSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        _logDirectory = Path.GetDirectoryName(Path.GetFullPath(_settings.LogFilePath))
                      ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    }

    /// <summary>
    /// エラーパターンを分析
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> AnalyzeErrorPatternsAsync(int days = 7)
    {
        try
        {
            _logger.LogInformation("エラーパターンの分析を開始します（過去{Days}日間）", days);
            
            var startDate = DateTime.Now.AddDays(-days);
            var errorPatterns = new Dictionary<string, int>();
            
            var files = await GetLogFilesAsync(startDate);
            foreach (var file in files)
            {
                await ProcessFileForErrorsAsync(file, errorPatterns);
            }
            
            // 出現頻度順にソート
            var sortedPatterns = errorPatterns
                .OrderByDescending(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            
            _logger.LogInformation("エラーパターンの分析が完了しました。{Count}種類のエラーが見つかりました", sortedPatterns.Count);
            return sortedPatterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "エラーパターンの分析中にエラーが発生しました");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// パフォーマンス分析
    /// </summary>
    public async Task<IReadOnlyDictionary<string, PerformanceMetrics>> AnalyzePerformanceAsync(int days = 1)
    {
        try
        {
            _logger.LogInformation("パフォーマンス分析を開始します（過去{Days}日間）", days);
            
            var startDate = DateTime.Now.AddDays(-days);
            var performanceByOperation = new Dictionary<string, List<double>>();
            
            var files = await GetLogFilesAsync(startDate);
            foreach (var file in files)
            {
                await ProcessFileForPerformanceAsync(file, performanceByOperation);
            }
            
            // 各操作の統計情報を計算
            var metrics = new Dictionary<string, PerformanceMetrics>();
            foreach (var kvp in performanceByOperation)
            {
                var timings = kvp.Value;
                if (timings.Count == 0) continue;
                
                metrics[kvp.Key] = new PerformanceMetrics
                {
                    OperationName = kvp.Key,
                    AverageTime = timings.Average(),
                    MinTime = timings.Min(),
                    MaxTime = timings.Max(),
                    Count = timings.Count,
                    Percentile95 = CalculatePercentile(timings, 95)
                };
            }
            
            // 平均時間順にソート
            var sortedMetrics = metrics
                .OrderByDescending(kv => kv.Value.AverageTime)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            
            _logger.LogInformation("パフォーマンス分析が完了しました。{Count}種類の操作が見つかりました", sortedMetrics.Count);
            return sortedMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "パフォーマンス分析中にエラーが発生しました");
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    /// <summary>
    /// 使用状況を分析
    /// </summary>
    public async Task<UsageAnalytics> AnalyzeUsageAsync(int days = 30)
    {
        try
        {
            _logger.LogInformation("使用状況の分析を開始します（過去{Days}日間）", days);
            
            var startDate = DateTime.Now.AddDays(-days);
            var usage = new UsageAnalytics
            {
                StartDate = startDate,
                EndDate = DateTime.Now,
                DailyActivity = new Dictionary<DateTime, int>(),
                OperationCounts = new Dictionary<string, int>(),
                ErrorCount = 0,
                WarningCount = 0
            };
            
            var files = await GetLogFilesAsync(startDate);
            foreach (var file in files)
            {
                await ProcessFileForUsageAsync(file, usage);
            }
            
            _logger.LogInformation("使用状況の分析が完了しました");
            return usage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "使用状況の分析中にエラーが発生しました");
            return new UsageAnalytics();
        }
    }

    /// <summary>
    /// レポートを生成
    /// </summary>
    public async Task GenerateReportAsync(string outputPath, int days = 7)
    {
        try
        {
            _logger.LogInformation("レポート生成を開始します（過去{Days}日間）", days);
            
            var errorPatterns = await AnalyzeErrorPatternsAsync(days);
            var performanceMetrics = await AnalyzePerformanceAsync(days);
            var usageAnalytics = await AnalyzeUsageAsync(days);
            
            var report = new
            {
                GeneratedAt = DateTime.Now,
                PeriodDays = days,
                ErrorPatterns = errorPatterns,
                Performance = performanceMetrics,
                Usage = usageAnalytics
            };
            
            var json = JsonConvert.SerializeObject(report, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json);
            
            _logger.LogInformation("レポートを生成しました: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "レポート生成中にエラーが発生しました: {OutputPath}", outputPath);
        }
    }

    /// <summary>
    /// 指定日付以降のログファイルを取得
    /// </summary>
    private async Task<IEnumerable<string>> GetLogFilesAsync(DateTime startDate)
    {
        var files = Directory.GetFiles(_logDirectory, "*.log*")
            .Union(Directory.GetFiles(_logDirectory, "*.gz"))
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTime >= startDate)
            .OrderBy(f => f.LastWriteTime)
            .Select(f => f.FullName)
            .ToList();

        return files;
    }

    /// <summary>
    /// エラーパターン分析のためのファイル処理
    /// </summary>
    private async Task ProcessFileForErrorsAsync(string filePath, Dictionary<string, int> errorPatterns)
    {
        string content;
        
        // 圧縮ファイルの場合は解凍
        if (filePath.EndsWith(".gz"))
        {
            using var fileStream = new FileStream(filePath, FileMode.Open);
            using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            content = await reader.ReadToEndAsync();
        }
        else
        {
            content = await File.ReadAllTextAsync(filePath);
        }
        
        // エラーログを抽出
        var errorRegex = new Regex(@"\[ERR(OR)?\].*?(?<message>.*?)(\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        var matches = errorRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var errorMessage = match.Groups["message"].Value.Trim();
                // エラーメッセージからエラーパターンを抽出（具体的な値を除去）
                var pattern = ExtractErrorPattern(errorMessage);
                
                if (!string.IsNullOrEmpty(pattern))
                {
                    if (errorPatterns.ContainsKey(pattern))
                    {
                        errorPatterns[pattern]++;
                    }
                    else
                    {
                        errorPatterns[pattern] = 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// パフォーマンス分析のためのファイル処理
    /// </summary>
    private async Task ProcessFileForPerformanceAsync(string filePath, Dictionary<string, List<double>> performanceByOperation)
    {
        string content;
        
        // 圧縮ファイルの場合は解凍
        if (filePath.EndsWith(".gz"))
        {
            using var fileStream = new FileStream(filePath, FileMode.Open);
            using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            content = await reader.ReadToEndAsync();
        }
        else
        {
            content = await File.ReadAllTextAsync(filePath);
        }
        
        // パフォーマンスログを抽出
        var perfRegex = new Regex(@"パフォーマンス測定: (?<operation>.*?) - (?<elapsed>\d+(\.\d+)?)ms", RegexOptions.Multiline);
        var matches = perfRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var operation = match.Groups["operation"].Value;
                if (double.TryParse(match.Groups["elapsed"].Value, out double elapsed))
                {
                    if (!performanceByOperation.ContainsKey(operation))
                    {
                        performanceByOperation[operation] = new List<double>();
                    }
                    
                    performanceByOperation[operation].Add(elapsed);
                }
            }
        }
    }

    /// <summary>
    /// 使用状況分析のためのファイル処理
    /// </summary>
    private async Task ProcessFileForUsageAsync(string filePath, UsageAnalytics usage)
    {
        string content;
        
        // 圧縮ファイルの場合は解凍
        if (filePath.EndsWith(".gz"))
        {
            using var fileStream = new FileStream(filePath, FileMode.Open);
            using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            content = await reader.ReadToEndAsync();
        }
        else
        {
            content = await File.ReadAllTextAsync(filePath);
        }
        
        // タイムスタンプを抽出
        var timeRegex = new Regex(@"(?<timestamp>\d{4}-\d{2}-\d{2})");
        var matches = timeRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            if (match.Success && DateTime.TryParse(match.Groups["timestamp"].Value, out DateTime date))
            {
                var dateOnly = date.Date;
                if (!usage.DailyActivity.ContainsKey(dateOnly))
                {
                    usage.DailyActivity[dateOnly] = 0;
                }
                
                usage.DailyActivity[dateOnly]++;
            }
        }
        
        // 操作名を抽出
        var operationRegex = new Regex(@"操作(?:開始|終了): (?<operation>.*?)( |$)");
        matches = operationRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var operation = match.Groups["operation"].Value;
                if (!usage.OperationCounts.ContainsKey(operation))
                {
                    usage.OperationCounts[operation] = 0;
                }
                
                usage.OperationCounts[operation]++;
            }
        }
        
        // エラーと警告の数を数える
        usage.ErrorCount += new Regex(@"\[ERR(OR)?\]", RegexOptions.IgnoreCase).Matches(content).Count;
        usage.WarningCount += new Regex(@"\[WARN(ING)?\]", RegexOptions.IgnoreCase).Matches(content).Count;
    }

    /// <summary>
    /// エラーメッセージからパターンを抽出
    /// </summary>
    private string ExtractErrorPattern(string errorMessage)
    {
        // 日付、時間、ID、GUID、パスなどの具体的な値を除去
        var pattern = Regex.Replace(errorMessage, @"\d{4}-\d{2}-\d{2}", "[DATE]");
        pattern = Regex.Replace(pattern, @"\d+\.\d+\.\d+\.\d+", "[IP]");
        pattern = Regex.Replace(pattern, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "[GUID]");
        pattern = Regex.Replace(pattern, @"[A-Za-z]:\\[\w\\]+", "[PATH]");
        pattern = Regex.Replace(pattern, @"/[\w/]+", "[PATH]");
        pattern = Regex.Replace(pattern, @"\d+", "[NUM]");
        
        return pattern.Trim();
    }

    /// <summary>
    /// パーセンタイル値を計算
    /// </summary>
    private double CalculatePercentile(List<double> values, int percentile)
    {
        var sortedValues = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, index)];
    }
}

/// <summary>
/// パフォーマンスメトリクス
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// 操作名
    /// </summary>
    public string OperationName { get; set; } = string.Empty;
    
    /// <summary>
    /// 平均時間（ミリ秒）
    /// </summary>
    public double AverageTime { get; set; }
    
    /// <summary>
    /// 最小時間（ミリ秒）
    /// </summary>
    public double MinTime { get; set; }
    
    /// <summary>
    /// 最大時間（ミリ秒）
    /// </summary>
    public double MaxTime { get; set; }
    
    /// <summary>
    /// 95パーセンタイル時間（ミリ秒）
    /// </summary>
    public double Percentile95 { get; set; }
    
    /// <summary>
    /// 実行回数
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// 使用状況分析
/// </summary>
public class UsageAnalytics
{
    /// <summary>
    /// 開始日
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// 終了日
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// 日別アクティビティ
    /// </summary>
    public Dictionary<DateTime, int> DailyActivity { get; set; } = new();
    
    /// <summary>
    /// 操作別実行回数
    /// </summary>
    public Dictionary<string, int> OperationCounts { get; set; } = new();
    
    /// <summary>
    /// エラー数
    /// </summary>
    public int ErrorCount { get; set; }
    
    /// <summary>
    /// 警告数
    /// </summary>
    public int WarningCount { get; set; }
} 