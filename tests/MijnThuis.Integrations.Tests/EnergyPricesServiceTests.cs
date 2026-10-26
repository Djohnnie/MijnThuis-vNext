using MijnThuis.Integrations.Power;

namespace MijnThuis.Integrations.Tests;

public class EnergyPricesServiceTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    [Theory]
    [InlineData("2025-03-30", 23)]
    [InlineData("2025-06-15", 24)]
    [InlineData("2025-10-26", 25)]
    public void GetPeriodsInDay_AccountsForDaylightSavingTime(string date, int expectedHours)
    {
        Assert.Equal(expectedHours, EnergyPricesService.GetPeriodsInDay(DateTime.Parse(date), Brussels));
    }

    [Fact]
    public void MapEnergyPrices_FillsMissingPositionsWithPreviousPrice()
    {
        var period = new Period
        {
            Resolution = "PT60M",
            Points = [new Point { Position = 1, Price = 10m }, new Point { Position = 3, Price = 30m }]
        };

        var prices = EnergyPricesService.MapEnergyPrices(new DateTime(2025, 6, 15), period, factor: 1, periodsInDay: 24);

        Assert.Equal(24, prices.Count);
        Assert.Equal([10m, 10m, 30m, 30m], prices.Take(4).Select(p => p.Price));
        Assert.Equal(new DateTime(2025, 6, 15, 23, 0, 0), prices[^1].TimeStamp);
    }

    [Fact]
    public void MapEnergyPrices_SkipsMissingHourOnShortDay()
    {
        var period = new Period
        {
            Resolution = "PT60M",
            Points = Enumerable.Range(1, 23).Select(i => new Point { Position = i, Price = i }).ToList()
        };

        var prices = EnergyPricesService.MapEnergyPrices(new DateTime(2025, 3, 30), period, factor: 1, periodsInDay: 23);

        Assert.Equal(23, prices.Count);
        Assert.DoesNotContain(prices, p => p.TimeStamp.Hour == 2);
        Assert.Equal(3m, prices.Single(p => p.TimeStamp.Hour == 3).Price);
    }

    [Fact]
    public void MapEnergyPrices_SupportsQuarterHourPrices()
    {
        var period = new Period
        {
            Resolution = "PT15M",
            Points = Enumerable.Range(1, 96).Select(i => new Point { Position = i, Price = i }).ToList()
        };

        var prices = EnergyPricesService.MapEnergyPrices(new DateTime(2025, 10, 15), period, factor: 4, periodsInDay: 96);

        Assert.Equal(96, prices.Count);
        Assert.Equal(new DateTime(2025, 10, 15, 0, 15, 0), prices[1].TimeStamp);
        Assert.Equal(new DateTime(2025, 10, 15, 23, 45, 0), prices[^1].TimeStamp);
    }

    [Fact]
    public void MapEnergyPrices_ThrowsWhenThereAreTooManyPoints()
    {
        var period = new Period
        {
            Resolution = "PT60M",
            Points = Enumerable.Range(1, 25).Select(i => new Point { Position = i, Price = i }).ToList()
        };

        Assert.Throws<InvalidDataException>(() => EnergyPricesService.MapEnergyPrices(new DateTime(2025, 6, 15), period, 1, 24));
    }
}
