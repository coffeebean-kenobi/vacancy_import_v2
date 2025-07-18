using System;
using System.IO;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport;

/// <summary>
/// テストデータ作成ユーティリティ
/// </summary>
public class CreateTestData
{
    private readonly ILogger<CreateTestData> _logger;
    private readonly ExcelSettings _excelSettings;

    public CreateTestData(
        ILogger<CreateTestData> logger,
        IOptions<AppSettings> settings)
    {
        _logger = logger;
        _excelSettings = settings.Value.ExcelSettings;
    }

    /// <summary>
    /// テスト用Excelファイルを作成
    /// </summary>
    public void CreateTestExcelFile(string filePath, string storeId)
    {
        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("予約状況");

            // ヘッダーの設定
            worksheet.Cell("A1").Value = "日付";
            worksheet.Cell("B1").Value = "時間帯";
            worksheet.Cell("C1").Value = "残数";

            // テストデータの作成
            var startDate = DateTime.Today;
            for (int i = 0; i < 7; i++)
            {
                var date = startDate.AddDays(i);
                for (int j = 0; j < 3; j++)
                {
                    var row = i * 3 + j + 2;
                    worksheet.Cell($"A{row}").Value = date.ToString("yyyy/MM/dd");
                    worksheet.Cell($"B{row}").Value = $"{(j + 1)}0:00";
                    worksheet.Cell($"C{row}").Value = new Random().Next(0, 5);
                }
            }

            // ファイルの保存
            workbook.SaveAs(filePath);
            _logger.LogInformation("テスト用Excelファイルを作成しました: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "テスト用Excelファイルの作成中にエラーが発生しました");
            throw;
        }
    }
} 