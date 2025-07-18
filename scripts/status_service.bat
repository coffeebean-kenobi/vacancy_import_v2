@echo off
echo 予約管理システム連携サービス 状態確認
echo ====================================

echo サービス状態:
sc query VacancyImportService

echo.
echo サービス設定:
sc qc VacancyImportService

echo.
echo イベントログを確認する場合は eventvwr.msc を実行してください
echo （アプリケーションログ内のVacancyImportServiceソース）
echo.
pause 