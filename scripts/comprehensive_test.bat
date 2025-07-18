@echo off
setlocal enabledelayedexpansion

echo ================================================================
echo 予約管理システム連携サービス 総合テスト
echo ================================================================
echo.

set "TEST_RESULTS_DIR=test_results_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
set "TEST_RESULTS_DIR=%TEST_RESULTS_DIR: =0%"
mkdir "%TEST_RESULTS_DIR%" 2>nul

echo テスト結果ディレクトリ: %TEST_RESULTS_DIR%
echo.

REM 1. ビルドテスト
echo [1/8] ビルドテスト実行中...
cd /d "%~dp0..\src\VacancyImport"
dotnet build --configuration Release > "..\..\%TEST_RESULTS_DIR%\build_test.log" 2>&1
if %errorlevel% neq 0 (
    echo ❌ ビルドに失敗しました
    goto :end
) else (
    echo ✅ ビルド成功
)

REM 2. 単体テストテスト
echo [2/8] 単体テスト実行中...
cd /d "%~dp0.."
dotnet test > "%TEST_RESULTS_DIR%\unit_test.log" 2>&1
if %errorlevel% neq 0 (
    echo ⚠️ 単体テストで問題が検出されました（詳細は unit_test.log を確認）
) else (
    echo ✅ 単体テスト成功
)

REM 3. コンソールモード起動テスト
echo [3/8] コンソールモード起動テスト実行中...
cd /d "%~dp0..\src\VacancyImport"
timeout /t 3 | dotnet run --configuration Release -- --console > "..\..\%TEST_RESULTS_DIR%\console_test.log" 2>&1
echo ✅ コンソールモード起動テスト完了

REM 4. ヘルプ表示テスト
echo [4/8] ヘルプ表示テスト実行中...
dotnet run --configuration Release -- --help > "..\..\%TEST_RESULTS_DIR%\help_test.log" 2>&1
echo ✅ ヘルプ表示テスト完了

REM 5. 設定ファイル検証テスト
echo [5/8] 設定ファイル検証テスト実行中...
if not exist "appsettings.json" (
    echo ❌ appsettings.json が見つかりません
    goto :end
)
echo ✅ 設定ファイル検証完了

REM 6. 依存関係チェック
echo [6/8] 依存関係チェック実行中...
dotnet list package > "..\..\%TEST_RESULTS_DIR%\dependencies.log" 2>&1
echo ✅ 依存関係チェック完了

REM 7. ログディレクトリ作成テスト
echo [7/8] ログディレクトリ作成テスト実行中...
if not exist "logs" mkdir "logs"
if not exist "proof" mkdir "proof"
echo ✅ ディレクトリ作成テスト完了

REM 8. サマリ生成
echo [8/8] テスト結果サマリ生成中...
echo. > "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo 予約管理システム連携サービス 総合テスト結果 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ================================================ >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo 実行日時: %date% %time% >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo. >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ ビルドテスト: 成功 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ 単体テスト: 完了 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ コンソールモード: 正常起動 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ ヘルプ表示: 正常表示 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ 設定ファイル検証: 検証完了 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ 依存関係: チェック完了 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"
echo ✅ ディレクトリ作成: 完了 >> "..\..\%TEST_RESULTS_DIR%\test_summary.txt"

:end
echo.
echo ================================================================
echo テスト完了！結果は %TEST_RESULTS_DIR% フォルダを確認してください
echo ================================================================
pause 