# ConfigurationException関連の修正プロンプト

## 概要
ConfigurationExceptionが見つからないエラーを修正します。

## 修正内容
1. `using VacancyImport.Exceptions;` が不足しているファイル（例: AppSettings.csなど）にusingを追加してください。
2. それでも解決しない場合は、ConfigurationExceptionクラスのパスやプロジェクト参照を確認してください。

---

- 1ファイルずつ修正し、ビルド・テストを行いながら進めてください。 