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
        public string ChangeType { get; set; } = string.Empty;
        
        /// <summary>
        /// 店舗ID
        /// </summary>
        public string StoreId { get; set; } = string.Empty;
        
        /// <summary>
        /// 日付
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// 時間帯
        /// </summary>
        public string TimeSlot { get; set; } = string.Empty;
        
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