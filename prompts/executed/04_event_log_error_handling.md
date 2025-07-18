# イベントログ設定とエラーハンドリング強化プロンプト

## 📖 概要
Windows Service用のイベントログ設定を追加し、エラーハンドリングを強化する。パフォーマンス監視機能を統合し、Windows Service特有の堅牢なエラー処理を実装する。

## 🎯 実装対象
- Windows イベントログの設定と実装
- グローバルエラーハンドリングの強化
- パフォーマンス監視の統合
- 障害復旧機能の実装
- ヘルスチェック機能の追加

## 📋 詳細仕様

### 1. イベントログサービス作成

**ファイル**: `src/VacancyImport/Services/EventLogService.cs`

```csharp
using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Services
{
    /// <summary>
    /// Windows イベントログサービス
    /// </summary>
    public class EventLogService
    {
        private readonly ILogger<EventLogService> _logger;
        private readonly ServiceSettings _serviceSettings;
        private EventLog _eventLog;

        public EventLogService(ILogger<EventLogService> logger, IOptions<ServiceSettings> serviceSettings)
        {
            _logger = logger;
            _serviceSettings = serviceSettings.Value;
            InitializeEventLog();
        }

        private void InitializeEventLog()
        {
            try
            {
                const string logName = "Application";
                const string sourceName = "VacancyImportService";

                // イベントログソースの確認・作成
                if (!EventLog.SourceExists(sourceName))
                {
                    EventLog.CreateEventSource(sourceName, logName);
                    _logger.LogInformation($"イベントログソース '{sourceName}' を作成しました");
                }

                _eventLog = new EventLog(logName)
                {
                    Source = sourceName
                };

                _logger.LogInformation("イベントログサービスが初期化されました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "イベントログの初期化に失敗しました");
                // イベントログが使用できない場合でも続行
            }
        }

        /// <summary>
        /// 情報イベントを記録
        /// </summary>
        public void WriteInformation(string message, int eventId = 1000)
        {
            WriteEntry(message, EventLogEntryType.Information, eventId);
        }

        /// <summary>
        /// 警告イベントを記録
        /// </summary>
        public void WriteWarning(string message, int eventId = 2000)
        {
            WriteEntry(message, EventLogEntryType.Warning, eventId);
        }

        /// <summary>
        /// エラーイベントを記録
        /// </summary>
        public void WriteError(string message, int eventId = 3000)
        {
            WriteEntry(message, EventLogEntryType.Error, eventId);
        }

        /// <summary>
        /// エラーイベントを記録（例外付き）
        /// </summary>
        public void WriteError(string message, Exception exception, int eventId = 3000)
        {
            var fullMessage = $"{message}\n\n例外詳細:\n{exception}";
            WriteEntry(fullMessage, EventLogEntryType.Error, eventId);
        }

        /// <summary>
        /// サービス開始イベントを記録
        /// </summary>
        public void WriteServiceStart()
        {
            WriteInformation($"予約管理システム連携サービスが開始されました (Version: {GetAssemblyVersion()})", 1001);
        }

        /// <summary>
        /// サービス停止イベントを記録
        /// </summary>
        public void WriteServiceStop()
        {
            WriteInformation("予約管理システム連携サービスが停止されました", 1002);
        }

        /// <summary>
        /// 設定変更イベントを記録
        /// </summary>
        public void WriteConfigurationChange(string configName, string oldValue, string newValue)
        {
            WriteInformation($"設定が変更されました: {configName} ({oldValue} → {newValue})", 1010);
        }

        /// <summary>
        /// データ処理完了イベントを記録
        /// </summary>
        public void WriteDataProcessingComplete(int changesCount, TimeSpan processingTime)
        {
            WriteInformation($"データ処理が完了しました: 変更件数={changesCount}, 処理時間={processingTime:mm\\:ss}", 1020);
        }

        /// <summary>
        /// 連続エラーイベントを記録
        /// </summary>
        public void WriteConsecutiveErrors(int errorCount, string lastError)
        {
            WriteError($"連続エラーが発生しています: {errorCount}回\n最新エラー: {lastError}", 3010);
        }

        private void WriteEntry(string message, EventLogEntryType type, int eventId)
        {
            try
            {
                _eventLog?.WriteEntry(message, type, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"イベントログの書き込みに失敗しました: {message}");
            }
        }

        private string GetAssemblyVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public void Dispose()
        {
            _eventLog?.Dispose();
        }
    }
}
```

