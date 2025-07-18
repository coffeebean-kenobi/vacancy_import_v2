using System.Collections.Generic;
using VacancyImport.Exceptions;

namespace VacancyImport.Configuration;

public class AppSettings
{
    public string ApplicationName { get; set; } = "VacancyImport";
    public ExcelSettings ExcelSettings { get; set; } = new();
    public SupabaseSettings SupabaseSettings { get; set; } = new();
    public LineWorksSettings LineWorksSettings { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public PerformanceSettings PerformanceSettings { get; set; } = new();
    public ProofListSettings ProofListSettings { get; set; } = new();
}

public class ExcelSettings
{
    public Dictionary<string, ExcelEnvironmentSettings> Environments { get; set; } = new();
    public int PollingIntervalMinutes { get; set; }
    public int RetryCount { get; set; }

    public ExcelEnvironmentSettings GetEnvironmentSettings(string environment)
    {
        if (Environments.TryGetValue(environment, out var settings))
        {
            return settings;
        }
        throw new ConfigurationException(
            $"Excel settings for environment '{environment}' not found.",
            environment,
            "CONFIG-KEY-NOT-FOUND",
            ErrorSeverity.Error,
            false);
    }
}

public class ExcelEnvironmentSettings
{
    public string BasePath { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int ColumnIndex { get; set; }
}

public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

public class LineWorksSettings
{
    public string BotId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string MessageUrl { get; set; } = string.Empty;
}

public class LoggingSettings
{
    public LogLevelSettings LogLevel { get; set; } = new();
    public string LogFilePath { get; set; } = string.Empty;
}

public class LogLevelSettings
{
    public string Default { get; set; } = string.Empty;
    public string Microsoft { get; set; } = string.Empty;
}

/// <summary>
/// プルーフリスト設定
/// </summary>
public class ProofListSettings
{
    /// <summary>
    /// プルーフリストの出力先ディレクトリ
    /// </summary>
    public string OutputDirectory { get; set; } = "./proofs";
    
    /// <summary>
    /// ファイル保持日数
    /// </summary>
    public int RetentionDays { get; set; } = 180;
    
    /// <summary>
    /// 自動クリーンアップを有効にするかどうか
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;
    
    /// <summary>
    /// CSVファイルのエンコーディング
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";
    
    /// <summary>
    /// CSVファイルの区切り文字
    /// </summary>
    public string Delimiter { get; set; } = ",";
}

/// <summary>
/// パフォーマンス設定
/// </summary>
public class PerformanceSettings
{
    /// <summary>
    /// パフォーマンスモニタリングを有効にするかどうか
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;
    
    /// <summary>
    /// レポート間隔（秒）
    /// </summary>
    public int ReportingIntervalSeconds { get; set; } = 60;
    
    /// <summary>
    /// アラート閾値（ミリ秒）
    /// </summary>
    public int AlertThresholdMs { get; set; } = 5000;
    
    /// <summary>
    /// データベースのバッチサイズ
    /// </summary>
    public int DatabaseBatchSize { get; set; } = 50;
    
    /// <summary>
    /// 並列処理の最大同時実行数
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 4;
    
    /// <summary>
    /// ファイル処理のバッファサイズ（KB）
    /// </summary>
    public int FileBufferSizeKB { get; set; } = 64;
    
    /// <summary>
    /// GCの設定
    /// </summary>
    public GCSettings GCSettings { get; set; } = new();
}

/// <summary>
/// ガベージコレクションの設定
/// </summary>
public class GCSettings
{
    /// <summary>
    /// サーバーGCモードを有効にするかどうか
    /// </summary>
    public bool UseServerGC { get; set; } = true;
    
    /// <summary>
    /// 並行GCを有効にするかどうか
    /// </summary>
    public bool ConcurrentGC { get; set; } = true;
    
    /// <summary>
    /// ラージオブジェクトヒープコンパクションを有効にするかどうか
    /// </summary>
    public bool LargeObjectHeapCompaction { get; set; } = false;
} 