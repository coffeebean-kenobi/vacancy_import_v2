namespace VacancyImport.Exceptions;

/// <summary>
/// サービスの重大なエラーを表す例外
/// 連続エラー発生時など、サービスの停止が必要な状況で使用
/// </summary>
public class ServiceCriticalException : Exception
{
    public ServiceCriticalException() : base()
    {
    }

    public ServiceCriticalException(string message) : base(message)
    {
    }

    public ServiceCriticalException(string message, Exception innerException) : base(message, innerException)
    {
    }
} 