# 01: nullデータの送信防止とエラー時の適切な終了処理の実装

## 概要
ExcelServiceでnullデータがSupabaseに送信される問題を修正し、エラー時に適切にプログラムが終了するように改善します。

## 修正対象ファイル
- `src/VacancyImport/Services/ExcelService.cs`

## 実装要件

### 1. nullデータの送信防止
- `ExtractFacilityIdFromFileName`メソッドで施設IDが0の場合、そのファイルのデータを除外
- `ExtractMonthlyReservations`メソッドでfacility_idが0の場合は空のリストを返す
- ログに「施設IDが特定できないためスキップ」を出力

### 2. エラー時の適切な終了処理
- 重大エラー（設定エラー、データベース接続エラー等）時に`ServiceCriticalException`をスロー
- 個別ファイルのエラーは継続処理、全体の処理エラーは終了
- エラーログに「重大エラーのため処理を終了します」を出力

### 3. 施設IDマッピングの改善
- 現在の3拠点マッピングを維持
- マッピング外のファイルは処理しない（スキップ）
- スキップしたファイル数をログに出力

## 具体的な修正内容

### ExtractFacilityIdFromFileNameメソッドの修正
```csharp
private int ExtractFacilityIdFromFileName(string fileName)
{
    // 施設IDマッピング（3拠点のみ）
    var facilityMapping = new Dictionary<string, int>
    {
        { "ふじみの", 7 },
        { "みさと", 10 },
        { "いちかわ", 14 }
    };
    
    foreach (var mapping in facilityMapping)
    {
        if (fileName.Contains(mapping.Key))
        {
            _logger.LogDebug("施設IDを特定しました: {FacilityName} -> {FacilityId}", mapping.Key, mapping.Value);
            return mapping.Value;
        }
    }
    
    _logger.LogWarning("施設IDが特定できませんでした: {FileName}", fileName);
    return 0; // マッピングが見つからない場合
}
```

### ExtractMonthlyReservationsメソッドの修正
```csharp
private IEnumerable<FacilityMonthlyReservation> ExtractMonthlyReservations(IXLWorksheet worksheet, int facilityId)
{
    // facility_idが0の場合は空のリストを返す
    if (facilityId == 0)
    {
        _logger.LogWarning("施設IDが0のため、データ抽出をスキップします");
        return Enumerable.Empty<FacilityMonthlyReservation>();
    }
    
    // 既存の処理を継続...
    var results = new List<FacilityMonthlyReservation>();
    var currentYear = DateTime.Now.Year;
    
    // ... 既存のコード ...
    
    foreach (var group in monthlyGroups)
    {
        var year = group.Key.Year;
        var month = group.Key.Month;
        
        // ... 既存のコード ...
        
        var monthlyReservation = new FacilityMonthlyReservation
        {
            TenantId = 1, // ハードコード
            FacilityId = facilityId,
            Year = year,
            Month = month,
            ReservationCounts = reservationCounts
        };
        
        results.Add(monthlyReservation);
    }
    
    return results;
}
```

### ExtractMonthlyReservationsAsyncメソッドの修正
```csharp
public async Task<IEnumerable<FacilityMonthlyReservation>> ExtractMonthlyReservationsAsync(string filePath)
{
    // ... 既存のコード ...
    
    var allResults = new List<FacilityMonthlyReservation>();
    var successCount = 0;
    var errorCount = 0;
    var skipCount = 0; // スキップしたファイル数
    
    foreach (var file in unlockedFiles)
    {
        try
        {
            var fileName = Path.GetFileName(file);
            var facilityId = ExtractFacilityIdFromFileName(fileName);
            
            // 施設IDが0の場合はスキップ
            if (facilityId == 0)
            {
                skipCount++;
                _logger.LogInformation("施設IDが特定できないためスキップ: {FileName}", fileName);
                continue;
            }
            
            // ... 既存の処理 ...
            
            successCount++;
        }
        catch (Exception ex)
        {
            errorCount++;
            _logger.LogError(ex, "ファイル処理中にエラーが発生しました: {FilePath}", file);
        }
    }
    
    _logger.LogInformation("データ抽出完了: 成功{SuccessCount}件, エラー{ErrorCount}件, スキップ{SkipCount}件, 総予約データ{TotalReservations}件", 
        successCount, errorCount, skipCount, allResults.Count);
    
    return allResults;
}
```

## 期待される動作
1. マッピング外のファイルは処理されず、スキップされる
2. nullデータがSupabaseに送信されない
3. エラー時に適切なログが出力される
4. 処理統計が正確に記録される 