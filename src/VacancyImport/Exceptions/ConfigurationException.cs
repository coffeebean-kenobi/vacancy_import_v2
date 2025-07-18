using System;
using System.Runtime.Serialization;

namespace VacancyImport.Exceptions;

/// <summary>
/// 設定関連の例外クラス
/// </summary>
[Serializable]
public class ConfigurationException : VacancyImportException, ISerializable
{
    /// <summary>
    /// 設定キー
    /// </summary>
    public string ConfigKey { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public ConfigurationException(string message, string configKey, string errorCode = "CONFIG-ERR", 
        ErrorSeverity severity = ErrorSeverity.Error, bool isRetryable = false, Exception? innerException = null)
        : base(message, errorCode, severity, isRetryable, innerException)
    {
        ConfigKey = configKey;
    }

    /// <summary>
    /// 設定キーが見つからない場合の例外を作成
    /// </summary>
    public static ConfigurationException KeyNotFound(string configKey, Exception? innerException = null)
    {
        return new ConfigurationException(
            $"設定キーが見つかりません: {configKey}", 
            configKey, 
            "CONFIG-KEY-NOT-FOUND", 
            ErrorSeverity.Error, 
            false, 
            innerException);
    }

    /// <summary>
    /// 設定値が無効な場合の例外を作成
    /// </summary>
    public static ConfigurationException InvalidValue(string configKey, string details, Exception? innerException = null)
    {
        return new ConfigurationException(
            $"設定値が無効です: {configKey} - {details}", 
            configKey, 
            "CONFIG-INVALID-VALUE", 
            ErrorSeverity.Error, 
            false, 
            innerException);
    }

    /// <summary>
    /// 設定ファイルが見つからない場合の例外を作成
    /// </summary>
    public static ConfigurationException FileNotFound(string fileName, Exception? innerException = null)
    {
        return new ConfigurationException(
            $"設定ファイルが見つかりません: {fileName}", 
            fileName, 
            "CONFIG-FILE-NOT-FOUND", 
            ErrorSeverity.Critical, 
            false, 
            innerException);
    }

    /// <summary>
    /// シリアル化コンストラクタ
    /// </summary>
    protected ConfigurationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ConfigKey = info.GetString(nameof(ConfigKey)) ?? string.Empty;
    }

    /// <summary>
    /// シリアル化データの取得
    /// </summary>
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ConfigKey), ConfigKey);
    }
} 