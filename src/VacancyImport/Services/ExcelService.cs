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
    /// ファイルの更新を確認
    /// </summary>
    public async Task<bool> CheckFileUpdatesAsync()
    {
        return await CheckFileUpdatesAsync(string.Empty);
    }

    /// <summary>
    /// 予約データを抽出
    /// </summary>
    public async Task<IEnumerable<ReservationData>> ExtractReservationDataAsync()
    {
        return await ExtractReservationDataAsync(string.Empty);
    }

    /// <summary>
    /// ファイルの更新を確認
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
                    
                    // 並列処理で複数ファイルのハッシュを計算
                    await ParallelProcessor.ProcessParallelAsync(
                        files, 
                        async (file, ct) => 
                        {
                            var fileInfo = new FileInfo(file);
                            
                            if (!fileInfo.Exists)
                            {
                                _logger.LogWarning("ファイルが存在しません: {FilePath}", file);
                                return false;
                            }
                            
                            // キャッシュから前回の状態を取得
                            if (_fileStateCache.TryGet(file, out var cachedState))
                            {
                                // 最終更新日時が同じならハッシュ計算をスキップ
                                if (cachedState.LastWriteTime == fileInfo.LastWriteTime)
                                {
                                    return false;
                                }
                            }
                            
                            // ファイルのハッシュを計算
                            var currentHash = await CalculateFileHashAsync(file);
                            
                            if (!_fileStateCache.TryGet(file, out var state) ||
                                state.LastWriteTime != fileInfo.LastWriteTime ||
                                state.Hash != currentHash)
                            {
                                _fileStateCache.Set(file, (fileInfo.LastWriteTime, currentHash));
                                _logger.LogInformation("ファイルが更新されました: {FilePath}", file);
                                return true;
                            }
                            
                            return false;
                        }, 
                        maxDegreeOfParallelism: 4,
                        logger: _logger,
                        operationName: "ファイル変更チェック")
                        .ContinueWith(t => t.Result.Any(updated => updated));
                    
                    return hasUpdates;
                }
                catch (IOException ex)
                {
                    throw ExcelFileException.ReadError(filePath, ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw ExcelFileException.ReadError(filePath, ex);
                }
                catch (Exception ex) when (!(ex is ExcelFileException))
                {
                    throw new ExcelFileException(
                        $"ファイル更新チェック中にエラーが発生しました: {filePath}", 
                        filePath, 
                        "EXCEL-UPDATE-CHECK-ERR", 
                        ErrorSeverity.Error, 
                        true, 
                        ex);
                }
            },
            _settings.Value.ExcelSettings.RetryCount,
            200,
            5000,
            _logger);
    }

    /// <summary>
    /// 予約データを抽出
    /// </summary>
    public async Task<IEnumerable<ReservationData>> ExtractReservationDataAsync(string filePath)
    {
        using var performance = _performanceMonitor.MeasureOperation("ExtractReservationData");
        
        return await RetryPolicy.ExecuteWithRetryAsync(
            async () =>
            {
                try
                {
                    var fullPath = Path.Combine(_excelSettings.BasePath, filePath);
                    var allResults = new List<ReservationData>();
                    
                    if (!Directory.Exists(_excelSettings.BasePath))
                    {
                        throw ExcelFileException.FileNotFound(_excelSettings.BasePath);
                    }
                    
                    var files = Directory.GetFiles(_excelSettings.BasePath, "*.xlsm", SearchOption.AllDirectories);
                    
                    // 並列処理で複数ファイルのデータを抽出
                    var results = await ParallelProcessor.ProcessParallelAsync(
                        files,
                        async (file, ct) =>
                        {
                            // ファイルごとの処理結果
                            var fileResults = new List<ReservationData>();
                            
                            if (!File.Exists(file))
                            {
                                _logger.LogWarning("ファイルが存在しません: {FilePath}", file);
                                return fileResults;
                            }
                            
                            // ファイルアクセスのロックを取得
                            await _fileLock.WaitAsync(ct);
                            
                            try
                            {
                                using var performance = _performanceMonitor.MeasureOperation($"ProcessExcelFile_{Path.GetFileName(file)}");
                                using var workbook = new XLWorkbook(file);
                                var worksheet = workbook.Worksheet(_excelSettings.SheetName);

                                if (worksheet == null)
                                {
                                    _logger.LogWarning("ワークシートが見つかりません: {SheetName} in {FilePath}", _excelSettings.SheetName, file);
                                    return fileResults;
                                }

                                var storeId = Path.GetFileNameWithoutExtension(file).Split('_')[0];
                                var today = DateOnly.FromDateTime(DateTime.Today);
                                var endDate = today.AddDays(60);

                                // 日付行を取得してバッファに格納
                                var rowBuffer = new List<(DateTime Date, string TimeSlot, int Remain)>();
                                
                                var dateRow = worksheet.FirstRowUsed();
                                while (dateRow != null)
                                {
                                    var dateCell = dateRow.Cell(_excelSettings.ColumnIndex);
                                    if (dateCell.TryGetValue(out DateTime date) && date >= today.ToDateTime(TimeOnly.MinValue) && date <= endDate.ToDateTime(TimeOnly.MaxValue))
                                    {
                                        var timeSlot = dateCell.GetValue<string>();
                                        var remain = dateCell.CellRight().GetValue<int>();
                                        
                                        rowBuffer.Add((date, timeSlot, remain));
                                    }
                                    dateRow = dateRow.RowBelow();
                                }
                                
                                // バッファからReservationDataオブジェクトを生成
                                foreach (var (date, timeSlot, remain) in rowBuffer)
                                {
                                    var reservation = new ReservationData
                                    {
                                        StoreId = storeId,
                                        Date = DateOnly.FromDateTime(date),
                                        TimeSlot = timeSlot,
                                        Remain = remain,
                                        UpdatedAt = DateTime.Now,
                                        FilePath = file,
                                        ChangeType = ChangeType.New
                                    };
                                    
                                    fileResults.Add(reservation);
                                }
                            }
                            catch (IOException ex)
                            {
                                throw ExcelFileException.ReadError(file, ex);
                            }
                            catch (Exception ex) when (!(ex is ExcelFileException))
                            {
                                throw ExcelFileException.DataFormatError(file, ex.Message, ex);
                            }
                            finally
                            {
                                _fileLock.Release();
                            }
                            
                            return fileResults;
                        },
                        maxDegreeOfParallelism: 2, // Excelファイルは同時アクセス数を制限
                        logger: _logger,
                        operationName: "Excelファイル処理");
                    
                    // 並列処理の結果を結合
                    foreach (var result in results)
                    {
                        allResults.AddRange(result);
                    }

                    return allResults;
                }
                catch (Exception ex) when (!(ex is ExcelFileException))
                {
                    throw new ExcelFileException(
                        $"予約データの抽出中にエラーが発生しました: {filePath}", 
                        filePath, 
                        "EXCEL-EXTRACT-ERR", 
                        ErrorSeverity.Error, 
                        true, 
                        ex);
                }
            },
            _settings.Value.ExcelSettings.RetryCount,
            200,
            5000,
            _logger);
    }

    /// <summary>
    /// プルーフリストを保存
    /// </summary>
    public async Task SaveProofListAsync(IEnumerable<ReservationData> changes)
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
                        change => $"{change.StoreId},{change.Date:yyyy/MM/dd},{change.TimeSlot},{change.Remain},{change.ChangeType},{change.FilePath}",
                        header: "StoreId,Date,TimeSlot,Remain,ChangeType,FilePath",
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
} 