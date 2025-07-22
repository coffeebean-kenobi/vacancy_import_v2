using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Supabase;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using VacancyImport.Configuration;
using VacancyImport.Exceptions;
using VacancyImport.Models;

namespace VacancyImport.Services;

/// <summary>
/// Supabase連携サービス
/// </summary>
public class SupabaseService : ISupabaseService
{
    private readonly SupabaseSettings _settings;
    private readonly AppSettings _appSettings;
    private readonly ILogger<SupabaseService> _logger;
    private readonly Supabase.Client _client;
    private RealtimeChannel? _channel;
    private bool _isSubscribed;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SupabaseService(IOptions<AppSettings> settings, ILogger<SupabaseService> logger)
    {
        _appSettings = settings.Value;
        _settings = settings.Value.SupabaseSettings;
        _logger = logger;

        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true
        };

        _client = new Supabase.Client(_settings.Url, _settings.Key, options);
    }

    /// <summary>
    /// 現在の月別予約データを取得
    /// </summary>
    public async Task<IEnumerable<FacilityMonthlyReservation>> GetCurrentMonthlyReservationsAsync()
    {
        try
        {
            var response = await _client
                .From<FacilityMonthlyReservation>()
                .Select("*")
                .Get();

            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "月別予約データの取得中にエラーが発生しました");
            throw new SupabaseException("月別予約データの取得に失敗しました", ex.Message);
        }
    }

    /// <summary>
    /// 月別予約データをSupabaseに更新し、変更情報を返す
    /// </summary>
    /// <param name="monthlyReservations">更新する月別予約データ</param>
    /// <returns>変更情報のリスト</returns>
    public async Task<IEnumerable<ReservationChange>> UpdateMonthlyReservationsAsync(IEnumerable<FacilityMonthlyReservation> monthlyReservations)
    {
        var changes = new List<ReservationChange>();
        
        try
        {
            _logger.LogInformation("月別予約データの更新を開始します");
            
            // バッチサイズに分割して処理
            var batches = monthlyReservations
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / _appSettings.PerformanceSettings.DatabaseBatchSize)
                .Select(g => g.Select(x => x.item));

            foreach (var batch in batches)
            {
                var batchChanges = await ProcessMonthlyReservationBatchAsync(batch);
                changes.AddRange(batchChanges);
            }

            _logger.LogInformation($"月別予約データの更新が完了しました。変更件数: {changes.Count}");
            
            return changes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "月別予約データの更新中にエラーが発生しました");
            throw;
        }
    }

    private async Task<IEnumerable<ReservationChange>> ProcessMonthlyReservationBatchAsync(IEnumerable<FacilityMonthlyReservation> batch)
    {
        var changes = new List<ReservationChange>();
        
        foreach (var monthlyReservation in batch)
        {
            try
            {
                // 既存データの取得
                var existingQuery = _client
                    .From<FacilityMonthlyReservation>()
                    .Where(x => x.TenantId == monthlyReservation.TenantId)
                    .Where(x => x.FacilityId == monthlyReservation.FacilityId)
                    .Where(x => x.Year == monthlyReservation.Year)
                    .Where(x => x.Month == monthlyReservation.Month);

                var existingResult = await existingQuery.Get();
                var existingData = existingResult?.Models?.FirstOrDefault();

                if (existingData == null)
                {
                    // 新規作成
                    await _client
                        .From<FacilityMonthlyReservation>()
                        .Insert(monthlyReservation);

                    changes.Add(ReservationChange.CreateNew(
                        monthlyReservation.FacilityId.ToString(),
                        new DateTime(monthlyReservation.Year, monthlyReservation.Month, 1),
                        $"{monthlyReservation.Year}-{monthlyReservation.Month}",
                        monthlyReservation.ReservationCounts.Length
                    ));

                    _logger.LogDebug($"新規作成: 施設{monthlyReservation.FacilityId} {monthlyReservation.Year}-{monthlyReservation.Month}");
                }
                else
                {
                    // 更新（配列の内容を比較）
                    var oldCounts = existingData.ReservationCounts;
                    var newCounts = monthlyReservation.ReservationCounts;
                    
                    if (!oldCounts.SequenceEqual(newCounts))
                    {
                        existingData.ReservationCounts = newCounts;

                        await _client
                            .From<FacilityMonthlyReservation>()
                            .Where(x => x.TenantId == existingData.TenantId)
                            .Where(x => x.FacilityId == existingData.FacilityId)
                            .Where(x => x.Year == existingData.Year)
                            .Where(x => x.Month == existingData.Month)
                            .Update(existingData);

                        changes.Add(ReservationChange.CreateChanged(
                            monthlyReservation.FacilityId.ToString(),
                            new DateTime(monthlyReservation.Year, monthlyReservation.Month, 1),
                            $"{monthlyReservation.Year}-{monthlyReservation.Month}",
                            oldCounts.Length,
                            newCounts.Length
                        ));

                        _logger.LogDebug($"更新: 施設{monthlyReservation.FacilityId} {monthlyReservation.Year}-{monthlyReservation.Month}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"月別予約データの処理中にエラーが発生しました: 施設{monthlyReservation.FacilityId} {monthlyReservation.Year}-{monthlyReservation.Month}");
                // 個別エラーは継続して処理
            }
        }

        return changes;
    }

    /// <summary>
    /// リアルタイム更新の購読を開始
    /// </summary>
    public Task StartRealtimeSubscriptionAsync()
    {
        if (_isSubscribed)
        {
            _logger.LogWarning("リアルタイムサブスクリプションは既に開始されています");
            return Task.CompletedTask;
        }

        try
        {
            _channel = _client.Realtime.Channel("realtime", "public", _settings.TableName);
            
            _channel.AddPostgresChangeHandler(
                PostgresChangesOptions.ListenType.All,
                async (sender, change) =>
                {
                    try
                    {
                        await HandleRealtimeChangeAsync(change);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "リアルタイム更新の処理中にエラーが発生しました");
                    }
                });

            _channel.Subscribe();
            _isSubscribed = true;
            _logger.LogInformation("リアルタイムサブスクリプションを開始しました");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リアルタイムサブスクリプションの開始中にエラーが発生しました");
            throw new SupabaseException("リアルタイムサブスクリプションの開始に失敗しました", ex.Message);
        }
    }

    /// <summary>
    /// リアルタイム更新の購読を停止
    /// </summary>
    public Task StopRealtimeSubscriptionAsync()
    {
        if (!_isSubscribed || _channel == null)
        {
            _logger.LogWarning("リアルタイムサブスクリプションは開始されていません");
            return Task.CompletedTask;
        }

        try
        {
            _channel.Unsubscribe();
            _channel = null;
            _isSubscribed = false;
            _logger.LogInformation("リアルタイムサブスクリプションを停止しました");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リアルタイムサブスクリプションの停止中にエラーが発生しました");
            throw new SupabaseException("リアルタイムサブスクリプションの停止に失敗しました", ex.Message);
        }
    }

    /// <summary>
    /// リアルタイム更新の処理
    /// </summary>
    private async Task HandleRealtimeChangeAsync(PostgresChangesResponse change)
    {
        try
        {
            switch (change.Event)
            {
                case Supabase.Realtime.Constants.EventType.Insert:
                    _logger.LogInformation("新しい予約が追加されました: {Reservation}", change.Payload);
                    break;

                case Supabase.Realtime.Constants.EventType.Update:
                    _logger.LogInformation("予約が更新されました: {Reservation}", change.Payload);
                    break;

                case Supabase.Realtime.Constants.EventType.Delete:
                    _logger.LogInformation("予約が削除されました: {Reservation}", change.Payload);
                    break;

                default:
                    _logger.LogWarning("未処理のイベントタイプ: {EventType}", change.Event);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リアルタイム更新の処理中にエラーが発生しました: {EventType}", change.Event);
            throw;
        }
    }
} 