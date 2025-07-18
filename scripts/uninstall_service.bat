@echo off
echo 予約管理システム連携サービス アンインストール
echo =========================================

REM 管理者権限チェック
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo エラー: このスクリプトは管理者権限で実行してください。
    pause
    exit /b 1
)

REM サービス停止
echo サービスを停止しています...
sc stop VacancyImportService
timeout /t 10 /nobreak >nul

REM サービス削除
echo サービスを削除しています...
sc delete VacancyImportService
if %errorlevel% neq 0 (
    echo エラー: サービスの削除に失敗しました。
    pause
    exit /b 1
)

echo.
echo アンインストールが完了しました！
echo.
pause 