using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace VacancyImport.Configuration;

public class SimpleHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }

    public SimpleHostEnvironment(string environmentName, string applicationName, string contentRootPath)
    {
        EnvironmentName = environmentName;
        ApplicationName = applicationName;
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }
} 