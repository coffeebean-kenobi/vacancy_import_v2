@echo off
setlocal enabledelayedexpansion

echo ================================================================
echo Windows Service統合テスト
echo ================================================================
echo.

REM 管理者権限確認
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ このスクリプトは管理者権限で実行してください
    echo    右クリックして「管理者として実行」を選択してください
    pause
    exit /b 1
)

set "SERVICE_NAME=VacancyImportService"
set "SERVICE_DISPLAY_NAME=予約管理システム連携サービス"
set "BINARY_PATH=%~dp0..\src\VacancyImport\bin\Release\net8.0\VacancyImport.exe"

echo [1/6] ビルド（Release）実行中...
cd /d "%~dp0..\src\VacancyImport"
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo ❌ ビルドに失敗しました
    goto :cleanup
)
echo ✅ ビルド成功

echo [2/6] 既存サービスの確認と削除...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo 既存のサービスが見つかりました。停止・削除しています...
    sc stop "%SERVICE_NAME%" >nul 2>&1
    timeout /t 3 >nul
    sc delete "%SERVICE_NAME%" >nul 2>&1
    timeout /t 2 >nul
)
echo ✅ サービス削除完了

echo [3/6] サービスのインストール...
sc create "%SERVICE_NAME%" binPath="%BINARY_PATH%" DisplayName="%SERVICE_DISPLAY_NAME%" start=demand
if %errorlevel% neq 0 (
    echo ❌ サービスのインストールに失敗しました
    goto :cleanup
)
echo ✅ サービスインストール成功

echo [4/6] サービスの開始テスト...
sc start "%SERVICE_NAME%"
if %errorlevel% neq 0 (
    echo ❌ サービスの開始に失敗しました
    goto :cleanup
)
echo ✅ サービス開始成功

echo [5/6] サービス状態確認（10秒待機）...
timeout /t 10 >nul
sc query "%SERVICE_NAME%"
echo ✅ サービス状態確認完了

echo [6/6] サービスの停止...
sc stop "%SERVICE_NAME%"
if %errorlevel% neq 0 (
    echo ⚠️ サービスの停止でエラーが発生しました
) else (
    echo ✅ サービス停止成功
)

:cleanup
echo.
echo クリーンアップ中...
sc delete "%SERVICE_NAME%" >nul 2>&1
echo ✅ テスト用サービスを削除しました

echo.
echo ================================================================
echo Windows Service統合テスト完了
echo ================================================================
pause 