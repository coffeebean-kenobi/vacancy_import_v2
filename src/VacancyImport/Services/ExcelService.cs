using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;
using VacancyImport.Exceptions;
using VacancyImport.Models;
using VacancyImport.Utilities;
using Microsoft.Extensions.Hosting;

namespace VacancyImport.Services;

/// <summary>
/// Excelファイル操作サービス
/// </summary>
public class ExcelService : IExcelService
{
    private readonly IOptions<AppSettings> _settings;
    private readonly ILogger<ExcelService> _logger;
    private readonly string _environment;
    private readonly ExcelEnvironmentSettings _excelSettings;
    private readonly MemoryCache<string, (DateTime LastWriteTime, string Hash)> _fileStateCache;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public ExcelService(
        IOptions<AppSettings> settings,
        ILogger<ExcelService> logger,
        IHostEnvironment environment,
        PerformanceMonitor performanceMonitor)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment?.EnvironmentName ?? throw new ArgumentNullException(nameof(environment));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        
        try
        {
            _excelSettings = settings.Value.ExcelSettings.GetEnvironmentSettings(_environment);
            
            // キャッシュの初期化
            _fileStateCache = new MemoryCache<string, (DateTime, string)>(
                logger,
                "FileStateCache",
                defaultExpirationMinutes: 60,
                cleanupIntervalMinutes: 15);
        }
        catch (Exception ex)
        {
            throw new ConfigurationException(
                $"Excel設定の取得に失敗しました: {_environment}", 
                "ExcelSettings", 
                "CONFIG-EXCEL-ERR", 
                ErrorSeverity.Critical, 
                false, 
                ex);
        }
    }

    /// <summary>
    /// ファイルの更新を確認（段階的処理対応）
    /// </summary>
    public async Task<bool> CheckFileUpdatesAsync()
    {
        return await CheckFileUpdatesAsync(string.Empty);
    }

    /// <summary>
    /// ファイルの更新を確認（段階的処理対応）
    /// </summary>
    public async Task<bool> CheckFileUpdatesAsync(string filePath)
    {
        using var performance = _performanceMonitor.MeasureOperation("CheckFileUpdates");
        
        return await RetryPolicy.ExecuteWithRetryAsync(
            async () =>
            {
                try
                {
                    var fullPath = Path.Combine(_excelSettings.BasePath, filePath);
                    
                    if (!Directory.Exists(_excelSettings.BasePath))
                    {
                        throw ExcelFileException.FileNotFound(_excelSettings.BasePath);
                    }
                    
                    var hasUpdates = false;
                    
                    // 対象ファイルをすべて取得
                    var files = Directory.GetFiles(_excelSettings.BasePath, "*.xlsm", SearchOption.AllDirectories);
                    
                    // ファイルロックチェックを実行
                    var unlockedFiles = await FileLockChecker.GetUnlockedFilesAsync(files, _logger);
                    
                    if (unlockedFiles.Length == 0)
                    {
                        _logger.LogWarning("処理可能なファイルが見つかりませんでした");
                        return false;
                    }
                    
                    // ロックされていないファイルのみを処理
                    var results = await ParallelProcessor.ProcessParallelAsync(
                        unlockedFiles,
                        async (file, ct) =>
                        {
                            try
                            {
                                var fileName = Path.GetFileName(file);
                                var fileInfo = new FileInfo(file);
                                
                                // キャッシュから前回の状態を取得
                                var cacheKey = file;
                                if (_fileStateCache.TryGet(cacheKey, out var cachedState))
                                {
                                    // ファイルの更新をチェック
                                    var currentHash = await CalculateFileHashAsync(file);
                                    var currentLastWriteTime = fileInfo.LastWriteTime;
                                    
                                    if (cachedState.Hash != currentHash)
                                    {
                                        _logger.LogInformation("ファイルが更新されました: {FilePath}", file);
                                        
                                        // キャッシュを更新
                                        _fileStateCache.Set(cacheKey, (currentLastWriteTime, currentHash));
                                        
                                        return true;
                                    }
                                }
                                else
                                {
                                    // 初回処理の場合
                                    var currentHash = await CalculateFileHashAsync(file);
                                    var currentLastWriteTime = fileInfo.LastWriteTime;
                                    
                                    _logger.LogInformation("新規ファイルを検出しました: {FilePath}", file);
                                    
                                    // キャッシュに保存
                                    _fileStateCache.Set(cacheKey, (currentLastWriteTime, currentHash));
                                    
                                    return true;
                                }
                                
                                return false;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "ファイル変更チェック中にエラーが発生しました: {FilePath}", file);
                                return false;
                            }
                        },
                        maxDegreeOfParallelism: 5,
                        _logger,
                        "ファイル変更チェック",
                        CancellationToken.None);
                    
                    hasUpdates = results.Any(r => r);
                    
                    if (hasUpdates)
                    {
                        _logger.LogInformation("ファイル更新を検出しました: {UpdatedFiles}個のファイルが更新されています", 
                            results.Count(r => r));
                    }
                    
                    return hasUpdates;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ファイル更新チェック中に予期しないエラーが発生しました");
                    throw;
                }
            },
            retryCount: 3,
            initialDelay: 1000,
            maxDelay: 5000,
            _logger);
    }

    /// <summary>
    /// 月別予約データを抽出（段階的処理対応）
    /// </summary>
    public async Task<IEnumerable<FacilityMonthlyReservation>> ExtractMonthlyReservationsAsync()
    {
        return await ExtractMonthlyReservationsAsync(string.Empty);
    }

    /// <summary>
    /// 月別予約データを抽出（段階的処理対応）
    /// </summary>
    public async Task<IEnumerable<FacilityMonthlyReservation>> ExtractMonthlyReservationsAsync(string filePath)
    {
        using var performance = _performanceMonitor.MeasureOperation("ExtractMonthlyReservations");
        
        return await RetryPolicy.ExecuteWithRetryAsync(
            async () =>
            {
                try
                {
                    if (!Directory.Exists(_excelSettings.BasePath))
                    {
                        throw ExcelFileException.FileNotFound(_excelSettings.BasePath);
                    }
                    
                    // 対象ファイルをすべて取得
                    var files = Directory.GetFiles(_excelSettings.BasePath, "*.xlsm", SearchOption.AllDirectories);
                    
                    // ファイルロックチェックを実行
                    var unlockedFiles = await FileLockChecker.GetUnlockedFilesAsync(files, _logger);
                    
                    if (unlockedFiles.Length == 0)
                    {
                        _logger.LogWarning("処理可能なファイルが見つかりませんでした");
                        return Enumerable.Empty<FacilityMonthlyReservation>();
                    }
                    
                    _logger.LogInformation("データ抽出を開始します: {FileCount}個のファイルを処理", unlockedFiles.Length);
                    
                    var allResults = new List<FacilityMonthlyReservation>();
                    var successCount = 0;
                    var errorCount = 0;
                    
                    // 段階的処理: ファイルごとに個別処理
                    foreach (var file in unlockedFiles)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(file);
                            var facilityId = ExtractFacilityIdFromFileName(fileName);
                            
                            if (facilityId == 0)
                            {
                                _logger.LogDebug("対象外のファイルをスキップ: {FileName}", fileName);
                                continue;
                            }
                            
                            _logger.LogDebug("ファイルを処理中: {FileName} (FacilityId: {FacilityId})", fileName, facilityId);
                            
                            // ファイルロックを取得してから処理を開始
                            await _fileLock.WaitAsync();
                            XLWorkbook? workbook = null;
                            var monthlyReservations = Enumerable.Empty<FacilityMonthlyReservation>();
                            
                            try
                            {
                                // ファイルがロックされているか再チェック
                                if (!await FileLockChecker.IsFileUnlockedAsync(file, _logger, 500))
                                {
                                    _logger.LogWarning("ファイルがロックされているためスキップ: {FileName}", fileName);
                                    continue;
                                }
                                
                                workbook = new XLWorkbook(file);
                                
                                // ワークシート名を動的に検出
                                var worksheet = workbook.Worksheet(_excelSettings.SheetName);
                                if (worksheet == null)
                                {
                                    // 代替ワークシート名を試行
                                    var availableSheets = workbook.Worksheets.Select(w => w.Name).ToList();
                                    _logger.LogWarning("ワークシート '{SheetName}' が見つかりません: {FileName}。利用可能なシート: {AvailableSheets}", 
                                        _excelSettings.SheetName, fileName, string.Join(", ", availableSheets));
                                    
                                    // 最初のワークシートを使用
                                    if (availableSheets.Any())
                                    {
                                        worksheet = workbook.Worksheet(availableSheets.First());
                                        _logger.LogInformation("代替ワークシート '{SheetName}' を使用します: {FileName}", 
                                            worksheet.Name, fileName);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                
                                monthlyReservations = ExtractMonthlyReservations(worksheet, facilityId);
                                allResults.AddRange(monthlyReservations);
                            }
                            finally
                            {
                                // リソースの確実な解放
                                if (workbook != null)
                                {
                                    try
                                    {
                                        workbook.Dispose();
                                        _logger.LogDebug("ワークブックを解放しました: {FileName}", fileName);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "ワークブックの解放中にエラーが発生しました: {FileName}", fileName);
                                    }
                                }
                                
                                // ファイルロックを解放
                                _fileLock.Release();
                            }
                            
                            successCount++;
                            _logger.LogDebug("ファイル処理完了: {FileName} ({ReservationCount}件の予約データ)", 
                                fileName, monthlyReservations.Count());
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            _logger.LogError(ex, "ファイル処理中にエラーが発生しました: {FilePath}", file);
                            // エラーが発生しても他のファイルの処理は継続
                        }
                    }
                    
                    _logger.LogInformation("データ抽出完了: 成功{SuccessCount}件, エラー{ErrorCount}件, 総予約データ{TotalReservations}件", 
                        successCount, errorCount, allResults.Count);
                    
                    return allResults;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "月別予約データ抽出中に予期しないエラーが発生しました");
                    throw;
                }
            },
            retryCount: 3,
            initialDelay: 1000,
            maxDelay: 5000,
            _logger);
    }

    /// <summary>
    /// プルーフリストを保存
    /// </summary>
    public async Task SaveProofListAsync(IEnumerable<FacilityMonthlyReservation> changes)
    {
        using var performance = _performanceMonitor.MeasureOperation("SaveProofList");
        
        await RetryPolicy.ExecuteWithRetryAsync(
            async () =>
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var fileName = $"proof_{timestamp}.csv";
                    var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proofs");
                    Directory.CreateDirectory(directory);

                    var filePath = Path.Combine(directory, fileName);
                    
                    // ストリーミング処理でCSVファイルに書き込み
                    await StreamProcessor.WriteFileStreamAsync(
                        filePath,
                        changes,
                        change => $"{change.FacilityId},{change.Year},{change.Month},{string.Join(",", change.ReservationCounts)}",
                        header: "FacilityId,Year,Month,ReservationCounts",
                        bufferSize: 8192,
                        logger: _logger);
                    
                    _logger.LogInformation("プルーフリストを保存しました: {FilePath}", filePath);
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "プルーフリストの保存中にIOエラーが発生しました");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "プルーフリストの保存中にエラーが発生しました");
                    throw;
                }
            },
            _settings.Value.ExcelSettings.RetryCount,
            200,
            5000,
            _logger);
    }

    /// <summary>
    /// ワークシート名を確認（デバッグ用）
    /// </summary>
    public Task<List<string>> GetWorksheetNamesAsync(string filePath)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
            
            _logger.LogInformation("ファイル {FilePath} のワークシート名: {WorksheetNames}", 
                filePath, string.Join(", ", worksheetNames));
            
            return Task.FromResult(worksheetNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ワークシート名の取得中にエラーが発生しました: {FilePath}", filePath);
            return Task.FromResult(new List<string>());
        }
    }

    /// <summary>
    /// 全ファイルのワークシート名を確認（デバッグ用）
    /// </summary>
    public async Task CheckAllWorksheetNamesAsync()
    {
        try
        {
            if (!Directory.Exists(_excelSettings.BasePath))
            {
                _logger.LogWarning("ベースパスが存在しません: {BasePath}", _excelSettings.BasePath);
                return;
            }
            
            var files = Directory.GetFiles(_excelSettings.BasePath, "*.xlsm", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                await GetWorksheetNamesAsync(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ワークシート名確認中にエラーが発生しました");
        }
    }

    /// <summary>
    /// ファイルのハッシュ値を計算
    /// </summary>
    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var performance = _performanceMonitor.MeasureOperation("CalculateFileHash");
        
        using var stream = new FileStream(
            filePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.ReadWrite,
            bufferSize: 81920); // 大きなバッファサイズを使用
            
        using var sha = SHA256.Create();
        byte[] hash = await sha.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// ファイル名から施設IDを抽出
    /// </summary>
    private int ExtractFacilityIdFromFileName(string fileName)
    {
        // 施設IDマッピング
        var facilityMapping = new Dictionary<string, int>
        {
            { "ふじみの", 7 },
            { "みさと", 10 },
            { "いちかわ", 14 }
        };
        
        foreach (var mapping in facilityMapping)
        {
            if (fileName.Contains(mapping.Key))
            {
                return mapping.Value;
            }
        }
        
        return 0; // マッピングが見つからない場合
    }

    /// <summary>
    /// 月別の予約数を抽出
    /// </summary>
    private IEnumerable<FacilityMonthlyReservation> ExtractMonthlyReservations(IXLWorksheet worksheet, int facilityId)
    {
        var results = new List<FacilityMonthlyReservation>();
        var currentYear = DateTime.Now.Year;
        
        // A列から日付を取得し、CH列から予約数を取得
        var dateReservationPairs = new List<(DateTime Date, int ReservationCount)>();
        
        var usedRows = worksheet.RowsUsed();
        foreach (var row in usedRows)
        {
            var dateCell = row.Cell("A");
            var reservationCell = row.Cell("CH");
            
            if (dateCell.TryGetValue(out DateTime date) && 
                reservationCell.TryGetValue(out int reservationCount))
            {
                dateReservationPairs.Add((date, reservationCount));
            }
        }
        
        // 月別にグループ化
        var monthlyGroups = dateReservationPairs
            .GroupBy(pair => new { pair.Date.Year, pair.Date.Month })
            .Where(group => group.Key.Year == currentYear);
        
        foreach (var group in monthlyGroups)
        {
            var year = group.Key.Year;
            var month = group.Key.Month;
            
            // その月の日数分の配列を作成
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var reservationCounts = new string[daysInMonth];
            
            // 各日の予約数を設定
            foreach (var (date, reservationCount) in group)
            {
                var dayIndex = date.Day - 1; // 0ベースのインデックス
                if (dayIndex < daysInMonth)
                {
                    reservationCounts[dayIndex] = reservationCount.ToString();
                }
            }
            
            // 未設定の日は0で埋める
            for (int i = 0; i < daysInMonth; i++)
            {
                if (string.IsNullOrEmpty(reservationCounts[i]))
                {
                    reservationCounts[i] = "0";
                }
            }
            
            var monthlyReservation = new FacilityMonthlyReservation
            {
                TenantId = 1,
                FacilityId = facilityId,
                Year = year,
                Month = month,
                ReservationCounts = reservationCounts
            };
            
            results.Add(monthlyReservation);
        }
        
        return results;
    }
    
    /// <summary>
    /// リソースの解放
    /// </summary>
    public void Dispose()
    {
        try
        {
            _logger.LogDebug("ExcelServiceのリソースクリーンアップを開始します");
            
            // ファイルロックの解放
            if (_fileLock != null)
            {
                try
                {
                    _fileLock.Dispose();
                    _logger.LogDebug("ファイルロックを解放しました");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ファイルロックの解放中にエラーが発生しました");
                }
            }
            
            // キャッシュの解放
            if (_fileStateCache != null)
            {
                try
                {
                    _fileStateCache.Dispose();
                    _logger.LogDebug("ファイル状態キャッシュを解放しました");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ファイル状態キャッシュの解放中にエラーが発生しました");
                }
            }
            
            _logger.LogDebug("ExcelServiceのリソースクリーンアップが完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExcelServiceのDispose中にエラーが発生しました");
        }
    }
} 