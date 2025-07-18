@echo off
echo 予約管理システム連携サービス 開始
echo ==================================

REM 管理者権限チェック
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo エラー: このスクリプトは管理者権限で実行してください。
    pause
    exit /b 1
)

echo サービスを開始しています...
sc start VacancyImportService
if %errorlevel% equ 0 (
    echo サービスが正常に開始されました。
) else (
    echo エラー: サービスの開始に失敗しました。
    echo 詳細はイベントログを確認してください。
)

echo.
pause 