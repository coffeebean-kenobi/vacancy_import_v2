using System;
using Xunit;
using VacancyImport.Models;

namespace VacancyImport.Tests.Models;

public class ReservationDataTests
{
    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var data1 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5,
            UpdatedAt = DateTime.Now,
            FilePath = "path/to/file.xlsx",
            ChangeType = ChangeType.New
        };

        var data2 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5,
            UpdatedAt = DateTime.Now.AddMinutes(5), // 異なる更新時間
            FilePath = "different/path.xlsx", // 異なるファイルパス
            ChangeType = ChangeType.Changed // 異なる変更種別
        };

        // Act & Assert
        Assert.True(data1.Equals(data2));
        Assert.True(data2.Equals(data1));
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var data1 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5
        };

        var data2 = new ReservationData
        {
            StoreId = "store2", // 異なる店舗ID
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5
        };

        var data3 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 2), // 異なる日付
            TimeSlot = "10:00-11:00",
            Remain = 5
        };

        var data4 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "11:00-12:00", // 異なる時間帯
            Remain = 5
        };

        var data5 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 3 // 異なる残り枠数
        };

        // Act & Assert
        Assert.False(data1.Equals(data2));
        Assert.False(data1.Equals(data3));
        Assert.False(data1.Equals(data4));
        Assert.False(data1.Equals(data5));
        Assert.False(data1.Equals(null));
        Assert.False(data1.Equals("not a reservation data"));
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHashCode()
    {
        // Arrange
        var data1 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5
        };

        var data2 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5,
            UpdatedAt = DateTime.Now, // 異なる更新時間
            FilePath = "path/to/file.xlsx", // 追加のプロパティ
            ChangeType = ChangeType.New // 追加のプロパティ
        };

        // Act
        var hashCode1 = data1.GetHashCode();
        var hashCode2 = data2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHashCodes()
    {
        // Arrange
        var data1 = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5
        };

        var data2 = new ReservationData
        {
            StoreId = "store2", // 異なる店舗ID
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5
        };

        // Act
        var hashCode1 = data1.GetHashCode();
        var hashCode2 = data2.GetHashCode();

        // Assert
        Assert.NotEqual(hashCode1, hashCode2);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var data = new ReservationData
        {
            StoreId = "store1",
            Date = new DateOnly(2023, 12, 1),
            TimeSlot = "10:00-11:00",
            Remain = 5,
            ChangeType = ChangeType.New
        };

        // Act
        var result = data.ToString();

        // Assert
        Assert.Equal("store1 - 2023/12/01 10:00-11:00 - 残り5枠 (New)", result);
    }
} 