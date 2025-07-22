# FacilityMonthlyReservationモデルの実装

以下の要件に基づいて、施設月別予約データを表すモデルクラスを作成してください：

1. ファイルパス：src/VacancyImport/Models/RoomAvailability.cs

2. 必要なプロパティ：
   - TenantId (int): テナントID
   - FacilityId (int): 施設ID
   - Year (int): 年
   - Month (int): 月
   - ReservationCounts (string[]): 予約数配列
   - CreatedAt (DateTime): 作成日時
   - UpdatedAt (DateTime): 更新日時

3. 実装要件：
   - すべてのプロパティに適切なXMLドキュメントコメントを付ける
   - Supabaseテーブルマッピング用の属性を付ける
   - プロパティは読み取り専用（init）として実装
   - 等価比較のためのEqualsとGetHashCodeを実装
   - 文字列表現のためのToStringを実装 