using System.Threading.Tasks;
using VacancyImport.Models;

namespace VacancyImport.Services;

public interface IExcelService
{
    Task<bool> CheckFileUpdatesAsync();
    Task<IEnumerable<ReservationData>> ExtractReservationDataAsync();
    Task SaveProofListAsync(IEnumerable<ReservationData> changes);
} 