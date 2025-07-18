namespace VacancyImport.Exceptions;

/// <summary>
/// エラーの重要度を表す列挙型
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// 情報レベル - 処理は継続可能
    /// </summary>
    Info = 0,

    /// <summary>
    /// 警告レベル - 処理は継続可能だが、注意が必要
    /// </summary>
    Warning = 1,

    /// <summary>
    /// エラーレベル - 処理は継続できないが、システム全体には影響なし
    /// </summary>
    Error = 2,

    /// <summary>
    /// 致命的レベル - システム全体に影響する重大なエラー
    /// </summary>
    Critical = 3
} 