### 2. ヘルスチェックサービス作成

**ファイル**: `src/VacancyImport/Services/HealthCheckService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Services
{
    /// <summary>
    /// システムヘルスチェックサービス
    /// </summary>
    public class HealthCheckService
    {
        private readonly ILogger<HealthCheckService> _logger;
        private readonly AppSettings _settings;
        private readonly EventLogService _eventLogService;

        public HealthCheckService(
            ILogger<HealthCheckService> logger, 
            IOptions<AppSettings> settings,
            EventLogService eventLogService)
        {
            _logger = logger;
            _settings = settings.Value;
            _eventLogService = eventLogService;
        }

        /// <summary>
        /// 総合ヘルスチェック実行
        /// </summary>
        public async Task<HealthCheckResult> PerformHealthCheckAsync()
        {
            var result = new HealthCheckResult();
            var checks = new List<Task<bool>>
            {
                CheckDiskSpaceAsync(result),
                CheckExcelPathAccessAsync(result),
                CheckSupabaseConnectionAsync(result),
                CheckLineWorksConfigurationAsync(result),
                CheckProofListDirectoryAsync(result)
            };

            try
            {
                await Task.WhenAll(checks);
                
                result.IsHealthy = result.FailedChecks.Count == 0;
                result.CheckedAt = DateTime.Now;

                if (!result.IsHealthy)
                {
                    _eventLogService.WriteWarning($"ヘルスチェックで問題が検出されました: {string.Join(", ", result.FailedChecks)}", 2001);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ヘルスチェック実行中にエラーが発生しました");
                result.IsHealthy = false;
                result.FailedChecks.Add("HealthCheck execution failed");
                return result;
            }
        }

        private async Task<bool> CheckDiskSpaceAsync(HealthCheckResult result)
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory()));
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024);
                
                if (freeSpaceGB < 1) // 1GB未満の場合は警告
                {
                    result.FailedChecks.Add($"低ディスク容量: {freeSpaceGB:F1}GB");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ディスク容量チェックに失敗しました");
                result.FailedChecks.Add("ディスク容量チェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckExcelPathAccessAsync(HealthCheckResult result)
        {
            try
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
                var excelPath = _settings.ExcelSettings.GetEnvironmentSettings(environment).BasePath;
                
                if (!Directory.Exists(excelPath))
                {
                    result.FailedChecks.Add($"Excelパスにアクセスできません: {excelPath}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excelパスアクセスチェックに失敗しました");
                result.FailedChecks.Add("Excelパスアクセスチェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckSupabaseConnectionAsync(HealthCheckResult result)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.SupabaseSettings.Url) || 
                    string.IsNullOrEmpty(_settings.SupabaseSettings.Key))
                {
                    result.FailedChecks.Add("Supabase設定が不完全です");
                    return false;
                }
                
                // 簡易接続チェック（実際のAPI呼び出しは行わない）
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase接続チェックに失敗しました");
                result.FailedChecks.Add("Supabase接続チェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckLineWorksConfigurationAsync(HealthCheckResult result)
        {
            try
            {
                var lineWorksSettings = _settings.LineWorksSettings;
                if (string.IsNullOrEmpty(lineWorksSettings.BotId) ||
                    string.IsNullOrEmpty(lineWorksSettings.ClientId) ||
                    string.IsNullOrEmpty(lineWorksSettings.ClientSecret))
                {
                    result.FailedChecks.Add("LINE WORKS設定が不完全です");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LINE WORKS設定チェックに失敗しました");
                result.FailedChecks.Add("LINE WORKS設定チェック失敗");
                return false;
            }
        }

        private async Task<bool> CheckProofListDirectoryAsync(HealthCheckResult result)
        {
            try
            {
                var proofDirectory = _settings.ProofListSettings?.OutputDirectory ?? "./proof";
                
                if (!Directory.Exists(proofDirectory))
                {
                    Directory.CreateDirectory(proofDirectory);
                }
                
                // 書き込み権限テスト
                var testFile = Path.Combine(proofDirectory, "healthcheck.tmp");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "プルーフリストディレクトリチェックに失敗しました");
                result.FailedChecks.Add("プルーフリストディレクトリアクセス失敗");
                return false;
            }
        }
    }

    /// <summary>
    /// ヘルスチェック結果
    /// </summary>
    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; } = true;
        public List<string> FailedChecks { get; set; } = new List<string>();
        public DateTime CheckedAt { get; set; }
        
        public string GetSummary()
        {
            if (IsHealthy)
            {
                return "✅ すべてのヘルスチェックが正常です";
            }
            
            return $"⚠️ ヘルスチェックで {FailedChecks.Count} 件の問題が検出されました:\n" +
                   string.Join("\n", FailedChecks.Select(f => $"- {f}"));
        }
    }
}
```

