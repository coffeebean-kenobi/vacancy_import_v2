# 予約管理システム連携サービス 運用ガイド

## 📖 概要
このドキュメントは、予約管理システム連携サービスの運用に関する包括的なガイドです。

## 🏗️ システム構成

### 主要コンポーネント
- **VacancyImportService**: Windows Service本体
- **EventLogService**: Windows イベントログ管理
- **HealthCheckService**: システムヘルスチェック
- **ProofListService**: 証跡CSV生成
- **SupabaseService**: データベース連携
- **LineWorksService**: 通知サービス

## 📦 インストール

### 前提条件
- Windows OS (Windows 10/11 または Windows Server 2019/2022)
- .NET 8.0 Runtime
- 管理者権限
- ネットワーク接続（Supabase、LINE WORKS アクセス用）

### インストール手順

1. **アプリケーションファイルの配置**
   ```cmd
   # 配置先フォルダの作成
   mkdir C:\VacancyImport
   
   # ファイルのコピー
   xcopy /E /I "ビルド済みファイル\*" C:\VacancyImport\
   ```

2. **設定ファイルの編集**
   ```cmd
   # appsettings.jsonを環境に合わせて編集
   notepad C:\VacancyImport\appsettings.json
   ```

3. **Windows Serviceの登録**
   ```cmd
   # 管理者権限のコマンドプロンプトで実行
   sc create VacancyImportService binPath="C:\VacancyImport\VacancyImport.exe"
   sc config VacancyImportService start=auto
   sc description VacancyImportService "予約管理システム連携サービス"
   ```

4. **サービスの開始**
   ```cmd
   sc start VacancyImportService
   ```

## 🔧 運用管理

### サービス管理コマンド

```cmd
# サービス状態確認
sc query VacancyImportService

# サービス開始
sc start VacancyImportService

# サービス停止
sc stop VacancyImportService

# サービス再起動
sc stop VacancyImportService && timeout /t 5 && sc start VacancyImportService

# サービス削除
sc delete VacancyImportService
```

### 設定変更

設定ファイル（`appsettings.json`）を編集後、サービスの再起動が必要：

```cmd
sc stop VacancyImportService
timeout /t 3
sc start VacancyImportService
```

### ログ確認

1. **アプリケーションログ**
   ```cmd
   # ファイルログ
   type C:\VacancyImport\logs\vacancy-import-*.log
   
   # リアルタイム監視
   powershell Get-Content C:\VacancyImport\logs\vacancy-import-*.log -Wait
   ```

2. **Windows イベントログ**
   ```cmd
   # イベントビューアーで確認
   eventvwr.msc
   
   # PowerShellで確認
   Get-EventLog -LogName Application -Source "VacancyImportService" -Newest 10
   ```

3. **プルーフリスト確認**
   ```cmd
   dir C:\VacancyImport\proof\*.csv
   ```

## 📊 監視とアラート

### ヘルスチェック
システムは1時間毎に自動ヘルスチェックを実行し、問題を検出すると：
- Windows イベントログに記録
- LINE WORKSに通知送信

### 監視項目
- ディスク容量（1GB未満で警告）
- Excelファイルパスアクセス
- Supabase接続設定
- LINE WORKS設定
- プルーフリストディレクトリアクセス

### パフォーマンス監視
- メモリ使用量
- CPU時間
- スレッド数
- 操作実行時間

## 🚨 トラブルシューティング

### よくある問題と解決方法

#### 1. サービスが開始しない
```cmd
# ログ確認
type C:\VacancyImport\logs\vacancy-import-*.log

# イベントログ確認
eventvwr.msc → アプリケーション → VacancyImportService

# 設定ファイル確認
notepad C:\VacancyImport\appsettings.json
```

**考えられる原因:**
- 設定ファイルの書式エラー
- 必要なファイルの不足
- ネットワーク接続問題

#### 2. データ処理が実行されない
```cmd
# Excel監視設定確認
type C:\VacancyImport\appsettings.json | findstr "BasePath"

# パス存在確認
dir "設定されているExcelパス"
```

