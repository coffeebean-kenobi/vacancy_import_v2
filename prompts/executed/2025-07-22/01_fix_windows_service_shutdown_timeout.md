# 01. Windows Service終了タイムアウト問題の修正

## 目的
Windows Service環境での30秒タイムアウト制限に対応し、適切な終了処理を実装する

## 修正内容

### 1. ServiceHost.cs の修正
- `CancellationTokenSource`の適切な破棄処理を追加
- バックグラウンドタスクの強制終了処理を実装
- Windows Service特有の終了処理を追加

### 2. Program.cs の修正
- `HostOptions.ShutdownTimeout`を適切に設定
- グレースフルシャットダウンの実装
- 強制終了時のクリーンアップ処理を追加

## 実装手順

1. `src/VacancyImport/Services/ServiceHost.cs`の`Dispose`メソッドを修正
2. `src/VacancyImport/Program.cs`の`CreateHostBuilder`メソッドを修正
3. バックグラウンドタスクの強制終了処理を追加

## 期待される効果
- Windows Serviceの適切な終了（30秒以内）
- リソースの適切なクリーンアップ
- 強制終了時のデータ損失防止 