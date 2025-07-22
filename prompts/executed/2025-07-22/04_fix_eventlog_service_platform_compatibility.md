# 04. EventLogサービスのプラットフォーム互換性改善

## 目的
Windows以外のプラットフォーム（Mac/Linux）でのEventLogサービスエラーを解決し、クロスプラットフォーム対応を実装する

## 修正内容

### 1. EventLogService.cs のプラットフォーム対応
- `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`による条件分岐
- Windows以外での代替ログ出力実装
- プラットフォーム固有機能の適切な処理

### 2. Program.cs のサービス登録改善
- プラットフォームに応じたサービスの条件付き登録
- 代替ログサービスの実装
- エラー処理の改善

## 実装手順

1. `src/VacancyImport/Services/EventLogService.cs`にプラットフォーム判定を追加
2. `src/VacancyImport/Program.cs`のサービス登録を条件付きに変更
3. 代替ログ出力機能を実装

## 期待される効果
- クロスプラットフォーム対応
- EventLogエラーの解消
- 統一されたログ出力機能 