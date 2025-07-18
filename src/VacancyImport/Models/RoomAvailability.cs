using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace VacancyImport.Models;

/// <summary>
/// 部屋の空室状況を表すモデル
/// </summary>
[Table("room_availability")]
public class RoomAvailability : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("store_id")]
    public string StoreId { get; set; } = string.Empty;

    [Column("date")]
    public DateOnly Date { get; set; }

    [Column("time_slot")]
    public string TimeSlot { get; set; } = string.Empty;

    [Column("remain")]
    public int Remain { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
} 