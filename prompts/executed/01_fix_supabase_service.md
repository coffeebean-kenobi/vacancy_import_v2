# SupabaseServiceの修正プロンプト

## 概要
SupabaseServiceクラスのSupabase SDK呼び出し部分に関するビルドエラーを修正します。

## 修正内容
1. `From<FacilityMonthlyReservation>(_settings.TableName)` を `From<FacilityMonthlyReservation>()` に修正してください。
2. `RealtimeChannel` のリアルタイム購読部分は、現行SDKのイベント購読方法に合わせて修正してください。
3. 不要なusingや型の曖昧な参照があれば明確にしてください。

---

- 1ファイルずつ、段階的に修正を進めてください。
- 修正後は必ずビルド・テストを行い、次の修正に進んでください。 