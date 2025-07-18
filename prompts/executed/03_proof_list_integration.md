# ProofListService統合とSupabaseService修正プロンプト

## 📖 概要
既存のSupabaseServiceを修正してReservationChangeオブジェクトを返すようにし、ProofListServiceと連携して証跡CSV出力機能を統合する。

## 🎯 実装対象
- SupabaseService.UpdateReservationsAsyncメソッドの修正
- ReservationChangeクラスのModelsフォルダへの移動
- AppSettingsクラスにProofListSettings追加
- ProofListServiceの自動クリーンアップ機能実装

## 📋 詳細仕様

### 1. ReservationChangeクラスの移動

**ファイル**: `src/VacancyImport/Models/ReservationChange.cs`

```csharp
using System;

namespace VacancyImport.Models
{
    /// <summary>
    /// 予約データの変更情報
    /// </summary>
    public class ReservationChange
    {
        /// <summary>
        /// 変更種別 (New, Changed, Deleted)
        /// </summary>
        public string ChangeType { get; set; }
        
        /// <summary>
        /// 店舗ID
        /// </summary>
        public string StoreId { get; set; }
        
        /// <summary>
        /// 日付
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// 時間帯
        /// </summary>
        public string TimeSlot { get; set; }
        
        /// <summary>
        /// 変更前残数
        /// </summary>
        public int? OldRemain { get; set; }
        
        /// <summary>
        /// 変更後残数
        /// </summary>
        public int? NewRemain { get; set; }
        
        /// <summary>
        /// 更新日時
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ReservationChange()
        {
            UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// 変更種別を判定して作成
        /// </summary>
        public static ReservationChange CreateNew(string storeId, DateTime date, string timeSlot, int remain)
        {
            return new ReservationChange
            {
                ChangeType = "New",
                StoreId = storeId,
                Date = date,
                TimeSlot = timeSlot,
                OldRemain = null,
                NewRemain = remain
            };
        }

        /// <summary>
        /// 変更情報を作成
        /// </summary>
        public static ReservationChange CreateChanged(string storeId, DateTime date, string timeSlot, int oldRemain, int newRemain)
        {
            return new ReservationChange
            {
                ChangeType = "Changed",
                StoreId = storeId,
                Date = date,
                TimeSlot = timeSlot,
                OldRemain = oldRemain,
                NewRemain = newRemain
            };
        }

        /// <summary>
        /// 削除情報を作成
        /// </summary>
        public static ReservationChange CreateDeleted(string storeId, DateTime date, string timeSlot, int oldRemain)
        {
            return new ReservationChange
            {
                ChangeType = "Deleted",
                StoreId = storeId,
                Date = date,
                TimeSlot = timeSlot,
                OldRemain = oldRemain,
                NewRemain = null
            };
        }

        /// <summary>
        /// 文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"{ChangeType}: {StoreId} {Date:yyyy-MM-dd} {TimeSlot} ({OldRemain} → {NewRemain})";
        }
    }
}
```

### 2. SupabaseService修正

**ファイル**: `src/VacancyImport/Services/SupabaseService.cs` の `UpdateReservationsAsync` メソッド修正

```csharp
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
            .GroupBy(x => x.index / _settings.PerformanceSettings.DatabaseBatchSize)
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
                    reservation.Date,
                    reservation.TimeSlot,
                    reservation.Remain
                ));

                _logger.LogDebug($"新規作成: {reservation.StoreId} {reservation.Date:yyyy-MM-dd} {reservation.TimeSlot}");
            }
            else if (existingData.Remain != reservation.Remain)
            {
                // 更新
                existingData.Remain = reservation.Remain;
                existingData.UpdatedAt = DateTime.UtcNow;

                await _client
                    .From<RoomAvailability>()
                    .Where(x => x.Id == existingData.Id)
                    .Update(existingData);

                changes.Add(ReservationChange.CreateChanged(
                    reservation.StoreId,
                    reservation.Date,
                    reservation.TimeSlot,
                    existingData.Remain,
                    reservation.Remain
                ));

                _logger.LogDebug($"更新: {reservation.StoreId} {reservation.Date:yyyy-MM-dd} {reservation.TimeSlot} ({existingData.Remain} → {reservation.Remain})");
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
```

### 3. ProofListServiceの修正

**ファイル**: `src/VacancyImport/Services/ProofListService.cs` から `ReservationChange`クラス削除

```csharp
// ReservationChangeクラス定義を削除し、using文を追加
using VacancyImport.Models;

// クラス定義部分は既存のままで、ReservationChangeクラス定義のみ削除
```

### 4. AppSettingsクラス更新

**ファイル**: `src/VacancyImport/Configuration/AppSettings.cs` 更新

```csharp
// ... existing code ...

/// <summary>
/// プルーフリスト設定
/// </summary>
public ProofListSettings ProofListSettings { get; set; } = new ProofListSettings();

// ... existing code ...
```

### 5. ProofListServiceの自動クリーンアップ統合

**ファイル**: `src/VacancyImport/Services/ServiceHost.cs` の `ExecuteBusinessLogicAsync` メソッド修正

