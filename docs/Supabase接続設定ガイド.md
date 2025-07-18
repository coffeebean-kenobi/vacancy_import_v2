# Supabase接続設定ガイド

## 🎯 概要

このドキュメントでは、VacancyImportツールのSupabase接続設定について詳しく説明します。テスト用Supabase（本番のクローン）と本番Supabaseへの接続方法を網羅的に解説します。

## 📋 1. Supabase設定概要

### 1.1 設定ファイルの構成

VacancyImportは以下の設定ファイルでSupabase接続を管理します：

```
src/VacancyImport/
├── appsettings.json                 # 基本設定（空の値）
├── appsettings.Development.json     # 開発環境設定（ローカル）
├── appsettings.Staging.json         # ステージング環境設定（テスト用Supabase）
└── appsettings.Production.json      # 本番環境設定（本番Supabase）
```

### 1.2 設定読み込み優先順位

1. `appsettings.json` （基本設定）
2. `appsettings.{Environment}.json` （環境固有設定）
3. 環境変数 `VACANCY_IMPORT_` プレフィックス付き

上位の設定が下位の設定をオーバーライドします。

## ⚙️ 2. 設定ファイルの記載箇所と内容

### 2.1 基本設定構造

**ファイル**: `src/VacancyImport/appsettings.json`

```json
{
  "SupabaseSettings": {
    "Url": "",
    "Key": "",
    "TableName": "room_availability"
  }
}
```

### 2.2 開発環境設定（ローカルSupabase）

**ファイル**: `src/VacancyImport/appsettings.Development.json`

```json
{
  "SupabaseSettings": {
    "Url": "http://localhost:54321",
    "Key": "your-local-supabase-anon-key",
    "TableName": "reservations"
  }
}
```

### 2.3 ステージング環境設定（テスト用Supabase）

**ファイル**: `src/VacancyImport/appsettings.Staging.json`

```json
{
  "SupabaseSettings": {
    "Url": "https://your-staging-project.supabase.co",
    "Key": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", // staging anon key
    "TableName": "room_availability"
  }
}
```

### 2.4 本番環境設定（本番Supabase）

**ファイル**: `src/VacancyImport/appsettings.Production.json`

```json
{
  "SupabaseSettings": {
    "Url": "https://your-production-project.supabase.co",
    "Key": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", // production service_role key
    "TableName": "room_availability"
  }
}
```

## 🏗️ 3. Supabase環境のセットアップ

### 3.1 本番Supabaseプロジェクトの準備

1. **Supabaseプロジェクト作成**
   ```
   https://supabase.com/dashboard
   ```
   - 新しいプロジェクトを作成
   - プロジェクト名: `vacancy-import-production`
   - データベースパスワードを設定

2. **テーブル構造の作成**
   ```sql
   -- room_availability テーブル作成
   CREATE TABLE room_availability (
     id BIGSERIAL PRIMARY KEY,
     store_id VARCHAR(20) NOT NULL,
     room_name VARCHAR(100) NOT NULL,
     availability_date DATE NOT NULL,
     availability_status VARCHAR(20) NOT NULL DEFAULT 'available',
     created_at TIMESTAMPTZ DEFAULT NOW(),
     updated_at TIMESTAMPTZ DEFAULT NOW(),
     UNIQUE(store_id, room_name, availability_date)
   );

   -- インデックス作成
   CREATE INDEX idx_room_availability_date ON room_availability(availability_date);
   CREATE INDEX idx_room_availability_store ON room_availability(store_id);
   
   -- 更新時間の自動更新トリガー
   CREATE OR REPLACE FUNCTION update_updated_at_column()
   RETURNS TRIGGER AS $$
   BEGIN
     NEW.updated_at = NOW();
     RETURN NEW;
   END;
   $$ LANGUAGE plpgsql;
   
   CREATE TRIGGER update_room_availability_updated_at
     BEFORE UPDATE ON room_availability
     FOR EACH ROW
     EXECUTE FUNCTION update_updated_at_column();
   ```

3. **API キーの取得**
   - ダッシュボード → Settings → API
   - `anon public` キー（読み取り専用）
   - `service_role` キー（読み書き可能）

