# 設定値の調整

以下の要件に基づいて、設定値の調整を実施してください：

1. 環境別設定ファイル：
   - ファイルパス：src/VacancyImport/Configuration/
   - appsettings.Development.json
   - appsettings.Staging.json
   - appsettings.Production.json
   - 環境変数の設定

2. 設定値の検証：
   - 必須項目のチェック
   - 値の範囲チェック
   - 型の検証
   - 依存関係の検証

3. 設定の動的更新：
   - 設定変更の検知
   - 実行時の更新
   - 更新通知
   - 更新履歴の記録

4. セキュリティ設定：
   - 機密情報の管理
   - アクセス制御
   - 暗号化設定
   - 監査ログ 