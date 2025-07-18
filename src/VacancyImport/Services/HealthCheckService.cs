using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Services
{
    /// <summary>
    /// システムヘルスチェックサービス
    /// </summary>
    public class HealthCheckService
    {
        private readonly ILogger<HealthCheckService> _logger;
        private readonly AppSettings _settings;
        private readonly EventLogService _eventLogService;

        public HealthCheckService(
            ILogger<HealthCheckService> logger, 
            IOptions<AppSettings> settings,
            EventLogService eventLogService)
        {
            _logger = logger;
            _settings = settings.Value;
            _eventLogService = eventLogService;
        }

        /// <summary>
        /// 総合ヘルスチェック実行
        /// </summary>
        public async Task<HealthCheckResult> PerformHealthCheckAsync()
        {
            var result = new HealthCheckResult();
            var checks = new List<Task<bool>>
            {
                CheckDiskSpaceAsync(result),
                CheckExcelPathAccessAsync(result),
                CheckSupabaseConnectionAsync(result),
                CheckLineWorksConfigurationAsync(result),
                CheckProofListDirectoryAsync(result)
            };

            try
            {
                await Task.WhenAll(checks);
                
                result.IsHealthy = result.FailedChecks.Count == 0;
                result.CheckedAt = DateTime.Now;

                if (!result.IsHealthy)
                {
                    _eventLogService.WriteWarning($"ヘルスチェックで問題が検出されました: {string.Join(", ", result.FailedChecks)}", 2001);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ヘルスチェック実行中にエラーが発生しました");
                result.IsHealthy = false;
                result.FailedChecks.Add("HealthCheck execution failed");
                return result;
            }
        }

        private async Task<bool> CheckDiskSpaceAsync(HealthCheckResult result)
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "C:\\");
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024);
                
                if (freeSpaceGB < 1) // 1GB未満の場合は警告
                {
                    result.FailedChecks.Add($"低ディスク容量: {freeSpaceGB:F1}GB");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ディスク容量チェックに失敗しました");
                result.FailedChecks.Add("ディスク容量チェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckExcelPathAccessAsync(HealthCheckResult result)
        {
            try
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
                var excelPath = _settings.ExcelSettings.GetEnvironmentSettings(environment).BasePath;
                
                if (!Directory.Exists(excelPath))
                {
                    result.FailedChecks.Add($"Excelパスにアクセスできません: {excelPath}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excelパスアクセスチェックに失敗しました");
                result.FailedChecks.Add("Excelパスアクセスチェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckSupabaseConnectionAsync(HealthCheckResult result)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.SupabaseSettings.Url) || 
                    string.IsNullOrEmpty(_settings.SupabaseSettings.Key))
                {
                    result.FailedChecks.Add("Supabase設定が不完全です");
                    return false;
                }
                
                // 簡易接続チェック（実際のAPI呼び出しは行わない）
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase接続チェックに失敗しました");
                result.FailedChecks.Add("Supabase接続チェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckLineWorksConfigurationAsync(HealthCheckResult result)
        {
            try
            {
                var lineWorksSettings = _settings.LineWorksSettings;
                if (string.IsNullOrEmpty(lineWorksSettings.BotId) ||
                    string.IsNullOrEmpty(lineWorksSettings.ClientId) ||
                    string.IsNullOrEmpty(lineWorksSettings.ClientSecret))
                {
                    result.FailedChecks.Add("LINE WORKS設定が不完全です");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LINE WORKS設定チェックに失敗しました");
                result.FailedChecks.Add("LINE WORKS設定チェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckProofListDirectoryAsync(HealthCheckResult result)
        {
            try
            {
                var proofDirectory = _settings.ProofListSettings?.OutputDirectory ?? "./proof";
                
                if (!Directory.Exists(proofDirectory))
                {
                    Directory.CreateDirectory(proofDirectory);
                }
                
                // 書き込み権限テスト
                var testFile = Path.Combine(proofDirectory, "healthcheck.tmp");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "プルーフリストディレクトリチェックに失敗しました");
                result.FailedChecks.Add("プルーフリストディレクトリアクセス失敗");
                return false;
            }
        }
    }

    /// <summary>
    /// ヘルスチェック結果
    /// </summary>
    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; } = true;
        public List<string> FailedChecks { get; set; } = new List<string>();
        public DateTime CheckedAt { get; set; }
        
        public string GetSummary()
        {
            if (IsHealthy)
            {
                return "✅ すべてのヘルスチェックが正常です";
            }
            
            return $"⚠️ ヘルスチェックで {FailedChecks.Count} 件の問題が検出されました:\n" +
                   string.Join("\n", FailedChecks.Select(f => $"- {f}"));
        }
    }
} 