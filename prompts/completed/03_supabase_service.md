# SupabaseServiceの実装

以下の要件に基づいて、Supabaseデータベース操作サービスを実装してください：

1. ファイルパス：src/VacancyImport/Services/SupabaseService.cs

2. 実装要件：
   - ISupabaseServiceインターフェースを実装
   - コンストラクタでIOptions<AppSettings>とILogger<SupabaseService>を注入
   - GetCurrentReservationsAsync: 現在の予約データを取得
   - UpdateReservationsAsync: 予約データの更新（追加/変更/削除）
   - StartRealtimeSubscriptionAsync: リアルタイム更新の購読開始

3. 処理内容：
   - Supabaseクライアントの初期化と設定
   - トランザクション処理の実装
   - エラーハンドリングとリトライ処理
   - ログ出力の実装 