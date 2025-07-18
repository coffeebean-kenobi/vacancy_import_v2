using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VacancyImport.Exceptions;
using VacancyImport.Services;

namespace VacancyImport.Utilities;

/// <summary>
/// エラーハンドリングを提供するユーティリティクラス
/// </summary>
public class ErrorHandler
{
    private readonly ILogger<ErrorHandler> _logger;
    private readonly ILineWorksService? _lineWorksService;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public ErrorHandler(ILogger<ErrorHandler> logger, ILineWorksService? lineWorksService = null)
    {
        _logger = logger;
        _lineWorksService = lineWorksService;
    }

    /// <summary>
    /// 例外を処理し、適切なログ出力と通知を行う
    /// </summary>
    public async Task HandleExceptionAsync(Exception exception, string operationName)
    {
        var errorSeverity = GetErrorSeverity(exception);
        var errorDetails = FormatErrorDetails(exception, operationName);
        
        // エラーレベルに応じたログ出力
        switch (errorSeverity)
        {
            case ErrorSeverity.Info:
                _logger.LogInformation(exception, errorDetails);
                break;
            case ErrorSeverity.Warning:
                _logger.LogWarning(exception, errorDetails);
                break;
            case ErrorSeverity.Error:
                _logger.LogError(exception, errorDetails);
                break;
            case ErrorSeverity.Critical:
                _logger.LogCritical(exception, errorDetails);
                break;
            default:
                _logger.LogError(exception, errorDetails);
                break;
        }

        // 重大なエラーの場合は管理者に通知
        if (errorSeverity >= ErrorSeverity.Error && _lineWorksService != null)
        {
            try
            {
                await _lineWorksService.SendNotificationAsync(
                    $"【エラー通知】{errorSeverity}: {operationName}\n{exception.Message}");
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "エラー通知の送信に失敗しました");
            }
        }
    }

    /// <summary>
    /// エラーの重要度を取得
    /// </summary>
    private static ErrorSeverity GetErrorSeverity(Exception exception)
    {
        if (exception is VacancyImportException vacancyEx)
        {
            return vacancyEx.Severity;
        }

        // その他の例外は標準でErrorレベルとする
        return ErrorSeverity.Error;
    }

    /// <summary>
    /// 例外の詳細情報をフォーマット
    /// </summary>
    private static string FormatErrorDetails(Exception exception, string operationName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"操作: {operationName}");
        sb.AppendLine($"エラー: {exception.Message}");

        if (exception is VacancyImportException vacancyEx)
        {
            sb.AppendLine($"エラーコード: {vacancyEx.ErrorCode}");
            sb.AppendLine($"重要度: {vacancyEx.Severity}");
            sb.AppendLine($"リトライ可能: {(vacancyEx.IsRetryable ? "はい" : "いいえ")}");

            // 例外タイプに応じた追加情報
            if (exception is ExcelFileException excelEx)
            {
                sb.AppendLine($"ファイルパス: {excelEx.FilePath}");
            }
            else if (exception is SupabaseException supabaseEx)
            {
                sb.AppendLine($"エンドポイント: {supabaseEx.Endpoint}");
                if (supabaseEx.StatusCode.HasValue)
                {
                    sb.AppendLine($"ステータスコード: {supabaseEx.StatusCode}");
                }
            }
            else if (exception is LineWorksException lineWorksEx)
            {
                sb.AppendLine($"エンドポイント: {lineWorksEx.Endpoint}");
                if (lineWorksEx.StatusCode.HasValue)
                {
                    sb.AppendLine($"ステータスコード: {lineWorksEx.StatusCode}");
                }
            }
            else if (exception is ConfigurationException configEx)
            {
                sb.AppendLine($"設定キー: {configEx.ConfigKey}");
            }
        }

        // スタックトレース情報
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            sb.AppendLine("スタックトレース:");
            sb.AppendLine(exception.StackTrace);
        }

        // 内部例外がある場合は再帰的に追加
        if (exception.InnerException != null)
        {
            sb.AppendLine("内部例外:");
            sb.AppendLine(FormatErrorDetails(exception.InnerException, operationName));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 例外を安全に処理し、アプリケーションの実行を継続する
    /// </summary>
    public async Task<bool> SafeExecuteAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, operationName);
            return false;
        }
    }

    /// <summary>
    /// 例外を安全に処理し、アプリケーションの実行を継続する（戻り値あり）
    /// </summary>
    public async Task<(bool Success, T? Result)> SafeExecuteAsync<T>(Func<Task<T>> action, string operationName)
    {
        try
        {
            var result = await action();
            return (true, result);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, operationName);
            return (false, default);
        }
    }
} 