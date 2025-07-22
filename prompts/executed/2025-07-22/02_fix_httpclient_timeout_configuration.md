# 02. HTTPクライアントタイムアウト設定の修正

## 目的
Supabase接続のタイムアウト問題を解決し、Windows Service環境での適切なHTTP接続管理を実装する

## 修正内容

### 1. Program.cs のHTTPクライアント設定修正
- `HttpClient`のタイムアウト設定を追加
- 接続プールの適切な管理
- Windows Service環境での接続安定性向上

### 2. SupabaseService.cs の接続設定修正
- 接続タイムアウトの短縮
- リトライポリシーの最適化
- 接続エラー時の適切な処理

## 実装手順

1. `src/VacancyImport/Program.cs`の`ConfigureServices`メソッドでHTTPクライアント設定を追加
2. `src/VacancyImport/Services/SupabaseService.cs`の接続設定を修正
3. タイムアウト値の適切な設定（30秒以内）

## 期待される効果
- Supabase接続のタイムアウト短縮
- Windows Service終了時の接続待機時間短縮
- 接続エラー時の適切な処理 