using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VacancyImport.Utilities;

/// <summary>
/// ストリームデータ処理を提供するクラス
/// </summary>
public static class StreamProcessor
{
    /// <summary>
    /// ファイルをストリーミング処理
    /// </summary>
    /// <typeparam name="T">処理結果の型</typeparam>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="processFunc">行処理関数</param>
    /// <param name="bufferSize">バッファサイズ</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>処理結果のリスト</returns>
    public static async Task<IList<T>> ProcessFileStreamAsync<T>(
        string filePath, 
        Func<string, int, Task<T>> processFunc, 
        int bufferSize = 4096,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        
        using var fileStream = new FileStream(
            filePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.ReadWrite,
            bufferSize);
        
        using var reader = new StreamReader(fileStream, bufferSize: bufferSize);
        
        string? line;
        int lineNumber = 0;
        
        logger?.LogDebug("ファイルのストリーミング処理を開始: {FilePath}, バッファサイズ: {BufferSize}KB", 
            filePath, bufferSize / 1024);
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            lineNumber++;
            T result = await processFunc(line, lineNumber);
            results.Add(result);
            
            if (lineNumber % 10000 == 0)
            {
                logger?.LogDebug("ファイル処理進捗: {LineNumber}行処理済み, {FilePath}", 
                    lineNumber, filePath);
            }
        }
        
        logger?.LogDebug("ファイル処理完了: {LineNumber}行処理, {FilePath}", 
            lineNumber, filePath);
        
        return results;
    }
    
    /// <summary>
    /// CSVファイルをストリーミング処理
    /// </summary>
    /// <typeparam name="T">処理結果の型</typeparam>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="processFunc">行処理関数（カンマ区切りの列配列を受け取る）</param>
    /// <param name="skipHeader">ヘッダーをスキップするかどうか</param>
    /// <param name="bufferSize">バッファサイズ</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>処理結果のリスト</returns>
    public static async Task<IList<T>> ProcessCsvStreamAsync<T>(
        string filePath, 
        Func<string[], int, Task<T>> processFunc, 
        bool skipHeader = true,
        int bufferSize = 4096,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        
        using var fileStream = new FileStream(
            filePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.ReadWrite,
            bufferSize);
        
        using var reader = new StreamReader(fileStream, bufferSize: bufferSize);
        
        string? line;
        int lineNumber = 0;
        
        logger?.LogDebug("CSVファイルのストリーミング処理を開始: {FilePath}, バッファサイズ: {BufferSize}KB", 
            filePath, bufferSize / 1024);
        
        // ヘッダー行をスキップ
        if (skipHeader && !reader.EndOfStream)
        {
            await reader.ReadLineAsync();
            lineNumber++;
        }
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            lineNumber++;
            string[] columns = line.Split(',');
            T result = await processFunc(columns, lineNumber);
            results.Add(result);
            
            if (lineNumber % 10000 == 0)
            {
                logger?.LogDebug("CSVファイル処理進捗: {LineNumber}行処理済み, {FilePath}", 
                    lineNumber, filePath);
            }
        }
        
        logger?.LogDebug("CSVファイル処理完了: {LineNumber}行処理, {FilePath}", 
            lineNumber, filePath);
        
        return results;
    }
    
    /// <summary>
    /// ラージファイルの書き込み
    /// </summary>
    /// <typeparam name="T">データ型</typeparam>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="items">書き込みアイテム</param>
    /// <param name="lineFormatter">行フォーマット関数</param>
    /// <param name="header">ヘッダー行（オプション）</param>
    /// <param name="bufferSize">バッファサイズ</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    public static async Task WriteFileStreamAsync<T>(
        string filePath, 
        IEnumerable<T> items, 
        Func<T, string> lineFormatter,
        string? header = null,
        int bufferSize = 4096,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        // ディレクトリの作成
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var fileStream = new FileStream(
            filePath, 
            FileMode.Create, 
            FileAccess.Write, 
            FileShare.None,
            bufferSize);
        
        using var writer = new StreamWriter(fileStream, bufferSize: bufferSize);
        
        logger?.LogDebug("ファイルのストリーミング書き込みを開始: {FilePath}, バッファサイズ: {BufferSize}KB", 
            filePath, bufferSize / 1024);
        
        // ヘッダーの書き込み
        if (!string.IsNullOrEmpty(header))
        {
            await writer.WriteLineAsync(header);
        }
        
        int itemCount = 0;
        
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await writer.WriteLineAsync(lineFormatter(item));
            itemCount++;
            
            if (itemCount % 10000 == 0)
            {
                logger?.LogDebug("ファイル書き込み進捗: {ItemCount}アイテム書き込み済み, {FilePath}", 
                    itemCount, filePath);
                
                // バッファをフラッシュ
                await writer.FlushAsync();
            }
        }
        
        await writer.FlushAsync();
        
        logger?.LogDebug("ファイル書き込み完了: {ItemCount}アイテム書き込み, {FilePath}", 
            itemCount, filePath);
    }
} 