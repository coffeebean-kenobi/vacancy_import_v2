using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VacancyImport.Models;

/// <summary>
/// 施設の月別予約数を表すモデル
/// </summary>
[Table("facility_monthly_reservations")]
public class FacilityMonthlyReservation : BaseModel
{
    [PrimaryKey("tenant_id")]
    public int TenantId { get; set; }

    [PrimaryKey("facility_id")]
    public int FacilityId { get; set; }

    [PrimaryKey("year")]
    public int Year { get; set; }

    [PrimaryKey("month")]
    public int Month { get; set; }

    [Column("reservation_counts")]
    public string[] ReservationCounts { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 等価性を判定
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not FacilityMonthlyReservation other)
            return false;

        return TenantId == other.TenantId &&
               FacilityId == other.FacilityId &&
               Year == other.Year &&
               Month == other.Month &&
               ReservationCounts.Length == other.ReservationCounts.Length &&
               ReservationCounts.SequenceEqual(other.ReservationCounts);
    }

    /// <summary>
    /// ハッシュコードを生成
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TenantId);
        hash.Add(FacilityId);
        hash.Add(Year);
        hash.Add(Month);
        
        // 配列の内容をハッシュに追加
        if (ReservationCounts != null)
        {
            foreach (var count in ReservationCounts)
            {
                hash.Add(count);
            }
        }
        
        return hash.ToHashCode();
    }

    /// <summary>
    /// 文字列表現
    /// </summary>
    public override string ToString()
    {
        return $"FacilityMonthlyReservation(TenantId={TenantId}, FacilityId={FacilityId}, Year={Year}, Month={Month}, Counts=[{string.Join(",", ReservationCounts)}])";
    }
} 