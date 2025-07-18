using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using VacancyImport.Exceptions;
using VacancyImport.Logging;

namespace VacancyImport.Configuration;

public class ConfigurationManager : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<AppSettings> _appSettings;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly List<IDisposable> _changeTokenRegistrations = new();
    private readonly Dictionary<string, DateTime> _configChangeHistory = new();
    
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    
    public AppSettings CurrentSettings => _appSettings.CurrentValue;
    
    public ConfigurationManager(IConfiguration configuration, IOptionsMonitor<AppSettings> appSettings, ILogger<ConfigurationManager> logger)
    {
        _configuration = configuration;
        _appSettings = appSettings;
        _logger = logger;
        
        // 設定変更を監視
        _changeTokenRegistrations.Add(_appSettings.OnChange(OnSettingsChanged));
    }
    
    private void OnSettingsChanged(AppSettings settings, string? name)
    {
        try
        {
            // 設定の検証
            ConfigurationValidator.ValidateAppSettings(settings);
            
            // 変更履歴の記録
            _configChangeHistory[name ?? "default"] = DateTime.UtcNow;
            
            // 変更通知
            _logger.LogInformation($"Configuration changed: {name ?? "default"}");
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(name ?? "default", settings));
        }
        catch (ConfigurationException ex)
        {
            _logger.LogError($"Invalid configuration detected: {ex.Message}");
            throw;
        }
    }
    
    public void ValidateCurrentConfiguration()
    {
        try
        {
            ConfigurationValidator.ValidateAppSettings(CurrentSettings);
        }
        catch (ConfigurationException ex)
        {
            _logger.LogError($"Current configuration is invalid: {ex.Message}");
            throw;
        }
    }
    
    public Dictionary<string, DateTime> GetConfigChangeHistory()
    {
        return new Dictionary<string, DateTime>(_configChangeHistory);
    }
    
    public void SaveConfigurationSnapshot(string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(CurrentSettings, options);
            File.WriteAllText(filePath, json);
            
            _logger.LogInformation($"Configuration snapshot saved to {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save configuration snapshot: {ex.Message}");
            throw;
        }
    }
    
    public void Dispose()
    {
        foreach (var registration in _changeTokenRegistrations)
        {
            registration.Dispose();
        }
        
        _changeTokenRegistrations.Clear();
    }
}

public class ConfigurationChangedEventArgs : EventArgs
{
    public string Name { get; }
    public AppSettings Settings { get; }
    
    public ConfigurationChangedEventArgs(string name, AppSettings settings)
    {
        Name = name;
        Settings = settings;
    }
} 