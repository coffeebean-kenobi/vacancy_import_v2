using System;

namespace VacancyImport.Logging;

/// <summary>
/// アプリケーション固有のログレベルを定義
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// トレースレベル - 最も詳細なログ
    /// </summary>
    Trace = 0,

    /// <summary>
    /// デバッグレベル - 開発時のデバッグ情報
    /// </summary>
    Debug = 1,

    /// <summary>
    /// 情報レベル - 一般的な情報
    /// </summary>
    Information = 2,

    /// <summary>
    /// 警告レベル - 潜在的な問題
    /// </summary>
    Warning = 3,

    /// <summary>
    /// エラーレベル - エラーが発生したが、アプリケーションは継続可能
    /// </summary>
    Error = 4,

    /// <summary>
    /// 致命的レベル - アプリケーションの停止につながる重大なエラー
    /// </summary>
    Critical = 5,

    /// <summary>
    /// なし - ログを出力しない
    /// </summary>
    None = 6
}

/// <summary>
/// ログレベルの拡張メソッド
/// </summary>
public static class LogLevelExtensions
{
    /// <summary>
    /// アプリケーションのログレベルをMicrosoft.Extensions.Logging.LogLevelに変換
    /// </summary>
    public static Microsoft.Extensions.Logging.LogLevel ToMicrosoftLogLevel(this LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            LogLevel.None => Microsoft.Extensions.Logging.LogLevel.None,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }

    /// <summary>
    /// Microsoft.Extensions.Logging.LogLevelをアプリケーションのログレベルに変換
    /// </summary>
    public static LogLevel FromMicrosoftLogLevel(this Microsoft.Extensions.Logging.LogLevel level)
    {
        return level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.Trace,
            Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.Information,
            Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.Critical,
            Microsoft.Extensions.Logging.LogLevel.None => LogLevel.None,
            _ => LogLevel.Information
        };
    }
} 