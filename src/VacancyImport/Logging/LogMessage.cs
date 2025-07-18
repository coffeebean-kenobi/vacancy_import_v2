using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VacancyImport.Logging;

/// <summary>
/// 標準化されたログメッセージ
/// </summary>
public class LogMessage
{
    /// <summary>
    /// タイムスタンプ
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ログレベル
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// ログメッセージ
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ソースコンテキスト（クラス名など）
    /// </summary>
    public string SourceContext { get; set; } = string.Empty;

    /// <summary>
    /// 操作ID（リクエスト追跡用）
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// 例外情報
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// カスタムプロパティ
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// 処理時間（ミリ秒）
    /// </summary>
    public double? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// アプリケーション名
    /// </summary>
    public string Application { get; set; } = string.Empty;

    /// <summary>
    /// 環境名
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// マシン名
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// スレッドID
    /// </summary>
    public int ThreadId { get; set; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public LogMessage()
    {
        MachineName = System.Environment.MachineName;
        ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// プロパティを追加
    /// </summary>
    public void AddProperty(string key, object value)
    {
        Properties[key] = value;
    }

    /// <summary>
    /// 構造化JSON形式に変換
    /// </summary>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.None);
    }

    /// <summary>
    /// 文字列表現
    /// </summary>
    public override string ToString()
    {
        var exceptionText = string.IsNullOrEmpty(Exception) ? string.Empty : $"\n{Exception}";
        var propertiesText = Properties.Count > 0 ? $" Properties: {JsonConvert.SerializeObject(Properties)}" : string.Empty;
        var elapsedText = ElapsedMilliseconds.HasValue ? $" [{ElapsedMilliseconds.Value}ms]" : string.Empty;
        
        return $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {SourceContext}: {Message}{elapsedText}{propertiesText}{exceptionText}";
    }
}

/// <summary>
/// ログメッセージのビルダークラス
/// </summary>
public class LogMessageBuilder
{
    private readonly LogMessage _logMessage = new();

    /// <summary>
    /// ログレベルを設定
    /// </summary>
    public LogMessageBuilder WithLevel(LogLevel level)
    {
        _logMessage.Level = level.ToString();
        return this;
    }

    /// <summary>
    /// メッセージを設定
    /// </summary>
    public LogMessageBuilder WithMessage(string message)
    {
        _logMessage.Message = message;
        return this;
    }

    /// <summary>
    /// ソースコンテキストを設定
    /// </summary>
    public LogMessageBuilder WithSourceContext(string sourceContext)
    {
        _logMessage.SourceContext = sourceContext;
        return this;
    }

    /// <summary>
    /// 操作IDを設定
    /// </summary>
    public LogMessageBuilder WithOperationId(string operationId)
    {
        _logMessage.OperationId = operationId;
        return this;
    }

    /// <summary>
    /// 例外情報を設定
    /// </summary>
    public LogMessageBuilder WithException(Exception? exception)
    {
        _logMessage.Exception = exception?.ToString();
        return this;
    }

    /// <summary>
    /// プロパティを追加
    /// </summary>
    public LogMessageBuilder WithProperty(string key, object value)
    {
        _logMessage.AddProperty(key, value);
        return this;
    }

    /// <summary>
    /// 処理時間を設定
    /// </summary>
    public LogMessageBuilder WithElapsedMilliseconds(double elapsedMilliseconds)
    {
        _logMessage.ElapsedMilliseconds = elapsedMilliseconds;
        return this;
    }

    /// <summary>
    /// アプリケーション名を設定
    /// </summary>
    public LogMessageBuilder WithApplication(string application)
    {
        _logMessage.Application = application;
        return this;
    }

    /// <summary>
    /// 環境名を設定
    /// </summary>
    public LogMessageBuilder WithEnvironment(string environment)
    {
        _logMessage.Environment = environment;
        return this;
    }

    /// <summary>
    /// ログメッセージを構築
    /// </summary>
    public LogMessage Build()
    {
        return _logMessage;
    }
} 