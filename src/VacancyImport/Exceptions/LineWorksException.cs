using System;
using System.Runtime.Serialization;

namespace VacancyImport.Exceptions;

/// <summary>
/// LINE WORKS関連の例外クラス
/// </summary>
[Serializable]
public class LineWorksException : VacancyImportException, ISerializable
{
    /// <summary>
    /// APIエンドポイント
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// レスポンスコード
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public LineWorksException(string message, string endpoint, int? statusCode = null, string errorCode = "LINEWORKS-ERR", 
        ErrorSeverity severity = ErrorSeverity.Error, bool isRetryable = true, Exception? innerException = null)
        : base(message, errorCode, severity, isRetryable, innerException)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
    }

    /// <summary>
    /// 認証エラーの例外を作成
    /// </summary>
    public static LineWorksException AuthenticationError(string endpoint, int statusCode, Exception? innerException = null)
    {
        return new LineWorksException(
            $"LINE WORKS認証エラー: {endpoint} (StatusCode: {statusCode})", 
            endpoint, 
            statusCode, 
            "LINEWORKS-AUTH-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// 接続エラーの例外を作成
    /// </summary>
    public static LineWorksException ConnectionError(string endpoint, Exception? innerException = null)
    {
        return new LineWorksException(
            $"LINE WORKS接続エラー: {endpoint}", 
            endpoint, 
            null, 
            "LINEWORKS-CONN-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// メッセージ送信エラーの例外を作成
    /// </summary>
    public static LineWorksException MessageSendError(string endpoint, string details, int? statusCode = null, Exception? innerException = null)
    {
        return new LineWorksException(
            $"LINE WORKSメッセージ送信エラー: {endpoint} - {details}", 
            endpoint, 
            statusCode, 
            "LINEWORKS-MSG-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// トークン取得エラーの例外を作成
    /// </summary>
    public static LineWorksException TokenError(string endpoint, int statusCode, Exception? innerException = null)
    {
        return new LineWorksException(
            $"LINE WORKSトークン取得エラー: {endpoint} (StatusCode: {statusCode})", 
            endpoint, 
            statusCode, 
            "LINEWORKS-TOKEN-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// シリアル化コンストラクタ
    /// </summary>
    protected LineWorksException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Endpoint = info.GetString(nameof(Endpoint)) ?? string.Empty;
        StatusCode = info.GetValue(nameof(StatusCode), typeof(int?)) as int?;
    }

    /// <summary>
    /// シリアル化データの取得
    /// </summary>
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Endpoint), Endpoint);
        info.AddValue(nameof(StatusCode), StatusCode);
    }
} 