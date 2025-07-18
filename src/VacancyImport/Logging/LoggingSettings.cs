using System;
using System.Collections.Generic;

namespace VacancyImport.Logging;

/// <summary>
/// アプリケーションのログ設定
/// </summary>
public class LoggingSettings
{
    /// <summary>
    /// デフォルトのログレベル
    /// </summary>
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 名前空間ごとのログレベル設定
    /// </summary>
    public Dictionary<string, LogLevel> LogLevels { get; set; } = new();

    /// <summary>
    /// ログファイルのパス
    /// </summary>
    public string LogFilePath { get; set; } = "logs/app.log";

    /// <summary>
    /// コンソールにログ出力するかどうか
    /// </summary>
    public bool EnableConsoleLogging { get; set; } = true;

    /// <summary>
    /// ファイルにログ出力するかどうか
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// システムログ（Windowsイベントログ、Syslog）に出力するかどうか
    /// </summary>
    public bool EnableSystemLogging { get; set; } = false;

    /// <summary>
    /// ログファイルのサイズ制限（MB）
    /// </summary>
    public int FileSizeLimitMB { get; set; } = 10;

    /// <summary>
    /// 保持するログファイルの最大数
    /// </summary>
    public int MaxRollingFiles { get; set; } = 5;

    /// <summary>
    /// ログの保持期間（日数）
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// ログのフォーマット
    /// </summary>
    public string LogFormat { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}";

    /// <summary>
    /// 構造化ロギングを有効にするかどうか
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// パフォーマンスメトリクスを記録するかどうか
    /// </summary>
    public bool EnablePerformanceLogging { get; set; } = true;

    /// <summary>
    /// パフォーマンスログの間隔（秒）
    /// </summary>
    public int PerformanceLoggingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// ログの圧縮を有効にするかどうか
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// 環境名
    /// </summary>
    public string EnvironmentName { get; set; } = "Production";

    /// <summary>
    /// アプリケーション名
    /// </summary>
    public string ApplicationName { get; set; } = "VacancyImport";

    /// <summary>
    /// 機密情報マスキングを有効にするかどうか
    /// </summary>
    public bool EnableSensitiveDataMasking { get; set; } = true;

    /// <summary>
    /// 指定された名前空間のログレベルを取得
    /// </summary>
    public LogLevel GetLogLevelForNamespace(string namespaceName)
    {
        // 完全一致の名前空間があればそのログレベルを返す
        if (LogLevels.TryGetValue(namespaceName, out var exactLevel))
        {
            return exactLevel;
        }

        // 前方一致の名前空間を探す
        foreach (var kvp in LogLevels)
        {
            if (namespaceName.StartsWith(kvp.Key + ".", StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // 一致する名前空間がなければデフォルトのログレベルを返す
        return DefaultLogLevel;
    }
} 