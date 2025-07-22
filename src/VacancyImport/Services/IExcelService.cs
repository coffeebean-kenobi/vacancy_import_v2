using System.Threading.Tasks;
using VacancyImport.Models;

namespace VacancyImport.Services;

public interface IExcelService
{
    Task<bool> CheckFileUpdatesAsync();
    Task<IEnumerable<FacilityMonthlyReservation>> ExtractMonthlyReservationsAsync();
    Task SaveProofListAsync(IEnumerable<FacilityMonthlyReservation> changes);
} 