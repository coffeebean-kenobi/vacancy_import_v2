# ExcelServiceの実装

以下の要件に基づいて、Excelファイル操作サービスを実装してください：

1. ファイルパス：src/VacancyImport/Services/ExcelService.cs

2. 実装要件：
   - IExcelServiceインターフェースを実装
   - コンストラクタでIOptions<AppSettings>とILogger<ExcelService>を注入
   - CheckFileUpdatesAsync: 指定されたパスのExcelファイルの更新を確認
   - ExtractMonthlyReservationsAsync: Excelファイルから月別予約データを抽出
   - SaveProofListAsync: 変更内容をCSVファイルとして保存

3. 処理内容：
   - ファイル監視はLastWriteTimeとFileHashを使用
   - データ抽出はClosedXMLを使用
   - エラーハンドリングとリトライ処理の実装
   - ログ出力の実装 