### 3.2 テスト用Supabase環境の構築

#### 方法1: 本番のクローンプロジェクト作成

1. **新しいプロジェクト作成**
   - プロジェクト名: `vacancy-import-staging`
   - 本番と同じテーブル構造を作成

2. **データの同期設定**
   ```sql
   -- 本番データのサンプルインポート
   COPY room_availability FROM '/path/to/sample_data.csv' DELIMITER ',' CSV HEADER;
   ```

#### 方法2: ローカルSupabaseの利用

1. **Supabase CLIのインストール**
   ```bash
   npm install -g supabase
   ```

2. **ローカル環境の起動**
   ```bash
   # プロジェクトディレクトリで実行
   supabase init
   supabase start
   ```

3. **マイグレーション適用**
   ```bash
   # 本番スキーマのマイグレーション作成
   supabase db dump --schema-only > migrations/001_initial_schema.sql
   supabase db reset
   ```

## 🔧 4. 環境別接続設定手順

### 4.1 本番環境への接続設定

1. **設定ファイルの編集**
   ```bash
   # appsettings.Production.json を編集
   notepad src\VacancyImport\appsettings.Production.json
   ```

2. **設定値の入力**
   ```json
   {
     "SupabaseSettings": {
       "Url": "https://abcdefghijklmnop.supabase.co",
       "Key": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFiY2RlZmdoaWprbG1ub3AiLCJyb2xlIjoic2VydmljZV9yb2xlIiwiaWF0IjoxNjg0ODk2NzAwLCJleHAiOjIwMDA0NzI3MDB9.example_service_role_key",
       "TableName": "room_availability"
     }
   }
   ```

3. **環境変数による設定（推奨）**
   ```cmd
   # 本番環境指定
   set ASPNETCORE_ENVIRONMENT=Production
   
   # Supabase設定（環境変数での設定）
   set VACANCY_IMPORT_SupabaseSettings__Url=https://abcdefghijklmnop.supabase.co
   set VACANCY_IMPORT_SupabaseSettings__Key=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
   set VACANCY_IMPORT_SupabaseSettings__TableName=room_availability
   ```

### 4.2 テスト環境への接続設定

1. **ステージング環境の設定**
   ```bash
   # appsettings.Staging.json を編集
   notepad src\VacancyImport\appsettings.Staging.json
   ```

2. **テスト環境指定での実行**
   ```cmd
   # ステージング環境として実行
   set ASPNETCORE_ENVIRONMENT=Staging
   VacancyImport.exe --console
   ```

### 4.3 ローカル開発環境への接続設定

1. **ローカルSupabaseの起動**
   ```bash
   supabase start
   ```

2. **開発環境での実行**
   ```cmd
   # 開発環境として実行
   set ASPNETCORE_ENVIRONMENT=Development
   VacancyImport.exe --console
   ```

## 🔒 5. セキュリティとアクセス制御

### 5.1 APIキーの管理

1. **本番環境**
   - `service_role` キーを使用（書き込み権限必要）
   - 環境変数での管理を推奨
   - 定期的なキーローテーション

2. **テスト環境**
   - `anon` キーで十分（読み取り専用テスト）
   - `service_role` キー（書き込みテスト時）

### 5.2 Row Level Security (RLS) の設定

```sql
-- RLSの有効化
ALTER TABLE room_availability ENABLE ROW LEVEL SECURITY;

-- API接続用ポリシー
CREATE POLICY "VacancyImport API access" ON room_availability
  FOR ALL USING (true)
  WITH CHECK (true);

-- 読み取り専用ポリシー（テスト用）
CREATE POLICY "Read-only access" ON room_availability
  FOR SELECT USING (true);
```

## 🧪 6. 接続テストと動作確認

### 6.1 接続テストプログラム

**ファイル**: `tests/VacancyImport.Tests/Integration/SupabaseConnectionTest.cs`

