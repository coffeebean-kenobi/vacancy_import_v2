using System;
using System.Collections.Generic;
using System.Linq;
using VacancyImport.Exceptions;

namespace VacancyImport.Configuration;

public class ConfigurationValidator
{
    public static void ValidateExcelSettings(ExcelSettings settings)
    {
        if (settings == null)
            throw new ConfigurationException("ExcelSettings cannot be null", "ExcelSettings", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (settings.PollingIntervalMinutes <= 0)
            throw new ConfigurationException("PollingIntervalMinutes must be greater than zero", "PollingIntervalMinutes", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (settings.RetryCount < 0)
            throw new ConfigurationException("RetryCount cannot be negative", "RetryCount", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (settings.Environments == null || !settings.Environments.Any())
            throw new ConfigurationException("At least one environment must be configured in ExcelSettings", "Environments", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        foreach (var env in settings.Environments)
        {
            ValidateExcelEnvironmentSettings(env.Key, env.Value);
        }
    }
    
    private static void ValidateExcelEnvironmentSettings(string environment, ExcelEnvironmentSettings settings)
    {
        if (settings == null)
            throw new ConfigurationException($"Settings for environment '{environment}' cannot be null", $"Environments.{environment}", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.BasePath))
            throw new ConfigurationException($"BasePath for environment '{environment}' cannot be empty", $"Environments.{environment}.BasePath", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.SheetName))
            throw new ConfigurationException($"SheetName for environment '{environment}' cannot be empty", $"Environments.{environment}.SheetName", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.ColumnName))
            throw new ConfigurationException($"ColumnName for environment '{environment}' cannot be empty", $"Environments.{environment}.ColumnName", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
            
        if (settings.ColumnIndex <= 0)
            throw new ConfigurationException($"ColumnIndex for environment '{environment}' must be greater than zero", $"Environments.{environment}.ColumnIndex", "CONFIG-EXCEL-ERR", ErrorSeverity.Error, false);
    }
    
    public static void ValidateSupabaseSettings(SupabaseSettings settings)
    {
        if (settings == null)
            throw new ConfigurationException("SupabaseSettings cannot be null", "SupabaseSettings", "CONFIG-SUPABASE-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.Url))
            throw new ConfigurationException("Supabase URL cannot be empty", "SupabaseSettings.Url", "CONFIG-SUPABASE-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.Key))
            throw new ConfigurationException("Supabase Key cannot be empty", "SupabaseSettings.Key", "CONFIG-SUPABASE-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.TableName))
            throw new ConfigurationException("Supabase TableName cannot be empty", "SupabaseSettings.TableName", "CONFIG-SUPABASE-ERR", ErrorSeverity.Error, false);
    }
    
    public static void ValidateLineWorksSettings(LineWorksSettings settings)
    {
        if (settings == null)
            throw new ConfigurationException("LineWorksSettings cannot be null", "LineWorksSettings", "CONFIG-LINEWORKS-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.BotId))
            throw new ConfigurationException("LINE WORKS BotId cannot be empty", "LineWorksSettings.BotId", "CONFIG-LINEWORKS-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new ConfigurationException("LINE WORKS ClientId cannot be empty", "LineWorksSettings.ClientId", "CONFIG-LINEWORKS-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.ClientSecret))
            throw new ConfigurationException("LINE WORKS ClientSecret cannot be empty", "LineWorksSettings.ClientSecret", "CONFIG-LINEWORKS-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.TokenUrl))
            throw new ConfigurationException("LINE WORKS TokenUrl cannot be empty", "LineWorksSettings.TokenUrl", "CONFIG-LINEWORKS-ERR", ErrorSeverity.Error, false);
            
        if (string.IsNullOrWhiteSpace(settings.MessageUrl))
            throw new ConfigurationException("LINE WORKS MessageUrl cannot be empty", "LineWorksSettings.MessageUrl", "CONFIG-LINEWORKS-ERR", ErrorSeverity.Error, false);
    }
    
    public static void ValidateAppSettings(AppSettings settings)
    {
        if (settings == null)
            throw new ConfigurationException("AppSettings cannot be null", "AppSettings", "CONFIG-APP-ERR", ErrorSeverity.Error, false);
            
        ValidateExcelSettings(settings.ExcelSettings);
        ValidateSupabaseSettings(settings.SupabaseSettings);
        ValidateLineWorksSettings(settings.LineWorksSettings);
    }
} 