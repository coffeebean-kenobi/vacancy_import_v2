#!/bin/bash

# カラー表示のための変数
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}Macでのテスト環境を準備しています...${NC}"

# .NET SDKのインストール確認
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}.NET SDKがインストールされていません。${NC}"
    echo -e "${YELLOW}インストール方法:${NC}"
    echo "1. homebrew経由でインストール: brew install dotnet"
    echo "2. または公式サイトからダウンロード: https://dotnet.microsoft.com/download"
    exit 1
fi

# .NET SDKのバージョン確認
DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}.NET SDKバージョン: ${DOTNET_VERSION}${NC}"

# .NET 8.0のサポート確認
DOTNET_MAJOR_VERSION=$(echo $DOTNET_VERSION | cut -d. -f1)
if [ "$DOTNET_MAJOR_VERSION" -lt "8" ]; then
    echo -e "${RED}エラー: このプロジェクトには.NET 8.0以上が必要です。${NC}"
    echo -e "${YELLOW}インストール方法:${NC}"
    echo "1. homebrew経由でインストール: brew install --cask dotnet-sdk"
    echo "2. または公式サイトからダウンロード: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# 必要なディレクトリの作成
echo -e "${GREEN}テストデータ用ディレクトリを準備しています...${NC}"
mkdir -p test_data/excel logs

# テスト用のExcelファイルを準備（サンプルがあればコピー）
if [ -f "SS予約表_サンプル.xlsm" ]; then
    echo -e "${GREEN}テスト用Excelファイルをコピーしています...${NC}"
    cp "SS予約表_サンプル.xlsm" test_data/excel/
fi

# 設定ファイルの確認
echo -e "${GREEN}設定ファイルを確認しています...${NC}"
if [ ! -f "src/VacancyImport/appsettings.Development.json" ]; then
    echo -e "${RED}警告: appsettings.Development.jsonが見つかりません${NC}"
    exit 1
fi

# ソリューションの復元
echo -e "${GREEN}NuGetパッケージを復元しています...${NC}"
dotnet restore 14_vacancy_import.sln

# メインプロジェクトのビルド
echo -e "${GREEN}メインプロジェクトをビルドしています...${NC}"
dotnet build src/VacancyImport/VacancyImport.csproj --configuration Debug

# ビルド結果の確認
if [ $? -ne 0 ]; then
    echo -e "${RED}メインプロジェクトのビルドに失敗しました。${NC}"
    exit 1
fi

# テストプロジェクトのビルド
echo -e "${GREEN}テストプロジェクトをビルドしています...${NC}"
dotnet build tests/VacancyImport.Tests/VacancyImport.Tests.csproj --configuration Debug

# ビルド結果の確認
if [ $? -ne 0 ]; then
    echo -e "${RED}テストプロジェクトのビルドに失敗しました。${NC}"
    exit 1
fi

# テストの実行
echo -e "${GREEN}単体テストを実行しています...${NC}"
dotnet test tests/VacancyImport.Tests/VacancyImport.Tests.csproj --configuration Debug

# 正常終了
echo -e "${GREEN}テスト環境の準備が完了しました！${NC}"
echo -e "${YELLOW}以下のコマンドでアプリケーションを実行できます:${NC}"
echo "dotnet run --project src/VacancyImport/VacancyImport.csproj --configuration Debug" 