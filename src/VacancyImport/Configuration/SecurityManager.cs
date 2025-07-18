using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VacancyImport.Exceptions;
using VacancyImport.Logging;

namespace VacancyImport.Configuration;

public class SecurityManager
{
    private readonly ILogger<SecurityManager> _logger;
    private readonly byte[] _encryptionKey;
    private const string AuditLogPath = "logs/audit.log";
    
    public SecurityManager(ILogger<SecurityManager> logger, string encryptionKeyPath)
    {
        _logger = logger;
        
        // 暗号化キーの読み込みまたは生成
        if (File.Exists(encryptionKeyPath))
        {
            _encryptionKey = File.ReadAllBytes(encryptionKeyPath);
        }
        else
        {
            _encryptionKey = GenerateEncryptionKey();
            Directory.CreateDirectory(Path.GetDirectoryName(encryptionKeyPath) ?? string.Empty);
            File.WriteAllBytes(encryptionKeyPath, _encryptionKey);
        }
        
        EnsureAuditLogExists();
    }
    
    private byte[] GenerateEncryptionKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var key = new byte[32]; // 256-bit key
        rng.GetBytes(key);
        return key;
    }
    
    public string EncryptValue(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;
            
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        
        // IV を先頭に書き込む
        ms.Write(aes.IV, 0, aes.IV.Length);
        
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            using var sw = new StreamWriter(cs);
            sw.Write(plainText);
        }
        
        return Convert.ToBase64String(ms.ToArray());
    }
    
    public string DecryptValue(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;
            
        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            
            // IV は暗号文の先頭にある
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            
            // IV の長さを除いた残りの暗号文を復号
            ms.Write(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);
            ms.Position = 0;
            
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to decrypt value: {ex.Message}");
            throw new ConfigurationException("Failed to decrypt configuration value", "SecurityManager.DecryptValue", "CONFIG-SECURITY-ERR", ErrorSeverity.Error, false, ex);
        }
    }
    
    public void LogAuditEvent(string userId, string action, string resource)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("o");
            var logEntry = $"{timestamp}|{userId}|{action}|{resource}";
            
            File.AppendAllText(AuditLogPath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write audit log: {ex.Message}");
        }
    }
    
    private void EnsureAuditLogExists()
    {
        var directory = Path.GetDirectoryName(AuditLogPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        if (!File.Exists(AuditLogPath))
        {
            File.WriteAllText(AuditLogPath, $"Timestamp|UserId|Action|Resource{Environment.NewLine}");
        }
    }
} 