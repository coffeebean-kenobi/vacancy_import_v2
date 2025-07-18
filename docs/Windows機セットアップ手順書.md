# Windows機セットアップ手順書

## 🎯 概要

このドキュメントでは、予約管理システム連携ツール（VacancyImport）をWindows機にセットアップする手順を詳しく説明します。

## 📋 1. 事前要件

### 1.1 ハードウェア要件

- **CPU**: Intel Core i5 相当以上（推奨：Intel Core i7）
- **メモリ**: 8GB以上（推奨：16GB）
- **ストレージ**: 空き容量10GB以上のSSD（推奨：20GB以上）
- **ネットワーク**: 社内LAN接続（有線推奨）+ インターネット接続

### 1.2 ソフトウェア要件

- **OS**: Windows 10 Pro/Enterprise (64bit) / Windows 11 Pro (64bit)
- **.NET Runtime**: .NET 8.0 Runtime (推奨：最新版)
- **Excel**: Microsoft Excel 2016以降（予約表読み込み用）
- **権限**: 管理者権限を持つユーザーアカウント

### 1.3 ネットワーク要件

- **内部アクセス**: `\\192.168.200.20\全社共有\SS予約表\` へのアクセス権限
- **外部アクセス**: 
  - HTTPS (TCP/443): Supabase API通信用
  - HTTPS (TCP/443): LINE WORKS API通信用

## 🚀 2. インストール手順

### 2.1 .NET 8.0 Runtimeのインストール

1. **ダウンロード**
   ```
   https://dotnet.microsoft.com/download/dotnet/8.0
   ```
   - 「Run desktop apps」セクションの「Download x64」を選択

2. **インストール実行**
   ```cmd
   # ダウンロードしたファイルを実行
   windowsdesktop-runtime-8.0.x-win-x64.exe
   ```

3. **インストール確認**
   ```cmd
   # PowerShellまたはコマンドプロンプトで実行
   dotnet --info
   ```
   - 出力に`.NET 8.0.x`が含まれることを確認

### 2.2 リポジトリのクローンとビルド

1. **Gitクローン**
   ```cmd
   # 作業ディレクトリに移動
   cd C:\dev
   
   # リポジトリをクローン
   git clone [リポジトリURL] vacancy-import
   cd vacancy-import
   ```

2. **プロジェクトのビルド**
   ```cmd
   # VacancyImportプロジェクトのディレクトリに移動
   cd src\VacancyImport
   
   # Releaseビルド実行
   dotnet build --configuration Release
   
   # 成功確認
   echo %ERRORLEVEL%
   # 0が表示されればビルド成功
   ```

### 2.3 設定ファイルの準備

1. **環境設定ファイルの作成**
   ```cmd
   # プロジェクトディレクトリ内で実行
   copy appsettings.json appsettings.Production.json
   ```

2. **後述のSupabase設定セクション参照**

## ⚙️ 3. サービスとしてのインストール

### 3.1 Windowsサービス用設定

1. **発行（Publish）の実行**
   ```cmd
   # VacancyImportプロジェクトディレクトリで実行
   dotnet publish --configuration Release --self-contained false --output "C:\VacancyImport"
   ```

2. **サービス登録**
   ```cmd
   # 管理者権限でPowerShellを開く
   # サービス作成
   sc create VacancyImportService binpath= "C:\VacancyImport\VacancyImport.exe" start= auto
   
   # サービス詳細設定
   sc config VacancyImportService DisplayName= "予約管理システム連携サービス"
   sc config VacancyImportService description= "Excel予約管理シートからデータを抽出し、Supabaseデータベースに同期、LINE WORKSで通知を行うサービス"
   ```

### 3.2 サービスの開始

```cmd
# サービス開始
sc start VacancyImportService

# サービス状態確認
sc query VacancyImportService
```

## 🏃‍♂️ 4. 手動実行での動作確認

### 4.1 コンソールモードでの実行

```cmd
# VacancyImportディレクトリに移動
cd C:\VacancyImport

