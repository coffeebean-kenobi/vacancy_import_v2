using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic; // Added missing import for List

namespace VacancyImport.Utilities;

/// <summary>
/// ファイルロック状態を検出するユーティリティ
/// </summary>
public static class FileLockChecker
{
    /// <summary>
    /// ファイルがロックされているかどうかを非同期でチェック
    /// </summary>
    /// <param name="filePath">チェックするファイルパス</param>
    /// <param name="logger">ロガー</param>
    /// <param name="timeoutMs">タイムアウト時間（ミリ秒）</param>
    /// <returns>ロックされていない場合はtrue</returns>
    public static async Task<bool> IsFileUnlockedAsync(string filePath, ILogger logger, int timeoutMs = 1000)
    {
        try
        {
            // 一時ファイル（~$で始まるファイル）は除外
            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith("~$"))
            {
                logger.LogDebug("一時ファイルを除外: {FilePath}", filePath);
                return false;
            }

            // ファイルが存在しない場合は除外
            if (!File.Exists(filePath))
            {
                logger.LogDebug("ファイルが存在しません: {FilePath}", filePath);
                return false;
            }

            // Windows Service環境での適切なタイムアウト設定
            var actualTimeout = Environment.UserInteractive ? timeoutMs : Math.Min(timeoutMs, 500);
            
            // ファイルロックチェック（リトライ付き）
            var startTime = DateTime.UtcNow;
            var maxAttempts = 3;
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await Task.Run(() =>
                    {
                        try
                        {
                            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            return true;
                        }
                        catch (IOException ex)
                        {
                            logger.LogDebug("ファイルがロックされています (試行 {Attempt}/{MaxAttempts}): {FilePath}, エラー: {Error}", 
                                attempt, maxAttempts, filePath, ex.Message);
                            return false;
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            logger.LogDebug("ファイルへのアクセス権限がありません (試行 {Attempt}/{MaxAttempts}): {FilePath}, エラー: {Error}", 
                                attempt, maxAttempts, filePath, ex.Message);
                            return false;
                        }
                    });
                    
                    if (result)
                    {
                        logger.LogDebug("ファイルロックチェック成功 (試行 {Attempt}/{MaxAttempts}): {FilePath}", 
                            attempt, maxAttempts, filePath);
                        return true;
                    }
                    
                    // タイムアウトチェック
                    if (DateTime.UtcNow - startTime > TimeSpan.FromMilliseconds(actualTimeout))
                    {
                        logger.LogWarning("ファイルロックチェックがタイムアウトしました: {FilePath}, タイムアウト: {Timeout}ms", 
                            filePath, actualTimeout);
                        return false;
                    }
                    
                    // リトライ前に少し待機（指数バックオフ）
                    if (attempt < maxAttempts)
                    {
                        var delayMs = 100 * attempt;
                        await Task.Delay(delayMs);
                        logger.LogDebug("ファイルロックチェックをリトライ中 (試行 {Attempt}/{MaxAttempts}): {FilePath}, 待機: {Delay}ms", 
                            attempt, maxAttempts, filePath, delayMs);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ファイルロックチェック中にエラーが発生しました (試行 {Attempt}/{MaxAttempts}): {FilePath}", 
                        attempt, maxAttempts, filePath);
                    
                    if (attempt == maxAttempts)
                    {
                        return false;
                    }
                }
            }
            
            logger.LogWarning("ファイルロックチェックが失敗しました (最大試行回数到達): {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ファイルロックチェック中に予期しないエラーが発生しました: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// 複数ファイルのロック状態を一括チェック
    /// </summary>
    /// <param name="filePaths">チェックするファイルパスのリスト</param>
    /// <param name="logger">ロガー</param>
    /// <returns>ロックされていないファイルのリスト</returns>
    public static async Task<string[]> GetUnlockedFilesAsync(string[] filePaths, ILogger logger)
    {
        var unlockedFiles = new List<string>();
        var startTime = DateTime.UtcNow;
        
        logger.LogInformation("ファイルロックチェックを開始します: {TotalFiles}個のファイル", filePaths.Length);

        // 並列処理でロックチェックを実行（Windows Service環境では制限）
        var maxConcurrency = Environment.UserInteractive ? 4 : 2;
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = new List<Task<(string FilePath, bool IsUnlocked)>>();

        foreach (var filePath in filePaths)
        {
            tasks.Add(CheckFileLockWithSemaphoreAsync(filePath, logger, semaphore));
        }

        // すべてのタスクの完了を待機
        var results = await Task.WhenAll(tasks);
        
        // 結果を収集
        foreach (var result in results)
        {
            if (result.IsUnlocked)
            {
                unlockedFiles.Add(result.FilePath);
            }
        }

        var elapsed = DateTime.UtcNow - startTime;
        logger.LogInformation("ロックチェック完了: {TotalFiles}個中{UnlockedFiles}個が利用可能 (処理時間: {Elapsed:F2}秒)", 
            filePaths.Length, unlockedFiles.Count, elapsed.TotalSeconds);

        return unlockedFiles.ToArray();
    }
    
    /// <summary>
    /// セマフォを使用したファイルロックチェック
    /// </summary>
    private static async Task<(string FilePath, bool IsUnlocked)> CheckFileLockWithSemaphoreAsync(
        string filePath, ILogger logger, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            var isUnlocked = await IsFileUnlockedAsync(filePath, logger);
            return (filePath, isUnlocked);
        }
        finally
        {
            semaphore.Release();
        }
    }
} 