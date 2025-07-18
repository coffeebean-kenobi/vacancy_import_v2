namespace VacancyImport.Configuration;

/// <summary>
/// サービス実行に関する設定
/// </summary>
public class ServiceSettings
{
    /// <summary>
    /// サービス名
    /// </summary>
    public string ServiceName { get; set; } = "VacancyImportService";
    
    /// <summary>
    /// サービス表示名
    /// </summary>
    public string ServiceDisplayName { get; set; } = "予約管理システム連携サービス";
    
    /// <summary>
    /// サービス説明
    /// </summary>
    public string ServiceDescription { get; set; } = "Excel予約管理シートからデータを抽出し、Supabaseデータベースに同期、LINE WORKSで通知を行うサービス";
    
    /// <summary>
    /// ポーリング間隔（分）
    /// </summary>
    public int PollingIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// 最大再試行回数
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// リトライ間隔（分）
    /// </summary>
    public int RetryIntervalMinutes { get; set; } = 1;
    
    /// <summary>
    /// 自動開始を有効にするかどうか
    /// </summary>
    public bool AutoStart { get; set; } = true;
    
    /// <summary>
    /// 詳細ログを有効にするかどうか
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;
} 