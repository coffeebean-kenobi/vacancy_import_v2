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
} 