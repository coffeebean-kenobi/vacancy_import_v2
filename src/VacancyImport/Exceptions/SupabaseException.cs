using System;
using System.Runtime.Serialization;

namespace VacancyImport.Exceptions;

/// <summary>
/// Supabase関連の例外クラス
/// </summary>
[Serializable]
public class SupabaseException : VacancyImportException, ISerializable
{
    /// <summary>
    /// エンドポイントURL
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// レスポンスコード
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SupabaseException(string message, string endpoint, int? statusCode = null, string errorCode = "SUPABASE-ERR", 
        ErrorSeverity severity = ErrorSeverity.Error, bool isRetryable = true, Exception? innerException = null)
        : base(message, errorCode, severity, isRetryable, innerException)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
    }

    /// <summary>
    /// 認証エラーの例外を作成
    /// </summary>
    public static SupabaseException AuthenticationError(string endpoint, int statusCode, Exception? innerException = null)
    {
        return new SupabaseException(
            $"Supabase認証エラー: {endpoint} (StatusCode: {statusCode})", 
            endpoint, 
            statusCode, 
            "SUPABASE-AUTH-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// 接続エラーの例外を作成
    /// </summary>
    public static SupabaseException ConnectionError(string endpoint, Exception? innerException = null)
    {
        return new SupabaseException(
            $"Supabase接続エラー: {endpoint}", 
            endpoint, 
            null, 
            "SUPABASE-CONN-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// データ操作エラーの例外を作成
    /// </summary>
    public static SupabaseException DataOperationError(string endpoint, string details, int? statusCode = null, Exception? innerException = null)
    {
        return new SupabaseException(
            $"Supabaseデータ操作エラー: {endpoint} - {details}", 
            endpoint, 
            statusCode, 
            "SUPABASE-DATA-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// シリアル化コンストラクタ
    /// </summary>
    protected SupabaseException(SerializationInfo info, StreamingContext context)
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