### 3. ServiceHost エラーハンドリング強化

**ファイル**: `src/VacancyImport/Services/ServiceHost.cs` に追加メソッド

```csharp
// 既存のクラスに以下のメソッドとフィールドを追加

private readonly EventLogService _eventLogService;
private readonly HealthCheckService _healthCheckService;
private readonly PerformanceMonitor _performanceMonitor;
private DateTime _lastHealthCheck = DateTime.MinValue;
private int _consecutiveErrors = 0;
private DateTime _lastErrorNotification = DateTime.MinValue;

// コンストラクタに追加パラメータ
public ServiceHost(
    IServiceProvider serviceProvider, 
    ILogger<ServiceHost> logger, 
    IOptions<ServiceSettings> serviceSettings,
    EventLogService eventLogService,
    HealthCheckService healthCheckService,
    PerformanceMonitor performanceMonitor)
{
    _serviceProvider = serviceProvider;
    _logger = logger;
    _serviceSettings = serviceSettings.Value;
    _eventLogService = eventLogService;
    _healthCheckService = healthCheckService;
    _performanceMonitor = performanceMonitor;
}

/// <summary>
/// 定期ヘルスチェック実行
/// </summary>
private async Task PerformPeriodicHealthCheckAsync()
{
    var now = DateTime.Now;
    
    // 1時間に1回実行
    if ((now - _lastHealthCheck).TotalHours >= 1)
    {
        try
        {
            _logger.LogInformation("定期ヘルスチェックを開始します");
            
            var healthResult = await _healthCheckService.PerformHealthCheckAsync();
            
            if (!healthResult.IsHealthy)
            {
                _eventLogService.WriteWarning($"ヘルスチェック警告: {string.Join(", ", healthResult.FailedChecks)}", 2002);
                
                // 重要なエラーの場合は通知
                var criticalErrors = healthResult.FailedChecks.Where(f => 
                    f.Contains("Supabase") || 
                    f.Contains("Excel") || 
                    f.Contains("ディスク容量")).ToList();
                
                if (criticalErrors.Any())
                {
                    var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
                    await lineWorksService.SendErrorNotificationAsync(
                        $"システム警告: {string.Join(", ", criticalErrors)}", 
                        1
                    );
                }
            }
            
            _lastHealthCheck = now;
            _logger.LogInformation("定期ヘルスチェックが完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "定期ヘルスチェック中にエラーが発生しました");
        }
    }
}

/// <summary>
/// エラーハンドリングとリトライロジック
/// </summary>
private async Task<bool> HandleExecutionErrorAsync(Exception ex)
{
    _consecutiveErrors++;
    
    // パフォーマンス監視にエラーを記録
    _performanceMonitor.RecordError(ex);
    
    // イベントログに記録
    _eventLogService.WriteError($"実行エラー (連続{_consecutiveErrors}回)", ex, 3020);
    
    _logger.LogError(ex, $"メインループでエラーが発生しました (連続エラー数: {_consecutiveErrors})");
    
    // エラー通知の頻度制限（10分に1回）
    var now = DateTime.Now;
    if ((now - _lastErrorNotification).TotalMinutes >= 10)
    {
        try
        {
            var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
            await lineWorksService.SendErrorNotificationAsync(ex.Message, _consecutiveErrors);
            _lastErrorNotification = now;
        }
        catch (Exception notificationEx)
        {
            _logger.LogError(notificationEx, "エラー通知の送信に失敗しました");
        }
    }
    
    // 最大試行回数チェック
    if (_consecutiveErrors >= _serviceSettings.MaxRetryAttempts)
    {
        _eventLogService.WriteError($"最大試行回数({_serviceSettings.MaxRetryAttempts})に達しました。サービスを停止します。", 3030);
        
        // 緊急通知
        try
        {
            var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
            await lineWorksService.SendErrorNotificationAsync(
                $"🚨 重大エラー: 連続{_consecutiveErrors}回のエラーによりサービスを停止します", 
                _consecutiveErrors
            );
        }
        catch
        {
            // 通知失敗は無視
        }
        
        return false; // サービス停止
    }
    
    return true; // 継続
}

/// <summary>
/// 成功時の処理
/// </summary>
private void HandleExecutionSuccess()
{
    if (_consecutiveErrors > 0)
    {
        _logger.LogInformation($"エラー状態から回復しました (連続エラー数: {_consecutiveErrors} → 0)");
        _eventLogService.WriteInformation($"エラー状態から回復しました (連続エラー数: {_consecutiveErrors})", 1030);
        _consecutiveErrors = 0;
    }
}

// RunMainLoopAsyncメソッドを修正
private async Task RunMainLoopAsync(CancellationToken cancellationToken)
{
    var pollingInterval = TimeSpan.FromMinutes(_serviceSettings.PollingIntervalMinutes);
    var retryInterval = TimeSpan.FromMinutes(_serviceSettings.RetryIntervalMinutes);

    _logger.LogInformation($"メインループを開始します (ポーリング間隔: {pollingInterval})");
    _eventLogService.WriteServiceStart();

    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            // 定期ヘルスチェック
            await PerformPeriodicHealthCheckAsync();
            
            // メインのビジネスロジック実行
            using var activity = _performanceMonitor.StartActivity("BusinessLogicExecution");
            
            await ExecuteBusinessLogicAsync();
            
            // 成功時の処理
            HandleExecutionSuccess();
            
            // 通常の待機
            await Task.Delay(pollingInterval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 正常なキャンセル
            break;
        }
        catch (Exception ex)
        {
            // エラーハンドリング
            var shouldContinue = await HandleExecutionErrorAsync(ex);
            
            if (!shouldContinue)
            {
                throw; // サービス停止
            }
            
            // エラー時は短い間隔でリトライ
            try
            {
                await Task.Delay(retryInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
```