# 環境変数設定
set ASPNETCORE_ENVIRONMENT=Production

# コンソールモードで実行
VacancyImport.exe --console
```

### 4.2 実行ログの確認

```cmd
# ログディレクトリの確認
dir C:\VacancyImport\logs

# 最新ログファイルの表示
type C:\VacancyImport\logs\vacancy-import-*.log
```

## 📁 5. フォルダ構成

インストール後の推奨フォルダ構成：

```
C:\VacancyImport\                    # アプリケーションルート
├── VacancyImport.exe               # メインプログラム
├── appsettings.json                # 基本設定
├── appsettings.Production.json     # 本番環境設定
├── logs\                           # ログファイル格納
│   └── vacancy-import-*.log
├── proof\                          # 証跡ファイル格納
│   └── proof-list-*.csv
└── test_data\                      # テスト用データ（開発時のみ）
```

## 🔧 6. Windowsタスクスケジューラ設定（任意）

### 6.1 定期実行タスクの作成

1. **タスクスケジューラを開く**
   ```cmd
   taskschd.msc
   ```

2. **基本タスクの作成**
   - タスク名：`VacancyImport定期実行`
   - 説明：`予約データの定期同期処理`

3. **トリガー設定**
   - 開始：毎日
   - 時刻：10:00、18:00（2つのトリガーを作成）

4. **操作設定**
   - プログラム：`C:\VacancyImport\VacancyImport.exe`
   - 引数：`--console`
   - 開始場所：`C:\VacancyImport`

## 🩺 7. 動作確認とトラブルシューティング

### 7.1 基本動作確認

1. **サービス状態確認**
   ```cmd
   # サービス一覧確認
   sc query type= service state= all | findstr "VacancyImport"
   
   # イベントログ確認
   eventvwr.msc
   ```

2. **ログファイル確認**
   ```cmd
   # エラーログ検索
   findstr "ERROR" C:\VacancyImport\logs\vacancy-import-*.log
   findstr "WARN" C:\VacancyImport\logs\vacancy-import-*.log
   ```

### 7.2 よくある問題と解決方法

| 問題 | 原因 | 解決方法 |
|------|------|----------|
| `.NET Runtime not found` | .NET 8.0がインストールされていない | 2.1の手順で.NET 8.0をインストール |
| `Access denied to network path` | ネットワーク共有へのアクセス権限がない | IT部門にアクセス権限申請 |
| `Supabase connection failed` | Supabase設定が間違っている | 設定ファイルのSupabase設定を確認 |
| `LINE WORKS API error` | LINE WORKS認証情報が間違っている | 認証情報を再確認 |
| `Service failed to start` | 設定ファイルまたは権限の問題 | ログファイルの詳細エラー確認 |

### 7.3 デバッグ方法

1. **詳細ログの有効化**
   ```json
   // appsettings.Production.json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "VacancyImport": "Debug"
       }
     }
   }
   ```

2. **コンソールモードでのデバッグ実行**
   ```cmd
   # 詳細出力でコンソール実行
   set ASPNETCORE_ENVIRONMENT=Production
   VacancyImport.exe --console --verbose
   ```

## 📞 8. サポート情報

### 8.1 ログファイル保存場所

- **アプリケーションログ**: `C:\VacancyImport\logs\`
- **Windowsイベントログ**: アプリケーションログ → ソース「VacancyImportService」
- **証跡ファイル**: `C:\VacancyImport\proof\`

### 8.2 緊急時の対応

1. **サービス停止**
   ```cmd
   sc stop VacancyImportService
   ```

2. **サービス削除**（再インストール時）
   ```cmd
   sc delete VacancyImportService
   ```

3. **設定リセット**
   ```cmd
   # 設定ファイルのバックアップ
   copy appsettings.Production.json appsettings.Production.json.bak
   
   # 工場出荷時設定の復元
   copy appsettings.json appsettings.Production.json
   ```

---

**次のセクション**: [Supabase接続設定ガイド](./Supabase接続設定ガイド.md) 