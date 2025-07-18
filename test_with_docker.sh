#!/bin/bash

# テスト環境の準備
echo "テスト環境を準備しています..."
mkdir -p test_data/excel logs

# テスト用のExcelファイルを準備
echo "テスト用Excelファイルを準備しています..."
touch test_data/excel/store001_test.xlsm

# Dockerイメージをビルド
echo "Dockerイメージをビルドしています..."
docker build -t vacancy-import:test -f Dockerfile.test .

# Dockerコンテナを実行
echo "Dockerコンテナを実行しています..."
docker run --rm -it \
  -v "$(pwd)/test_data:/app/test_data" \
  -v "$(pwd)/logs:/app/logs" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e VACANCY_IMPORT_SupabaseSettings__Url=https://example.supabase.co \
  -e VACANCY_IMPORT_SupabaseSettings__Key=test-key \
  -e VACANCY_IMPORT_SupabaseSettings__TableName=room_availability \
  vacancy-import:test

echo "テスト完了" 