#### 3. 通知が送信されない
```cmd
# LINE WORKS設定確認
type C:\VacancyImport\appsettings.json | findstr "LineWorks"

# ネットワーク接続確認
ping api.worksmobile.com
```

#### 4. ディスク容量不足
```cmd
# ディスク容量確認
dir C:\ /-c

# 古いログファイル削除
forfiles /p C:\VacancyImport\logs /s /m *.log /d -30 /c "cmd /c del @path"

# 古いプルーフリスト削除（180日以上）
forfiles /p C:\VacancyImport\proof /s /m *.csv /d -180 /c "cmd /c del @path"
```

### エラーコード一覧

| イベントID | レベル | 説明 |
|-----------|--------|------|
| 1001 | 情報 | サービス開始 |
| 1002 | 情報 | サービス停止 |
| 1010 | 情報 | 設定変更 |
| 1020 | 情報 | データ処理完了 |
| 1030 | 情報 | エラー状態から回復 |
| 2001 | 警告 | ヘルスチェック問題検出 |
| 2002 | 警告 | ヘルスチェック警告 |
| 3000 | エラー | 一般エラー |
| 3010 | エラー | 連続エラー |
| 3020 | エラー | 実行エラー |
| 3030 | エラー | 重大エラー（サービス停止） |

## 📈 パフォーマンスチューニング

### 推奨設定

1. **ポーリング間隔の調整**
   ```json
   {
     "ServiceSettings": {
       "PollingIntervalMinutes": 5
     }
   }
   ```

2. **バッチサイズの調整**
   ```json
   {
     "PerformanceSettings": {
       "DatabaseBatchSize": 100
     }
   }
   ```

3. **ログレベルの調整**
   ```json
   {
     "Serilog": {
       "MinimumLevel": {
         "Default": "Information"
       }
     }
   }
   ```

## 🔄 バックアップとリストア

### バックアップ対象
- 設定ファイル: `appsettings.json`
- 証跡ファイル: `proof/*.csv`
- ログファイル: `logs/*.log`

### バックアップスクリプト例
```cmd
@echo off
set BACKUP_DIR=backup_%date:~0,4%%date:~5,2%%date:~8,2%
mkdir %BACKUP_DIR%
xcopy /E /I C:\VacancyImport\*.json %BACKUP_DIR%\
xcopy /E /I C:\VacancyImport\proof %BACKUP_DIR%\proof\
xcopy /E /I C:\VacancyImport\logs %BACKUP_DIR%\logs\
```

## 📞 サポート情報

### ログ収集スクリプト
問題発生時は以下のスクリプトでログを収集：

```cmd
@echo off
set LOG_DIR=support_logs_%date:~0,4%%date:~5,2%%date:~8,2%
mkdir %LOG_DIR%

REM アプリケーションログ
xcopy /E /I C:\VacancyImport\logs %LOG_DIR%\app_logs\

REM イベントログ
wevtutil epl Application %LOG_DIR%\application_events.evtx /q:"*[System[Provider[@Name='VacancyImportService']]]"

REM システム情報
systeminfo > %LOG_DIR%\system_info.txt
sc query VacancyImportService > %LOG_DIR%\service_status.txt

REM 設定ファイル（機密情報を除く）
copy C:\VacancyImport\appsettings.json %LOG_DIR%\appsettings.json

echo サポートログが %LOG_DIR% に収集されました
```

## 📋 定期メンテナンス

### 週次メンテナンス
- [ ] サービス状態確認
- [ ] ログファイル確認
- [ ] ディスク容量確認

### 月次メンテナンス
- [ ] ヘルスチェック結果レビュー
- [ ] パフォーマンス統計確認
- [ ] バックアップファイル確認

### 年次メンテナンス
- [ ] .NET Runtimeアップデート
- [ ] 証明書更新（必要な場合）
- [ ] 設定見直し 