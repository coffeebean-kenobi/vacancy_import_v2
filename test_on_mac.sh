#!/bin/bash

# エラー発生時に即座に終了
set -e

# ログ出力用の関数
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# エラー出力用の関数
error() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] ERROR: $1" >&2
}

# 環境変数の設定
export ASPNETCORE_ENVIRONMENT=Development
export VACANCY_IMPORT_LOG_LEVEL=Debug
export VACANCY_IMPORT_EXCEL_BASE_PATH="./test_data/excel"
export VACANCY_IMPORT_LOG_PATH="./logs"

# テスト環境の準備
log "テスト環境を準備しています..."
mkdir -p test_data/excel logs

# 権限の設定
log "ディレクトリの権限を設定しています..."
chmod 755 test_data/excel logs

# テスト用のExcelファイルを準備
log "テスト用Excelファイルを準備しています..."
if [ ! -f "test_data/excel/store001_test.xlsm" ]; then
    touch test_data/excel/store001_test.xlsm
    chmod 644 test_data/excel/store001_test.xlsm
fi

# 設定ファイルの確認
log "設定ファイルを確認しています..."
if [ ! -f "src/VacancyImport/appsettings.Development.json" ]; then
    error "appsettings.Development.jsonが見つかりません"
    exit 1
fi

# .NET SDKのバージョンを確認
log ".NET SDKのバージョンを確認しています..."
if ! command -v dotnet &> /dev/null; then
    error ".NET SDKがインストールされていません"
    exit 1
fi

dotnet_version=$(dotnet --version)
log "使用する.NET SDKバージョン: $dotnet_version"

# プロジェクトのビルド
log "プロジェクトをビルドしています..."
cd src/VacancyImport
if ! dotnet build --configuration Debug; then
    error "ビルドに失敗しました"
    exit 1
fi

# テスト実行
log "テストを実行しています..."
if ! dotnet run --configuration Debug --environment Development; then
    error "テストの実行に失敗しました"
    exit 1
fi

log "テスト完了" 