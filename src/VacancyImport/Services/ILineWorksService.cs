using System.Threading.Tasks;

namespace VacancyImport.Services;

public interface ILineWorksService
{
    Task<string> GetAccessTokenAsync();
    Task SendNotificationAsync(string message);
    Task SendProofListNotificationAsync(string summary, string proofListFileName);
    Task SendErrorNotificationAsync(string errorMessage, int errorCount);
} 