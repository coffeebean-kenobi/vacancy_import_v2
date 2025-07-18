# .NET 8.0準拠の確認と修正

## 目的
- プロジェクト全体が.NET 8.0に準拠していることを確認
- 必要な修正を実施

## 作業内容
1. プロジェクトファイルの確認
   - src/VacancyImport/VacancyImport.csproj
   - tests/VacancyImport.Tests/VacancyImport.Tests.csproj
   - 必要に応じて他のプロジェクトファイル

2. 依存関係の確認
   - NuGetパッケージのバージョン
   - 互換性の確認

3. コードの修正
   - 非推奨APIの使用箇所の特定と修正
   - インターフェース実装の修正
   - その他の.NET 8.0関連の修正

## 期待される結果
- すべてのプロジェクトが.NET 8.0で正常にビルドできること
- 警告やエラーが解消されていること 