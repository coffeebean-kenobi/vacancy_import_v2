@echo off
echo 予約管理システム連携サービス インストール
echo =======================================

REM 管理者権限チェック
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo エラー: このスクリプトは管理者権限で実行してください。
    pause
    exit /b 1
)

REM サービス停止・削除（既存の場合）
sc query VacancyImportService >nul 2>&1
if %errorlevel% equ 0 (
    echo 既存のサービスを停止・削除しています...
    sc stop VacancyImportService
    timeout /t 5 /nobreak >nul
    sc delete VacancyImportService
    timeout /t 3 /nobreak >nul
)

REM ビルド
echo アプリケーションをビルドしています...
cd /d "%~dp0..\src\VacancyImport"
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo エラー: ビルドに失敗しました。
    pause
    exit /b 1
)

REM 公開（自己完結型実行ファイル作成）
echo サービス実行ファイルを公開しています...
dotnet publish --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true
if %errorlevel% neq 0 (
    echo エラー: 公開に失敗しました。
    pause
    exit /b 1
)

REM サービス登録
echo サービスを登録しています...
set SERVICE_PATH="%~dp0..\src\VacancyImport\bin\Release\net8.0\win-x64\publish\VacancyImport.exe"
sc create VacancyImportService binPath=%SERVICE_PATH% start=auto DisplayName="予約管理システム連携サービス"
if %errorlevel% neq 0 (
    echo エラー: サービスの登録に失敗しました。
    pause
    exit /b 1
)

REM サービス説明の設定
sc description VacancyImportService "Excelファイルから予約データを抽出してSupabaseに同期し、LINE WORKSに通知するサービス"

REM サービス開始
echo サービスを開始しています...
sc start VacancyImportService
if %errorlevel% neq 0 (
    echo 警告: サービスの開始に失敗しました。手動で開始してください。
    echo 詳細はイベントログを確認してください。
)

echo.
echo インストールが完了しました！
echo サービス管理: services.msc
echo イベントログ: eventvwr.msc （アプリケーションログ内のVacancyImportServiceを確認）
echo.
pause 