```csharp
private async Task ExecuteBusinessLogicAsync()
{
    var excelService = _serviceProvider.GetRequiredService<ExcelService>();
    var proofListService = _serviceProvider.GetRequiredService<ProofListService>();
    
    // ファイル更新チェック
    var hasUpdates = await excelService.CheckFileUpdatesAsync();

    if (hasUpdates)
    {
        _logger.LogInformation("ファイル更新を検出しました。データ処理を開始します");

        // 予約データを抽出
        var reservationData = await excelService.ExtractReservationDataAsync();

        // Supabaseにデータを送信し、変更情報を取得
        var supabaseService = _serviceProvider.GetRequiredService<SupabaseService>();
        var changes = await supabaseService.UpdateReservationsAsync(reservationData);

        // プルーフリストを生成（変更がある場合のみ）
        if (changes.Any())
        {
            var proofListPath = await proofListService.GenerateProofListAsync(changes);
            var summary = proofListService.GenerateSummary(changes);
            
            // LINE WORKSに通知
            var lineWorksService = _serviceProvider.GetRequiredService<LineWorksService>();
            await lineWorksService.SendNotificationAsync($"{summary}\n📄 プルーフリスト: {Path.GetFileName(proofListPath)}");
            
            _logger.LogInformation($"プルーフリストを生成しました: {proofListPath}");
        }
        else
        {
            _logger.LogInformation("変更がないため、プルーフリストは生成されませんでした");
        }

        _logger.LogInformation("データ処理が完了しました");
    }
    
    // 定期的なクリーンアップ（1日1回実行）
    await PerformPeriodicCleanupAsync();
}

private DateTime _lastCleanupDate = DateTime.MinValue;

private async Task PerformPeriodicCleanupAsync()
{
    var today = DateTime.Today;
    
    // 1日1回実行
    if (_lastCleanupDate < today)
    {
        try
        {
            _logger.LogInformation("プルーフリストの定期クリーンアップを開始します");
            
            var proofListService = _serviceProvider.GetRequiredService<ProofListService>();
            await proofListService.CleanupOldProofListsAsync();
            
            _lastCleanupDate = today;
            _logger.LogInformation("プルーフリストの定期クリーンアップが完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "プルーフリストのクリーンアップ中にエラーが発生しました");
        }
    }
}
```

### 6. LineWorksServiceの通知メッセージ改善

**ファイル**: `src/VacancyImport/Services/LineWorksService.cs` 更新

```csharp
/// <summary>
/// プルーフリスト用の詳細通知を送信
/// </summary>
/// <param name="summary">サマリー文字列</param>
/// <param name="proofListFileName">プルーフリストファイル名</param>
public async Task SendProofListNotificationAsync(string summary, string proofListFileName)
{
    try
    {
        var message = new
        {
            content = new
            {
                type = "text",
                text = $"📊 予約データ更新通知\n\n{summary}\n\n📄 証跡ファイル: {proofListFileName}\n⏰ 処理時刻: {DateTime.Now:yyyy/MM/dd HH:mm:ss}"
            }
        };

        await SendMessageAsync(message);
        _logger.LogInformation("プルーフリスト通知を送信しました");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "プルーフリスト通知の送信に失敗しました");
        throw;
    }
}

/// <summary>
/// エラー通知を送信
/// </summary>
/// <param name="errorMessage">エラーメッセージ</param>
/// <param name="errorCount">連続エラー回数</param>
public async Task SendErrorNotificationAsync(string errorMessage, int errorCount)
{
    try
    {
        var urgencyEmoji = errorCount >= 3 ? "🚨" : "⚠️";
        var message = new
        {
            content = new
            {
                type = "text",
                text = $"{urgencyEmoji} 予約システム連携エラー\n\n" +
                       $"エラー内容: {errorMessage}\n" +
                       $"連続エラー回数: {errorCount}\n" +
                       $"発生時刻: {DateTime.Now:yyyy/MM/dd HH:mm:ss}\n\n" +
                       (errorCount >= 3 ? "⚠️ 管理者による確認が必要です" : "")
            }
        };

        await SendMessageAsync(message);
        _logger.LogInformation($"エラー通知を送信しました (エラー回数: {errorCount})");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "エラー通知の送信に失敗しました");
        // エラー通知の失敗は例外を再スローしない
    }
}
```

## 🔍 検証手順

1. **ReservationChangeクラスの移動確認**:
   ```bash
   # Modelsフォルダにファイルが作成されていることを確認
   ls src/VacancyImport/Models/
   ```

2. **ビルド確認**:
   ```bash
   cd src/VacancyImport
   dotnet build
   ```

3. **ProofListService統合テスト**:
   ```bash
   # テストデータでプルーフリスト生成をテスト
   dotnet test --filter "ProofListService"
   ```

4. **コンソールモードでの動作確認**:
   ```bash
   dotnet run --configuration Debug -- --console
   ```

## ⚠️ 注意事項

- ReservationChangeクラスを移動する際は、既存のusing文を確認
- SupabaseServiceの戻り値型が変更されるため、呼び出し元の修正が必要
- ProofListServiceとSupabaseServiceの連携により、証跡生成が自動化される
- 自動クリーンアップは1日1回実行され、古いファイルを削除する

## 📚 参考ドキュメント

- [.NET のコレクション](https://learn.microsoft.com/ja-jp/dotnet/standard/collections/)
- [非同期プログラミング](https://learn.microsoft.com/ja-jp/dotnet/csharp/programming-guide/concepts/async/)

## 🎯 完了条件

- [ ] ReservationChangeクラスがModelsフォルダに移動されている
- [ ] SupabaseService.UpdateReservationsAsyncが変更情報を返すように修正されている
- [ ] ProofListServiceが統合されている
- [ ] AppSettingsにProofListSettingsが追加されている
- [ ] ServiceHostに自動クリーンアップ機能が実装されている
- [ ] LineWorksServiceの通知機能が強化されている
- [ ] ビルドエラーがない
- [ ] テストが正常に実行される 