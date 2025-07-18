using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VacancyImport.Exceptions;

namespace VacancyImport.Utilities;

/// <summary>
/// リトライポリシーを提供するユーティリティクラス
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// 指定された関数をリトライポリシーに従って実行
    /// </summary>
    /// <typeparam name="T">戻り値の型</typeparam>
    /// <param name="func">実行する関数</param>
    /// <param name="retryCount">最大リトライ回数</param>
    /// <param name="initialDelay">初回遅延時間（ミリ秒）</param>
    /// <param name="maxDelay">最大遅延時間（ミリ秒）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>関数の実行結果</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> func,
        int retryCount = 3,
        int initialDelay = 200,
        int maxDelay = 5000,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay;
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryCount)
        {
            try
            {
                if (attempt > 0)
                {
                    logger?.LogWarning("リトライ実行中 (試行回数: {Attempt}/{RetryCount}, 遅延: {Delay}ms)", 
                        attempt, retryCount, delay);
                }

                return await func();
            }
            catch (Exception ex)
            {
                attempt++;
                lastException = ex;

                // リトライ不可能な例外の場合は即座に再スロー
                if (ex is VacancyImportException vacancyEx && !vacancyEx.IsRetryable)
                {
                    logger?.LogError(ex, "リトライ不可能なエラーが発生しました");
                    throw;
                }

                // 最大リトライ回数に達した場合は例外をスロー
                if (attempt > retryCount)
                {
                    logger?.LogError(ex, "最大リトライ回数に達しました ({RetryCount}回)", retryCount);
                    throw;
                }

                logger?.LogWarning(ex, "エラーが発生しました。{Delay}ms後にリトライします (試行回数: {Attempt}/{RetryCount})", 
                    delay, attempt, retryCount);

                // 遅延を指数バックオフで増加させる
                await Task.Delay(delay, cancellationToken);
                delay = Math.Min(delay * 2, maxDelay);
            }
        }

        // ここには到達しないはずだが、コンパイラのために例外をスロー
        throw new InvalidOperationException("リトライ処理中に予期しないエラーが発生しました", lastException);
    }

    /// <summary>
    /// 指定された関数をリトライポリシーに従って実行（戻り値なし）
    /// </summary>
    /// <param name="func">実行する関数</param>
    /// <param name="retryCount">最大リトライ回数</param>
    /// <param name="initialDelay">初回遅延時間（ミリ秒）</param>
    /// <param name="maxDelay">最大遅延時間（ミリ秒）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> func,
        int retryCount = 3,
        int initialDelay = 200,
        int maxDelay = 5000,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await func();
            return true;
        }, retryCount, initialDelay, maxDelay, logger, cancellationToken);
    }

    /// <summary>
    /// 指定された条件が満たされるまで待機
    /// </summary>
    /// <param name="condition">条件関数</param>
    /// <param name="timeout">タイムアウト（ミリ秒）</param>
    /// <param name="checkInterval">チェック間隔（ミリ秒）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>条件が満たされたかどうか</returns>
    public static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        int timeout = 30000,
        int checkInterval = 500,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMilliseconds(timeout);

        while (DateTime.UtcNow < endTime)
        {
            if (await condition())
            {
                return true;
            }

            if (DateTime.UtcNow.AddMilliseconds(checkInterval) >= endTime)
            {
                break;
            }

            await Task.Delay(checkInterval, cancellationToken);
        }

        logger?.LogWarning("条件が満たされないままタイムアウトしました ({Timeout}ms)", timeout);
        return false;
    }
} 