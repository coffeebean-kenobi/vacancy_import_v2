@echo off
echo 予約管理システム連携サービス 停止
echo ==================================

REM 管理者権限チェック
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo エラー: このスクリプトは管理者権限で実行してください。
    pause
    exit /b 1
)

echo サービスを停止しています...
sc stop VacancyImportService
if %errorlevel% equ 0 (
    echo サービスが正常に停止されました。
) else (
    echo エラー: サービスの停止に失敗しました。
)

echo.
pause 