@echo off
echo 予約管理システム連携サービス デバッグモード
echo ==========================================

echo コンソールアプリケーションとして実行します...
cd /d "%~dp0..\src\VacancyImport"

REM 開発環境設定
set ASPNETCORE_ENVIRONMENT=Development

dotnet run --configuration Debug
pause 