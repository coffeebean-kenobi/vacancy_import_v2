using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;
using VacancyImport.Models;

namespace VacancyImport.Services;

/// <summary>
/// 証跡生成（プルーフリストCSV出力）サービス
/// </summary>
public class ProofListService
{
    private readonly ILogger<ProofListService> _logger;
    private readonly AppSettings _settings;

    public ProofListService(ILogger<ProofListService> logger, IOptions<AppSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// 差分データをプルーフリストCSVとして出力
    /// </summary>
    /// <param name="changes">変更データ</param>
    /// <returns>出力ファイルパス</returns>
    public async Task<string> GenerateProofListAsync(IEnumerable<ReservationChange> changes)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{timestamp}_proof.csv";
            var outputPath = Path.Combine(_settings.ProofListSettings?.OutputDirectory ?? "proof", fileName);
            
            // ディレクトリが存在しない場合は作成
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var csvContent = new StringBuilder();
            
            // ヘッダー行
            csvContent.AppendLine("変更種別,店舗ID,日付,時間帯,変更前残数,変更後残数,更新日時");

            // データ行
            foreach (var change in changes)
            {
                csvContent.AppendLine($"{change.ChangeType},{change.StoreId},{change.Date:yyyy-MM-dd},{change.TimeSlot},{change.OldRemain},{change.NewRemain},{change.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
            }

            // UTF-8 BOMで出力
            var encoding = new UTF8Encoding(true);
            await File.WriteAllTextAsync(outputPath, csvContent.ToString(), encoding);

            _logger.LogInformation($"プルーフリストを生成しました: {outputPath}");
            
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プルーフリスト生成中にエラーが発生しました");
            throw;
        }
    }

    /// <summary>
    /// プルーフリストのサマリを生成
    /// </summary>
    /// <param name="changes">変更データ</param>
    /// <returns>サマリ文字列</returns>
    public string GenerateSummary(IEnumerable<ReservationChange> changes)
    {
        var changesList = changes.ToList();
        var newCount = changesList.Count(c => c.ChangeType == "New");
        var changedCount = changesList.Count(c => c.ChangeType == "Changed");
        var deletedCount = changesList.Count(c => c.ChangeType == "Deleted");
        var totalCount = changesList.Count;

        var summary = new StringBuilder();
        summary.AppendLine("📊 予約データ更新サマリ");
        summary.AppendLine($"🆕 新規: {newCount}件");
        summary.AppendLine($"🔄 変更: {changedCount}件");
        summary.AppendLine($"🗑️ 削除: {deletedCount}件");
        summary.AppendLine($"📈 合計: {totalCount}件");
        summary.AppendLine($"⏰ 更新日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return summary.ToString();
    }

    /// <summary>
    /// 古いプルーフリストファイルのクリーンアップ
    /// </summary>
    /// <param name="retentionDays">保持日数（デフォルト180日）</param>
    public Task CleanupOldProofListsAsync(int retentionDays = 180)
    {
        try
        {
            var proofDirectory = _settings.ProofListSettings?.OutputDirectory ?? "proof";
            
            if (!Directory.Exists(proofDirectory))
            {
                return Task.CompletedTask;
            }

            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var files = Directory.GetFiles(proofDirectory, "*_proof.csv");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    File.Delete(file);
                    _logger.LogInformation($"古いプルーフリストファイルを削除しました: {file}");
                }
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "プルーフリストのクリーンアップ中にエラーが発生しました");
            return Task.CompletedTask;
        }
    }
} 