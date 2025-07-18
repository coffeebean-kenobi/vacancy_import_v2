namespace VacancyImport.Models;

/// <summary>
/// 予約データの変更種別を表す列挙型
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// 新規追加
    /// </summary>
    New,

    /// <summary>
    /// 変更
    /// </summary>
    Changed,

    /// <summary>
    /// 削除
    /// </summary>
    Deleted
} 