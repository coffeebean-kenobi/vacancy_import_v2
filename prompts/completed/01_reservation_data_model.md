# ReservationDataモデルの実装

以下の要件に基づいて、予約データを表すモデルクラスを作成してください：

1. ファイルパス：src/VacancyImport/Models/ReservationData.cs

2. 必要なプロパティ：
   - StoreId (string): 店舗ID
   - Date (DateOnly): 予約日
   - TimeSlot (string): 時間帯
   - Remain (int): 残り枠数
   - UpdatedAt (DateTime): 更新日時
   - FilePath (string): 元のExcelファイルパス
   - ChangeType (enum): 変更種別（New, Changed, Deleted）

3. 実装要件：
   - すべてのプロパティに適切なXMLドキュメントコメントを付ける
   - ChangeTypeは列挙型として別ファイルに定義
   - プロパティは読み取り専用（init）として実装
   - 等価比較のためのEqualsとGetHashCodeを実装
   - 文字列表現のためのToStringを実装 