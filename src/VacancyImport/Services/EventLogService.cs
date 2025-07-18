using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Services
{
    /// <summary>
    /// Windows イベントログサービス
    /// </summary>
    public class EventLogService
    {
        private readonly ILogger<EventLogService> _logger;
        private readonly ServiceSettings _serviceSettings;
        private EventLog? _eventLog;

        public EventLogService(ILogger<EventLogService> logger, IOptions<ServiceSettings> serviceSettings)
        {
            _logger = logger;
            _serviceSettings = serviceSettings.Value;
            InitializeEventLog();
        }

        private void InitializeEventLog()
        {
            try
            {
                const string logName = "Application";
                const string sourceName = "VacancyImportService";

                // イベントログソースの確認・作成
                if (!EventLog.SourceExists(sourceName))
                {
                    EventLog.CreateEventSource(sourceName, logName);
                    _logger.LogInformation($"イベントログソース '{sourceName}' を作成しました");
                }

                _eventLog = new EventLog(logName)
                {
                    Source = sourceName
                };

                _logger.LogInformation("イベントログサービスが初期化されました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "イベントログの初期化に失敗しました");
                // イベントログが使用できない場合でも続行
            }
        }

        /// <summary>
        /// 情報イベントを記録
        /// </summary>
        public void WriteInformation(string message, int eventId = 1000)
        {
            WriteEntry(message, EventLogEntryType.Information, eventId);
        }

        /// <summary>
        /// 警告イベントを記録
        /// </summary>
        public void WriteWarning(string message, int eventId = 2000)
        {
            WriteEntry(message, EventLogEntryType.Warning, eventId);
        }

        /// <summary>
        /// エラーイベントを記録
        /// </summary>
        public void WriteError(string message, int eventId = 3000)
        {
            WriteEntry(message, EventLogEntryType.Error, eventId);
        }

        /// <summary>
        /// エラーイベントを記録（例外付き）
        /// </summary>
        public void WriteError(string message, Exception exception, int eventId = 3000)
        {
            var fullMessage = $"{message}\n\n例外詳細:\n{exception}";
            WriteEntry(fullMessage, EventLogEntryType.Error, eventId);
        }

        /// <summary>
        /// サービス開始イベントを記録
        /// </summary>
        public void WriteServiceStart()
        {
            WriteInformation($"予約管理システム連携サービスが開始されました (Version: {GetAssemblyVersion()})", 1001);
        }

        /// <summary>
        /// サービス停止イベントを記録
        /// </summary>
        public void WriteServiceStop()
        {
            WriteInformation("予約管理システム連携サービスが停止されました", 1002);
        }

        /// <summary>
        /// 設定変更イベントを記録
        /// </summary>
        public void WriteConfigurationChange(string configName, string oldValue, string newValue)
        {
            WriteInformation($"設定が変更されました: {configName} ({oldValue} → {newValue})", 1010);
        }

        /// <summary>
        /// データ処理完了イベントを記録
        /// </summary>
        public void WriteDataProcessingComplete(int changesCount, TimeSpan processingTime)
        {
            WriteInformation($"データ処理が完了しました: 変更件数={changesCount}, 処理時間={processingTime:mm\\:ss}", 1020);
        }

        /// <summary>
        /// 連続エラーイベントを記録
        /// </summary>
        public void WriteConsecutiveErrors(int errorCount, string lastError)
        {
            WriteError($"連続エラーが発生しています: {errorCount}回\n最新エラー: {lastError}", 3010);
        }

        private void WriteEntry(string message, EventLogEntryType type, int eventId)
        {
            try
            {
                _eventLog?.WriteEntry(message, type, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"イベントログの書き込みに失敗しました: {message}");
            }
        }

        private string GetAssemblyVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public void Dispose()
        {
            _eventLog?.Dispose();
        }
    }
} 