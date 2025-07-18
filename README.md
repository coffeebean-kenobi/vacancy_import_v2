# 予約管理システム連携ツール (Vacancy Import)

## 📖 概要

Excel予約管理シートからデータを抽出し、Supabaseデータベースに同期、LINE WORKSで通知を行う.NET連携ツールです。

## 🎯 現在の開発状況（フェーズ1）

### ✅ 実装完了機能

- **F-1 ファイル監視**: 5分間隔でExcelファイルの更新を監視
- **F-2 データ抽出**: CH列（夜間）データを抽出
- **F-3 差分判定**: Supabaseとの差分比較機能
- **F-4 双方向同期**: Excel→Supabase更新、Realtimeサブスクリプション
- **F-6 通知**: LINE WORKS Bot通知
- **Windows Service基盤**: ServiceBaseを継承したWindows Service実装
- **証跡生成**: プルーフリストCSV出力機能

### 🚧 進行中の作業

- **F-5 証跡生成**: プルーフリストCSV出力機能（実装済み、統合テスト中）
- **Windows Service化**: Service実装（統合テスト中）
- **本番環境対応**: 設定ファイルの環境別調整

## 🏗️ アーキテクチャ

```
┌─────────────────┐  定期実行   ┌─────────────────┐  差分抽出   ┌─────────────────┐
│ Windows Service │──────────►│ Excel監視・抽出 │──────────►│ 差分エンジン    │
└─────────────────┘            └─────────────────┘            └─────────────────┘
                                        │                              │
                                        ▼                              ▼
┌─────────────────┐  プルーフ  ┌─────────────────┐  同期      ┌─────────────────┐
│ CSV証跡出力     │◄──────────│ LINE WORKS通知  │◄──────────│ Supabaseデータ │
└─────────────────┘            └─────────────────┘            └─────────────────┘
```

## 🛠️ 技術スタック

- **.NET 8.0**: メインフレームワーク
- **Windows Service**: BackgroundServiceとIHostedLifecycleServiceを活用したバックグラウンド実行
- **Microsoft.Extensions.Hosting.WindowsServices 9.0.5**: 現代的なWindows Service実装
- **ClosedXML 0.105.0**: Excel読み取り（最新版）
- **Supabase .NET SDK**: データベース同期
- **Serilog 4.3.0**: 構造化ログ記録（最新版）
- **xUnit**: テストフレームワーク

### Windows Service機能
- **並行サービス開始**: .NET 8.0の新機能で高速起動
- **詳細ライフサイクル制御**: IHostedLifecycleServiceによる細かい制御
- **適切な例外処理**: BackgroundServiceExceptionBehavior.StopHostによる安全な再起動

## 📦 セットアップ手順

### 1. 前提条件

- Windows OS (Windows 10/11 または Windows Server)
- .NET 8.0 Runtime
- 管理者権限（Windows Service登録のため）

### 2. 設定ファイル

`src/VacancyImport/appsettings.json`を環境に合わせて設定：

```json
{
  "SupabaseSettings": {
    "Url": "YOUR_SUPABASE_URL",
    "Key": "YOUR_SUPABASE_KEY"
  },
  "LineWorksSettings": {
    "BotId": "YOUR_BOT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  },
  "ExcelSettings": {
    "Environments": {
      "Production": {
        "BasePath": "\\\\192.168.200.20\\全社共有\\SS予約表\\2025年"
      }
    }
  }
}
```

### 3. ビルドと実行

```bash
# ビルド
dotnet build

# 開発環境でテスト実行
cd src/VacancyImport
dotnet run --configuration Debug

# Windows Serviceとしてインストール（管理者権限必要）
sc create VacancyImportService binPath="C:\path\to\VacancyImport.exe"
sc start VacancyImportService
```

## 🧪 テスト実行

```bash
# テスト環境セットアップ（Mac）
./setup_mac_test_env.sh

# 単体テスト実行
dotnet test

# Dockerテスト
./test_with_docker.sh
```

## 📊 監視機能

### ログ出力先
- **コンソール**: 開発環境での即座の確認
- **ファイル**: `logs/vacancy-import-YYYY-MM-DD.log`
- **Windows イベントログ**: Service実行時

### プルーフリスト出力
- **場所**: `./proof/YYYYMMDD_HHmmss_proof.csv`
- **自動クリーンアップ**: 180日後に自動削除
- **内容**: 変更種別、店舗ID、日付、時間帯、変更前後残数

## 🔧 運用コマンド

```bash
# サービス状態確認
sc query VacancyImportService

# サービス停止
sc stop VacancyImportService

# サービス削除
sc delete VacancyImportService

# ログ確認
tail -f logs/vacancy-import-*.log
```

## 📋 次期フェーズ（フェーズ2）計画

- **完全クラウド化**: Excel廃止、Web UI実装
- **リアルタイム双方向同期**: Supabase Realtimeフル活用
- **OCI展開**: クラウドネイティブ化
- **CI/CD パイプライン**: GitHub Actions統合

## 🤝 開発チーム

開発中のため、質問や課題は開発チームまでお知らせください。

## 📚 関連ドキュメント

- [要件定義書](要件定義.MD)
- [.NET Framework公式ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/framework/)
- [Windows Service開発ガイド](https://learn.microsoft.com/ja-jp/dotnet/framework/windows-services/)
