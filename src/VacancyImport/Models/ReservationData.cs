using System;
using System.Collections.Generic;
using Postgrest.Models;
using Postgrest.Attributes;

namespace VacancyImport.Models;

/// <summary>
/// 予約データを表すクラス
/// </summary>
[Table("reservations")]
public class ReservationData : BaseModel
{
    /// <summary>
    /// 店舗ID
    /// </summary>
    [Column("store_id")]
    public string StoreId { get; init; } = string.Empty;

    /// <summary>
    /// 予約日
    /// </summary>
    [Column("date")]
    public DateOnly Date { get; init; }

    /// <summary>
    /// 時間帯
    /// </summary>
    [Column("time_slot")]
    public string TimeSlot { get; init; } = string.Empty;

    /// <summary>
    /// 残り枠数
    /// </summary>
    [Column("remain")]
    public int Remain { get; init; }

    /// <summary>
    /// 更新日時
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 元のExcelファイルパス
    /// </summary>
    [Column("file_path")]
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// 変更種別
    /// </summary>
    [Column("change_type")]
    public ChangeType ChangeType { get; init; }

    /// <summary>
    /// 等価比較
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not ReservationData other)
            return false;

        return StoreId == other.StoreId &&
               Date.Equals(other.Date) &&
               TimeSlot == other.TimeSlot &&
               Remain == other.Remain;
    }

    /// <summary>
    /// ハッシュコードの取得
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(StoreId, Date, TimeSlot, Remain);
    }

    /// <summary>
    /// 文字列表現
    /// </summary>
    public override string ToString()
    {
        return $"{StoreId} - {Date:yyyy/MM/dd} {TimeSlot} - 残り{Remain}枠 ({ChangeType})";
    }
} 