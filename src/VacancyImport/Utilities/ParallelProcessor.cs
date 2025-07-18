using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VacancyImport.Utilities;

/// <summary>
/// 並列処理を提供するクラス
/// </summary>
public static class ParallelProcessor
{
    /// <summary>
    /// 項目のコレクションを並列処理
    /// </summary>
    /// <typeparam name="TInput">入力の型</typeparam>
    /// <typeparam name="TOutput">出力の型</typeparam>
    /// <param name="items">処理する項目のコレクション</param>
    /// <param name="processFunc">項目の処理関数</param>
    /// <param name="maxDegreeOfParallelism">最大並列度</param>
    /// <param name="logger">ロガー</param>
    /// <param name="operationName">操作名（ログ用）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>処理結果のコレクション</returns>
    public static async Task<IReadOnlyCollection<TOutput>> ProcessParallelAsync<TInput, TOutput>(
        IEnumerable<TInput> items,
        Func<TInput, CancellationToken, Task<TOutput>> processFunc,
        int maxDegreeOfParallelism = 4,
        ILogger? logger = null,
        string operationName = "並列処理",
        CancellationToken cancellationToken = default)
    {
        var inputList = items.ToList();
        var results = new ConcurrentBag<TOutput>();
        var exceptions = new ConcurrentBag<Exception>();
        var totalItems = inputList.Count;
        var processedItems = 0;
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = new List<Task>();

        logger?.LogDebug("{OperationName}を開始: {Count}件, 最大並列度={MaxParallelism}",
            operationName, totalItems, maxDegreeOfParallelism);

        foreach (var item in inputList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var result = await processFunc(item, cancellationToken);
                    results.Add(result);

                    var processed = Interlocked.Increment(ref processedItems);
                    if (processed % 100 == 0 || processed == totalItems)
                    {
                        logger?.LogDebug("{OperationName}の進捗: {Processed}/{Total}件 ({PercentComplete}%)",
                            operationName, processed, totalItems, (processed * 100) / totalItems);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.LogError(ex, "{OperationName}の処理中にエラーが発生しました", operationName);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        semaphore.Dispose();

        if (exceptions.Any())
        {
            logger?.LogError("{OperationName}中に{ErrorCount}件のエラーが発生しました",
                operationName, exceptions.Count);

            throw new AggregateException($"{operationName}中に{exceptions.Count}件のエラーが発生しました", exceptions);
        }

        logger?.LogDebug("{OperationName}が完了しました: {Count}件処理", operationName, results.Count);

        return results;
    }

    /// <summary>
    /// 項目のバッチを並列処理
    /// </summary>
    /// <typeparam name="TInput">入力の型</typeparam>
    /// <typeparam name="TOutput">出力の型</typeparam>
    /// <param name="items">処理する項目のコレクション</param>
    /// <param name="batchSize">バッチサイズ</param>
    /// <param name="processBatchFunc">バッチ処理関数</param>
    /// <param name="maxDegreeOfParallelism">最大並列度</param>
    /// <param name="logger">ロガー</param>
    /// <param name="operationName">操作名（ログ用）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>処理結果のコレクション</returns>
    public static async Task<IReadOnlyCollection<TOutput>> ProcessBatchParallelAsync<TInput, TOutput>(
        IEnumerable<TInput> items,
        int batchSize,
        Func<IReadOnlyCollection<TInput>, CancellationToken, Task<IEnumerable<TOutput>>> processBatchFunc,
        int maxDegreeOfParallelism = 4,
        ILogger? logger = null,
        string operationName = "バッチ並列処理",
        CancellationToken cancellationToken = default)
    {
        var inputList = items.ToList();
        var results = new ConcurrentBag<TOutput>();
        var exceptions = new ConcurrentBag<Exception>();
        var totalItems = inputList.Count;
        var totalBatches = (int)Math.Ceiling((double)totalItems / batchSize);
        var processedBatches = 0;
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = new List<Task>();

        logger?.LogDebug("{OperationName}を開始: {Count}件, バッチサイズ={BatchSize}, バッチ数={BatchCount}, 最大並列度={MaxParallelism}",
            operationName, totalItems, batchSize, totalBatches, maxDegreeOfParallelism);

        // バッチに分割
        var batches = new List<IReadOnlyCollection<TInput>>();
        for (int i = 0; i < inputList.Count; i += batchSize)
        {
            batches.Add(inputList.Skip(i).Take(batchSize).ToList());
        }

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var batchResults = await processBatchFunc(batch, cancellationToken);
                    foreach (var result in batchResults)
                    {
                        results.Add(result);
                    }

                    var processed = Interlocked.Increment(ref processedBatches);
                    logger?.LogDebug("{OperationName}の進捗: {Processed}/{Total}バッチ ({PercentComplete}%)",
                        operationName, processed, totalBatches, (processed * 100) / totalBatches);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.LogError(ex, "{OperationName}の処理中にエラーが発生しました", operationName);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        semaphore.Dispose();

        if (exceptions.Any())
        {
            logger?.LogError("{OperationName}中に{ErrorCount}件のエラーが発生しました",
                operationName, exceptions.Count);

            throw new AggregateException($"{operationName}中に{exceptions.Count}件のエラーが発生しました", exceptions);
        }

        logger?.LogDebug("{OperationName}が完了しました: {BatchCount}バッチ, {ItemCount}件処理",
            operationName, totalBatches, results.Count);

        return results;
    }

    /// <summary>
    /// スロットリング付きの並列処理
    /// </summary>
    /// <typeparam name="TInput">入力の型</typeparam>
    /// <typeparam name="TOutput">出力の型</typeparam>
    /// <param name="items">処理する項目のコレクション</param>
    /// <param name="processFunc">項目の処理関数</param>
    /// <param name="maxDegreeOfParallelism">最大並列度</param>
    /// <param name="throttleDelayMs">スロットリング遅延（ミリ秒）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="operationName">操作名（ログ用）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>処理結果のコレクション</returns>
    public static async Task<IReadOnlyCollection<TOutput>> ProcessThrottledParallelAsync<TInput, TOutput>(
        IEnumerable<TInput> items,
        Func<TInput, CancellationToken, Task<TOutput>> processFunc,
        int maxDegreeOfParallelism = 4,
        int throttleDelayMs = 100,
        ILogger? logger = null,
        string operationName = "スロットリング並列処理",
        CancellationToken cancellationToken = default)
    {
        var inputList = items.ToList();
        var results = new ConcurrentBag<TOutput>();
        var exceptions = new ConcurrentBag<Exception>();
        var totalItems = inputList.Count;
        var processedItems = 0;
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = new List<Task>();

        logger?.LogDebug("{OperationName}を開始: {Count}件, 最大並列度={MaxParallelism}, スロットリング={ThrottleDelayMs}ms",
            operationName, totalItems, maxDegreeOfParallelism, throttleDelayMs);

        foreach (var item in inputList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // スロットリング
                    await Task.Delay(throttleDelayMs, cancellationToken);

                    var result = await processFunc(item, cancellationToken);
                    results.Add(result);

                    var processed = Interlocked.Increment(ref processedItems);
                    if (processed % 100 == 0 || processed == totalItems)
                    {
                        logger?.LogDebug("{OperationName}の進捗: {Processed}/{Total}件 ({PercentComplete}%)",
                            operationName, processed, totalItems, (processed * 100) / totalItems);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.LogError(ex, "{OperationName}の処理中にエラーが発生しました", operationName);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        semaphore.Dispose();

        if (exceptions.Any())
        {
            logger?.LogError("{OperationName}中に{ErrorCount}件のエラーが発生しました",
                operationName, exceptions.Count);

            throw new AggregateException($"{operationName}中に{exceptions.Count}件のエラーが発生しました", exceptions);
        }

        logger?.LogDebug("{OperationName}が完了しました: {Count}件処理", operationName, results.Count);

        return results;
    }
} 