### 4. Program.cs DI登録更新

**ファイル**: `src/VacancyImport/Program.cs` の `ConfigureServices` メソッドに追加

```csharp
// ユーティリティサービスの登録（既存の下に追加）
services.AddSingleton<EventLogService>();
services.AddSingleton<HealthCheckService>();

// パフォーマンスモニタリングが未登録の場合
// services.AddSingleton<PerformanceMonitor>();
```

## 🔍 検証手順

1. **イベントログサービステスト**:
   ```bash
   # Windows Event Viewerでテストメッセージを確認
   eventvwr.msc
   ```

2. **ヘルスチェックテスト**:
   ```bash
   dotnet run --configuration Debug -- --console
   # ログでヘルスチェック結果を確認
   ```

3. **エラーハンドリングテスト**:
   ```bash
   # 意図的にエラーを発生させてリトライ動作を確認
   ```

## 📚 参考ドキュメント

- [Windows イベントログ](https://learn.microsoft.com/ja-jp/dotnet/api/system.diagnostics.eventlog)
- [例外処理のベストプラクティス](https://learn.microsoft.com/ja-jp/dotnet/standard/exceptions/best-practices-for-exceptions)

## 🎯 完了条件

- [ ] EventLogServiceが実装されている
- [ ] HealthCheckServiceが実装されている
- [ ] ServiceHostにエラーハンドリングが統合されている
- [ ] Windows Event LogにServi事の動作が記録される
- [ ] ヘルスチェックが定期的に実行される
- [ ] エラー時の通知とリトライが正常に動作する 