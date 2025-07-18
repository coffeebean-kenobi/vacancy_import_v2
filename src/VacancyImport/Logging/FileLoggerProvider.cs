using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VacancyImport.Logging;

/// <summary>
/// ファイルロガープロバイダー
/// </summary>
[ProviderAlias("File")]
public class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly LoggingSettings _settings;
    private readonly ConcurrentDictionary<string, CustomFileLogger> _loggers = new();
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private IExternalScopeProvider? _scopeProvider;
    private readonly Timer? _rollingTimer;
    private readonly Timer? _cleanupTimer;
    private readonly object _lockObj = new();
    private long _currentFileSize;
    private static int _currentFileNumber = 1;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public FileLoggerProvider(IOptions<LoggingSettings> settings)
    {
        _settings = settings.Value;
        _logDirectory = Path.GetDirectoryName(Path.GetFullPath(_settings.LogFilePath))
                     ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        _logFilePath = Path.GetFullPath(_settings.LogFilePath);
        
        // ディレクトリの作成
        Directory.CreateDirectory(_logDirectory);
        
        // 現在のログファイルサイズを取得
        if (File.Exists(_logFilePath))
        {
            var fileInfo = new FileInfo(_logFilePath);
            _currentFileSize = fileInfo.Length;
        }

        // ファイルサイズチェック用のタイマーを設定（10秒ごと）
        _rollingTimer = new Timer(CheckFileSize, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        
        // ログファイルのクリーンアップ用のタイマーを設定（1日ごと）
        _cleanupTimer = new Timer(CleanupOldLogs, null, TimeSpan.FromHours(1), TimeSpan.FromDays(1));
    }

    /// <summary>
    /// ロガーの取得
    /// </summary>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new CustomFileLogger(name, this));
    }

    /// <summary>
    /// スコーププロバイダーの設定
    /// </summary>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// ログファイルにメッセージを書き込む
    /// </summary>
    public void WriteLog(string categoryName, Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, string message, Exception? exception)
    {
        var logBuilder = new LogMessageBuilder()
            .WithLevel(logLevel.FromMicrosoftLogLevel())
            .WithMessage(message)
            .WithSourceContext(categoryName)
            .WithException(exception)
            .WithApplication(_settings.ApplicationName)
            .WithEnvironment(_settings.EnvironmentName);

        // スコープ情報を追加
        _scopeProvider?.ForEachScope((scope, state) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> properties)
            {
                foreach (var property in properties)
                {
                    logBuilder.WithProperty(property.Key, property.Value);
                }
            }
        }, logBuilder);

        var logMessage = logBuilder.Build();
        var logEntry = _settings.EnableStructuredLogging
            ? logMessage.ToJson()
            : logMessage.ToString();

        WriteToFile(logEntry);
    }

    /// <summary>
    /// ファイルに書き込む
    /// </summary>
    private void WriteToFile(string logEntry)
    {
        lock (_lockObj)
        {
            try
            {
                // ファイルに追記
                using var writer = new StreamWriter(_logFilePath, true);
                writer.WriteLine(logEntry);

                // ファイルサイズを更新
                _currentFileSize += System.Text.Encoding.UTF8.GetByteCount(logEntry) + Environment.NewLine.Length;
            }
            catch (Exception ex)
            {
                // ファイル書き込みエラーの場合はコンソールに出力
                Console.Error.WriteLine($"ログファイルへの書き込みに失敗しました: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ファイルサイズをチェックしてローテーション
    /// </summary>
    private void CheckFileSize(object? state)
    {
        var maxSize = _settings.FileSizeLimitMB * 1024 * 1024;
        if (_currentFileSize >= maxSize)
        {
            RollLogFile();
        }
    }

    /// <summary>
    /// ログファイルのローテーション
    /// </summary>
    private void RollLogFile()
    {
        lock (_lockObj)
        {
            try
            {
                if (!File.Exists(_logFilePath))
                {
                    return;
                }

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
                var fileExt = Path.GetExtension(_logFilePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var rolledFileName = $"{fileNameWithoutExt}_{timestamp}_{_currentFileNumber++}{fileExt}";
                var rolledFilePath = Path.Combine(_logDirectory, rolledFileName);

                // ファイルを移動
                File.Move(_logFilePath, rolledFilePath);
                _currentFileSize = 0;

                // 圧縮オプションが有効な場合は圧縮
                if (_settings.EnableCompression)
                {
                    CompressLogFile(rolledFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ログファイルのローテーションに失敗しました: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ログファイルを圧縮
    /// </summary>
    private void CompressLogFile(string filePath)
    {
        try
        {
            var compressedFilePath = $"{filePath}.gz";
            using (var originalFileStream = new FileStream(filePath, FileMode.Open))
            using (var compressedFileStream = File.Create(compressedFilePath))
            using (var compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
            {
                originalFileStream.CopyTo(compressionStream);
            }

            // 元のファイルを削除
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ログファイルの圧縮に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// 古いログファイルをクリーンアップ
    /// </summary>
    private void CleanupOldLogs(object? state)
    {
        try
        {
            var retentionDate = DateTime.Now.AddDays(-_settings.RetentionDays);
            var logFiles = Directory.GetFiles(_logDirectory, "*.log*")
                .Union(Directory.GetFiles(_logDirectory, "*.gz"))
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime < retentionDate)
                .ToList();

            foreach (var file in logFiles)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"古いログファイルの削除に失敗しました: {file.FullName} - {ex.Message}");
                }
            }

            // 最大ファイル数を超えるファイルを削除
            var allLogFiles = Directory.GetFiles(_logDirectory, "*.log*")
                .Union(Directory.GetFiles(_logDirectory, "*.gz"))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(_settings.MaxRollingFiles)
                .ToList();

            foreach (var file in allLogFiles)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"余分なログファイルの削除に失敗しました: {file.FullName} - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ログファイルのクリーンアップ中にエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// リソースの破棄
    /// </summary>
    public void Dispose()
    {
        _rollingTimer?.Dispose();
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// 空のスコープ
/// </summary>
public sealed class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new NullScope();
    private NullScope() { }
    public void Dispose() { }
} 