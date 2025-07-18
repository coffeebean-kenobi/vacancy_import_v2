using System;
using System.Runtime.Serialization;

namespace VacancyImport.Exceptions;

/// <summary>
/// 予約インポートシステムの基底例外クラス
/// </summary>
[Serializable]
public abstract class VacancyImportException : Exception, ISerializable
{
    /// <summary>
    /// エラーコード
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// エラーの重要度
    /// </summary>
    public ErrorSeverity Severity { get; }

    /// <summary>
    /// リトライ可能かどうか
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    protected VacancyImportException(string message, string errorCode, ErrorSeverity severity, bool isRetryable, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Severity = severity;
        IsRetryable = isRetryable;
    }

    /// <summary>
    /// シリアル化コンストラクタ
    /// </summary>
    protected VacancyImportException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ErrorCode = info.GetString(nameof(ErrorCode)) ?? string.Empty;
        Severity = (ErrorSeverity)info.GetInt32(nameof(Severity));
        IsRetryable = info.GetBoolean(nameof(IsRetryable));
    }

    /// <summary>
    /// シリアル化データの取得
    /// </summary>
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ErrorCode), ErrorCode);
        info.AddValue(nameof(Severity), Severity);
        info.AddValue(nameof(IsRetryable), IsRetryable);
    }
} 