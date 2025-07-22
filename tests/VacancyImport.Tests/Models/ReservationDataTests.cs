using System;
using Xunit;
using VacancyImport.Models;

namespace VacancyImport.Tests.Models;

public class FacilityMonthlyReservationTests
{
    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var data1 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data2 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        // Act & Assert
        Assert.True(data1.Equals(data2));
        Assert.True(data2.Equals(data1));
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var data1 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data2 = new FacilityMonthlyReservation
        {
            TenantId = 2, // 異なるテナントID
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data3 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 10, // 異なる施設ID
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data4 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2024, // 異なる年
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data5 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 11, // 異なる月
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data6 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "1" } // 異なる予約数
        };

        // Act & Assert
        Assert.False(data1.Equals(data2));
        Assert.False(data1.Equals(data3));
        Assert.False(data1.Equals(data4));
        Assert.False(data1.Equals(data5));
        Assert.False(data1.Equals(data6));
        Assert.False(data1.Equals(null));
        Assert.False(data1.Equals("not a facility monthly reservation"));
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHashCode()
    {
        // Arrange
        var data1 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data2 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        // Act
        var hash1 = data1.GetHashCode();
        var hash2 = data2.GetHashCode();

        // Assert - 同じ値を持つオブジェクトは同じハッシュコードを返すべき
        Assert.Equal(hash1, hash2);
        Assert.True(hash1 != 0, "ハッシュコードは0以外の値であるべき");
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHashCodes()
    {
        // Arrange
        var data1 = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        var data2 = new FacilityMonthlyReservation
        {
            TenantId = 2,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        // Act & Assert
        Assert.NotEqual(data1.GetHashCode(), data2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var data = new FacilityMonthlyReservation
        {
            TenantId = 1,
            FacilityId = 7,
            Year = 2023,
            Month = 12,
            ReservationCounts = new[] { "5", "3", "2" }
        };

        // Act
        var result = data.ToString();

        // Assert
        Assert.Contains("1", result);
        Assert.Contains("7", result);
        Assert.Contains("2023", result);
        Assert.Contains("12", result);
        Assert.Contains("5,3,2", result);
    }
} 