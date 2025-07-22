using System.Collections.Generic;
using System.Threading.Tasks;
using VacancyImport.Models;

namespace VacancyImport.Services;

public interface ISupabaseService
{
    Task<IEnumerable<FacilityMonthlyReservation>> GetCurrentMonthlyReservationsAsync();
    Task<IEnumerable<ReservationChange>> UpdateMonthlyReservationsAsync(IEnumerable<FacilityMonthlyReservation> changes);
    /// <summary>
    /// リアルタイム更新の購読を開始
    /// </summary>
    Task StartRealtimeSubscriptionAsync();

    /// <summary>
    /// リアルタイム更新の購読を停止
    /// </summary>
    Task StopRealtimeSubscriptionAsync();
} 