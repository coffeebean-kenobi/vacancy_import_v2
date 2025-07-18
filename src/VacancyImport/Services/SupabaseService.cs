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
    /// 現在の予約データを取得
    /// </summary>
    public async Task<IEnumerable<ReservationData>> GetCurrentReservationsAsync()
    {
        try
        {
            var response = await _client
                .From<ReservationData>()
                .Select("*")
                .Get();

            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "予約データの取得中にエラーが発生しました");
            throw new SupabaseException("予約データの取得に失敗しました", ex.Message);
        }
    }

    /// <summary>
    /// 予約データをSupabaseに更新し、変更情報を返す
    /// </summary>
    /// <param name="reservationData">更新する予約データ</param>
    /// <returns>変更情報のリスト</returns>
    public async Task<IEnumerable<ReservationChange>> UpdateReservationsAsync(IEnumerable<ReservationData> reservationData)
    {
        var changes = new List<ReservationChange>();
        
        try
        {
            _logger.LogInformation("予約データの更新を開始します");
            
            // バッチサイズに分割して処理
            var batches = reservationData
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / _appSettings.PerformanceSettings.DatabaseBatchSize)
                .Select(g => g.Select(x => x.item));

            foreach (var batch in batches)
            {
                var batchChanges = await ProcessBatchAsync(batch);
                changes.AddRange(batchChanges);
            }

            _logger.LogInformation($"予約データの更新が完了しました。変更件数: {changes.Count}");
            
            return changes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "予約データの更新中にエラーが発生しました");
            throw;
        }
    }

    private async Task<IEnumerable<ReservationChange>> ProcessBatchAsync(IEnumerable<ReservationData> batch)
    {
        var changes = new List<ReservationChange>();
        
        foreach (var reservation in batch)
        {
            try
            {
                // 既存データの取得
                var existingQuery = _client
                    .From<RoomAvailability>()
                    .Where(x => x.StoreId == reservation.StoreId)
                    .Where(x => x.Date == reservation.Date)
                    .Where(x => x.TimeSlot == reservation.TimeSlot);

                var existingResult = await existingQuery.Get();
                var existingData = existingResult?.Models?.FirstOrDefault();

                if (existingData == null)
                {
                    // 新規作成
                    var newData = new RoomAvailability
                    {
                        StoreId = reservation.StoreId,
                        Date = reservation.Date,
                        TimeSlot = reservation.TimeSlot,
                        Remain = reservation.Remain,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _client
                        .From<RoomAvailability>()
                        .Insert(newData);

                    changes.Add(ReservationChange.CreateNew(
                        reservation.StoreId,
                        reservation.Date.ToDateTime(TimeOnly.MinValue),
                        reservation.TimeSlot,
                        reservation.Remain
                    ));

                    _logger.LogDebug($"新規作成: {reservation.StoreId} {reservation.Date:yyyy-MM-dd} {reservation.TimeSlot}");
                }
                else if (existingData.Remain != reservation.Remain)
                {
                    // 更新
                    var oldRemain = existingData.Remain;
                    existingData.Remain = reservation.Remain;
                    existingData.UpdatedAt = DateTime.UtcNow;

                    await _client
                        .From<RoomAvailability>()
                        .Where(x => x.Id == existingData.Id)
                        .Update(existingData);

                    changes.Add(ReservationChange.CreateChanged(
                        reservation.StoreId,
                        reservation.Date.ToDateTime(TimeOnly.MinValue),
                        reservation.TimeSlot,
                        oldRemain,
                        reservation.Remain
                    ));

                    _logger.LogDebug($"更新: {reservation.StoreId} {reservation.Date:yyyy-MM-dd} {reservation.TimeSlot} ({oldRemain} → {reservation.Remain})");
                }
                // 変更なしの場合は何もしない
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"予約データの処理中にエラーが発生しました: {reservation.StoreId} {reservation.Date:yyyy-MM-dd} {reservation.TimeSlot}");
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