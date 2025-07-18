using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VacancyImport.Utilities;

/// <summary>
/// メモリキャッシュを提供するクラス
/// </summary>
/// <typeparam name="TKey">キーの型</typeparam>
/// <typeparam name="TValue">値の型</typeparam>
public class MemoryCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheItem> _cache = new();
    private readonly TimeSpan _defaultExpiration;
    private readonly TimeSpan _cleanupInterval;
    private readonly Timer _cleanupTimer;
    private readonly ILogger _logger;
    private readonly int _maxItems;
    private readonly string _cacheName;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="cacheName">キャッシュ名</param>
    /// <param name="defaultExpirationMinutes">デフォルトの有効期限（分）</param>
    /// <param name="cleanupIntervalMinutes">クリーンアップ間隔（分）</param>
    /// <param name="maxItems">最大アイテム数</param>
    public MemoryCache(
        ILogger logger,
        string cacheName,
        int defaultExpirationMinutes = 15,
        int cleanupIntervalMinutes = 5,
        int maxItems = 1000)
    {
        _logger = logger;
        _cacheName = cacheName;
        _defaultExpiration = TimeSpan.FromMinutes(defaultExpirationMinutes);
        _cleanupInterval = TimeSpan.FromMinutes(cleanupIntervalMinutes);
        _maxItems = maxItems;
        _cleanupTimer = new Timer(CleanupCallback, null, _cleanupInterval, _cleanupInterval);
        
        _logger.LogDebug("メモリキャッシュを初期化しました: {CacheName} (有効期限: {Expiration}分, クリーンアップ間隔: {Interval}分, 最大数: {MaxItems})",
            _cacheName, defaultExpirationMinutes, cleanupIntervalMinutes, maxItems);
    }

    /// <summary>
    /// キャッシュにアイテムを追加または更新
    /// </summary>
    public void Set(TKey key, TValue value, TimeSpan? expiration = null)
    {
        var expiresAt = DateTime.UtcNow.Add(expiration ?? _defaultExpiration);
        var item = new CacheItem(value, expiresAt);
        
        _cache[key] = item;
        
        // 最大数を超えた場合は、最も古いアイテムを削除
        if (_cache.Count > _maxItems)
        {
            TrimCache();
        }
    }

    /// <summary>
    /// キャッシュからアイテムを取得
    /// </summary>
    public bool TryGet(TKey key, out TValue? value)
    {
        if (_cache.TryGetValue(key, out var item) && !item.IsExpired)
        {
            item.LastAccessed = DateTime.UtcNow;
            value = item.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// キャッシュからアイテムを取得または値ファクトリから生成
    /// </summary>
    public async Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> valueFactory, TimeSpan? expiration = null)
    {
        // まずキャッシュから取得を試みる
        if (TryGet(key, out var cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        try
        {
            // 同時実行による重複生成を防ぐためにロックを取得
            await _lock.WaitAsync();
            
            // ロック取得後に再度キャッシュを確認
            if (TryGet(key, out cachedValue) && cachedValue != null)
            {
                return cachedValue;
            }
            
            // キャッシュに存在しないため値を生成
            var value = await valueFactory();
            Set(key, value, expiration);
            return value;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// キャッシュからアイテムを削除
    /// </summary>
    public bool TryRemove(TKey key, out TValue? value)
    {
        if (_cache.TryRemove(key, out var item))
        {
            value = item.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// キャッシュ内のアイテム数を取得
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// キャッシュを空にする
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _logger.LogDebug("メモリキャッシュをクリアしました: {CacheName}", _cacheName);
    }

    /// <summary>
    /// クリーンアップコールバック
    /// </summary>
    private void CleanupCallback(object? state)
    {
        try
        {
            int removedCount = RemoveExpiredItems();
            if (removedCount > 0)
            {
                _logger.LogDebug("期限切れキャッシュアイテムを削除しました: {CacheName} ({Count}件 削除, 残り: {Remaining}件)",
                    _cacheName, removedCount, _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "キャッシュクリーンアップ中にエラーが発生しました: {CacheName}", _cacheName);
        }
    }

    /// <summary>
    /// 期限切れのアイテムを削除
    /// </summary>
    private int RemoveExpiredItems()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        return expiredKeys.Count;
    }

    /// <summary>
    /// キャッシュをトリミング（最も古いアイテムを削除）
    /// </summary>
    private void TrimCache()
    {
        // 最も古くアクセスされたアイテムから削除
        var oldestKeys = _cache
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(_cache.Count - _maxItems + 10) // バッファとして少し多めに削除
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestKeys)
        {
            _cache.TryRemove(key, out _);
        }

        _logger.LogDebug("キャッシュをトリミングしました: {CacheName} ({Count}件 削除, 残り: {Remaining}件)",
            _cacheName, oldestKeys.Count, _cache.Count);
    }

    /// <summary>
    /// リソースの破棄
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// リソースの破棄
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cleanupTimer?.Dispose();
            _lock?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// デストラクタ
    /// </summary>
    ~MemoryCache()
    {
        Dispose(false);
    }

    /// <summary>
    /// キャッシュアイテム
    /// </summary>
    private class CacheItem
    {
        public TValue Value { get; }
        public DateTime ExpiresAt { get; }
        public DateTime LastAccessed { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        public CacheItem(TValue value, DateTime expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
            LastAccessed = DateTime.UtcNow;
        }
    }
} 