```csharp
[Fact]
public async Task TestSupabaseConnection()
{
    // 設定読み込み
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
        .AddEnvironmentVariables("VACANCY_IMPORT_")
        .Build();

    var supabaseSettings = configuration.GetSection("SupabaseSettings").Get<SupabaseSettings>();
    
    // 接続テスト
    var client = new Supabase.Client(supabaseSettings.Url, supabaseSettings.Key);
    var response = await client.From<ReservationData>().Select("*").Limit(1).Get();
    
    Assert.NotNull(response);
}
```

### 6.2 手動接続確認コマンド

```cmd
# 接続テスト実行
cd src\VacancyImport
dotnet run --console -- --test-connection
```

### 6.3 接続エラーの確認

1. **ログファイルの確認**
   ```cmd
   # エラーログの検索
   findstr "Supabase" logs\vacancy-import-*.log
   findstr "SUPABASE-" logs\vacancy-import-*.log
   ```

2. **よくあるエラーと対策**

| エラーメッセージ | 原因 | 解決方法 |
|------------------|------|----------|
| `Invalid API key` | APIキーが間違っている | ダッシュボードでキーを再確認 |
| `Project not found` | URLが間違っている | プロジェクトURLを再確認 |
| `Permission denied` | RLSポリシーで拒否 | ポリシー設定を確認 |
| `Connection timeout` | ネットワーク問題 | ファイアウォール設定確認 |

## 📊 7. データベーステーブル構造

### 7.1 room_availability テーブル

| カラム名 | データ型 | 説明 | 制約 |
|----------|----------|------|------|
| `id` | BIGSERIAL | 主キー | PRIMARY KEY |
| `store_id` | VARCHAR(20) | 店舗ID | NOT NULL |
| `room_name` | VARCHAR(100) | 部屋名 | NOT NULL |
| `availability_date` | DATE | 利用可能日 | NOT NULL |
| `availability_status` | VARCHAR(20) | 利用状況 | DEFAULT 'available' |
| `created_at` | TIMESTAMPTZ | 作成日時 | DEFAULT NOW() |
| `updated_at` | TIMESTAMPTZ | 更新日時 | DEFAULT NOW() |

### 7.2 サンプルデータ

```sql
-- サンプルデータの挿入
INSERT INTO room_availability (store_id, room_name, availability_date, availability_status) VALUES
('STORE001', 'Room A', '2024-01-15', 'available'),
('STORE001', 'Room B', '2024-01-15', 'occupied'),
('STORE002', 'Room A', '2024-01-15', 'available');
```

## 🔄 8. 環境間のデータ同期

### 8.1 本番からテストへのデータ同期

```bash
# Supabase CLIを使用したデータ同期
supabase db dump --data-only --table room_availability > staging_data.sql
```

### 8.2 定期的なテストデータ更新

```sql
-- 古いテストデータの削除
DELETE FROM room_availability WHERE availability_date < CURRENT_DATE - INTERVAL '30 days';

-- 新しいテストデータの生成
INSERT INTO room_availability (store_id, room_name, availability_date, availability_status)
SELECT 
  'TEST' || LPAD(generate_series(1,10)::text, 3, '0'),
  'Room ' || chr(65 + (generate_series(1,5) % 5)),
  CURRENT_DATE + generate_series(1,30),
  CASE WHEN random() > 0.7 THEN 'occupied' ELSE 'available' END;
```

## 📞 9. トラブルシューティング

### 9.1 デバッグログの有効化

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "VacancyImport.Services.SupabaseService": "Debug",
      "Supabase": "Debug"
    }
  }
}
```

### 9.2 接続問題の診断

1. **ネットワーク疎通確認**
   ```cmd
   # Supabaseへの接続確認
   ping abcdefghijklmnop.supabase.co
   telnet abcdefghijklmnop.supabase.co 443
   ```

2. **APIエンドポイントテスト**
   ```cmd
   # REST APIの動作確認
   curl -H "apikey: YOUR_API_KEY" https://abcdefghijklmnop.supabase.co/rest/v1/room_availability?limit=1
   ```

---

**関連ドキュメント**: 
- [Windows機セットアップ手順書](./Windows機セットアップ手順書.md)
- [設定ガイド](./configuration-guide.md)
- [運用マニュアル](./運用マニュアル.md) 