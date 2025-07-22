# Program.csの修正プロンプト

## 概要
Program.csで発生しているビルドエラーを修正します。

## 修正内容
1. `CreateTestData` クラス/メソッドが未定義の場合は、呼び出し部分をコメントアウトしてください。
2. `GCSettings.IsServerGC` への代入は削除してください（読み取り専用のため）。
3. `AddHttpClient` の拡張メソッドが使えるように `using Microsoft.Extensions.DependencyInjection;` を確認し、必要なら追加してください。
4. `SupabaseService` の `SendReservationDataAsync` 呼び出しは、正しいメソッド名（例: `UpdateMonthlyReservationsAsync` など）に修正してください。

---

- 1つずつ修正し、ビルド・テストを行いながら進めてください。 