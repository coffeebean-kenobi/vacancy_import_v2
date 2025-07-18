using System;
using System.Runtime.Serialization;

namespace VacancyImport.Exceptions;

/// <summary>
/// Excel関連の例外クラス
/// </summary>
[Serializable]
public class ExcelFileException : VacancyImportException, ISerializable
{
    /// <summary>
    /// 対象のExcelファイルパス
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public ExcelFileException(string message, string filePath, string errorCode = "EXCEL-ERR", 
        ErrorSeverity severity = ErrorSeverity.Error, bool isRetryable = true, Exception? innerException = null)
        : base(message, errorCode, severity, isRetryable, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// ファイルが見つからない場合の例外を作成
    /// </summary>
    public static ExcelFileException FileNotFound(string filePath, Exception? innerException = null)
    {
        return new ExcelFileException(
            $"Excelファイルが見つかりません: {filePath}", 
            filePath, 
            "EXCEL-NOT-FOUND", 
            ErrorSeverity.Error, 
            false, 
            innerException);
    }

    /// <summary>
    /// ファイルの読み取りエラーの例外を作成
    /// </summary>
    public static ExcelFileException ReadError(string filePath, Exception? innerException = null)
    {
        return new ExcelFileException(
            $"Excelファイルの読み取りに失敗しました: {filePath}", 
            filePath, 
            "EXCEL-READ-ERR", 
            ErrorSeverity.Error, 
            true, 
            innerException);
    }

    /// <summary>
    /// データ形式エラーの例外を作成
    /// </summary>
    public static ExcelFileException DataFormatError(string filePath, string details, Exception? innerException = null)
    {
        return new ExcelFileException(
            $"Excelファイルのデータ形式が不正です: {filePath} - {details}", 
            filePath, 
            "EXCEL-FORMAT-ERR", 
            ErrorSeverity.Error, 
            false, 
            innerException);
    }

    /// <summary>
    /// シリアル化コンストラクタ
    /// </summary>
    protected ExcelFileException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        FilePath = info.GetString(nameof(FilePath)) ?? string.Empty;
    }

    /// <summary>
    /// シリアル化データの取得
    /// </summary>
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(FilePath), FilePath);
    }
} 