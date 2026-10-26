using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Http;

namespace MijnThuis.Integrations.Power;

public sealed record EnergyPricesForDate(DateTime Date, IReadOnlyList<EnergyPrice> Prices);

/// <param name="TimeStamp">Local start time of the period.</param>
/// <param name="Price">Day-ahead price in EUR/MWh.</param>
public sealed record EnergyPrice(DateTime TimeStamp, decimal Price);

public interface IEnergyPricesService
{
    Task<EnergyPricesForDate> GetEnergyPricesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Day-ahead energy prices from the ENTSO-E transparency platform (https://transparency.entsoe.eu).
/// </summary>
internal sealed class EnergyPricesService(
    HttpClient httpClient,
    IOptions<EnergyPricesOptions> options,
    TimeProvider timeProvider,
    ILogger<EnergyPricesService> logger) : HttpIntegrationServiceBase(httpClient, logger), IEnergyPricesService
{
    private const string DayAheadPricesDocumentType = "A44";
    private const string QuarterHourResolution = "PT15M";

    // From 2025-10-01 onwards, day-ahead prices are published per 15 minutes instead of per hour.
    private static readonly DateTime QuarterHourPricesStart = new(2025, 10, 1);
    private static readonly XmlSerializer Serializer = new(typeof(PublicationMarketDocument));

    protected override string IntegrationName => IntegrationNames.EnergyPrices;

    public Task<EnergyPricesForDate> GetEnergyPricesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        const string operation = "Get energy prices";
        date = date.Date;

        return ExecuteAsync(operation, async ct =>
        {
            var settings = options.Value;
            var start = string.Create(CultureInfo.InvariantCulture, $"{date:yyyyMMdd}0000");
            var end = string.Create(CultureInfo.InvariantCulture, $"{date:yyyyMMdd}2359");
            var area = Uri.EscapeDataString(settings.AreaCode);
            var uri = $"{settings.BaseAddress.TrimEnd('?')}?documentType={DayAheadPricesDocumentType}&in_Domain={area}&out_Domain={area}" +
                $"&periodStart={start}&periodEnd={end}&securityToken={Uri.EscapeDataString(settings.ApiKey)}";

            using var response = await HttpClient.GetAsync(uri, ct);
            await response.EnsureSuccessAsync(IntegrationName, operation, ct);

            var document = XDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            // ENTSO-E answers with an acknowledgement document (instead of an error status) when there is no data.
            if (document.Root?.Name.LocalName == "Acknowledgement_MarketDocument")
            {
                var reason = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "text")?.Value ?? "no reason given";
                throw ResponseError(operation, $"No energy prices available for {date:yyyy-MM-dd}: {reason}");
            }

            var result = Deserialize(document);
            var period = result.TimeSeries.FirstOrDefault()?.Periods.FirstOrDefault()
                ?? throw ResponseError(operation, "The response did not contain any price periods.");

            var factor = date >= QuarterHourPricesStart ? 4 : 1;
            var periodsInDay = GetPeriodsInDay(date, timeProvider.LocalTimeZone) * factor;

            return new EnergyPricesForDate(date, MapEnergyPrices(date, period, factor, periodsInDay));
        }, cancellationToken);
    }

    /// <summary>
    /// Maps ENTSO-E points to prices per period. Missing positions (ENTSO-E omits repeated prices) are filled with the
    /// previous price, and days with a daylight saving time transition (23 or 25 hours) are mapped to the correct local time.
    /// </summary>
    internal static List<EnergyPrice> MapEnergyPrices(DateTime date, Period period, int factor, int periodsInDay)
    {
        var minutesPerPeriod = factor == 1 ? 60 : 15;
        var sortedPoints = period.Points
            .Select(x => new Point
            {
                Position = period.Resolution == QuarterHourResolution && factor == 1 ? (x.Position - 1) / 4 : x.Position - 1,
                Price = x.Price
            })
            .OrderBy(x => x.Position)
            .ToList();

        if (sortedPoints.Count == 0)
        {
            throw new InvalidDataException($"{date:yyyy-MM-dd} has no price points.");
        }

        if (sortedPoints.Count > periodsInDay)
        {
            throw new InvalidDataException($"{date:yyyy-MM-dd} should have {periodsInDay} periods, but the service returned {sortedPoints.Count} points.");
        }

        for (var periodInDay = 0; periodInDay < periodsInDay; periodInDay++)
        {
            if (periodInDay >= sortedPoints.Count)
            {
                sortedPoints.Add(new Point { Position = periodInDay, Price = sortedPoints[periodInDay - 1].Price });
            }
            else if (sortedPoints[periodInDay].Position > periodInDay)
            {
                var previousPrice = periodInDay == 0 ? sortedPoints[0].Price : sortedPoints[periodInDay - 1].Price;
                sortedPoints.Insert(periodInDay, new Point { Position = periodInDay, Price = previousPrice });
            }
        }

        var energyPrices = new List<EnergyPrice>(periodsInDay);
        for (var periodInDay = 0; periodInDay < periodsInDay; periodInDay++)
        {
            // On a 23-hour day the hour 02:00-03:00 doesn't exist; on a 25-hour day 02:00-03:00 occurs twice.
            var periodOffset = (periodsInDay / factor) switch
            {
                23 => periodInDay < 2 * factor ? periodInDay : periodInDay + factor,
                24 => periodInDay,
                25 => periodInDay < 3 * factor ? periodInDay : periodInDay - factor,
                _ => throw new InvalidDataException($"Invalid number of periods in day: {periodsInDay}")
            };

            energyPrices.Add(new EnergyPrice(date.AddMinutes(periodOffset * minutesPerPeriod), sortedPoints[periodInDay].Price));
        }

        return energyPrices
            .GroupBy(price => price.TimeStamp)
            .Select(group => group.Last())
            .OrderBy(price => price.TimeStamp)
            .ToList();
    }

    /// <summary>
    /// Number of hours in the given local day: 23 or 25 on daylight saving time transitions, 24 otherwise.
    /// </summary>
    internal static int GetPeriodsInDay(DateTime date, TimeZoneInfo timeZone)
    {
        var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var end = start.AddDays(1);
        var utcStart = start - timeZone.GetUtcOffset(start);
        var utcEnd = end - timeZone.GetUtcOffset(end);

        return (int)Math.Round((utcEnd - utcStart).TotalHours);
    }

    private static PublicationMarketDocument Deserialize(XDocument document)
    {
        try
        {
            using var reader = document.CreateReader();
            return Serializer.Deserialize(reader) as PublicationMarketDocument
                ?? throw new InvalidDataException("The response could not be parsed as a publication market document.");
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException($"The response could not be parsed: {ex.InnerException?.Message ?? ex.Message}", ex);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"The response is not valid XML: {ex.Message}", ex);
        }
    }
}

[XmlRoot("Publication_MarketDocument", Namespace = "urn:iec62325.351:tc57wg16:451-3:publicationdocument:7:3")]
public sealed class PublicationMarketDocument
{
    [XmlElement("revisionNumber")]
    public int RevisionNumber { get; set; }

    [XmlElement("TimeSeries")]
    public List<TimeSeries> TimeSeries { get; set; } = [];
}

public sealed class TimeSeries
{
    [XmlElement("Period")]
    public List<Period> Periods { get; set; } = [];
}

public sealed class Period
{
    [XmlElement("resolution")]
    public string Resolution { get; set; } = string.Empty;

    [XmlElement("Point")]
    public List<Point> Points { get; set; } = [];
}

public sealed class Point
{
    [XmlElement("position")]
    public int Position { get; set; }

    [XmlElement("price.amount")]
    public decimal Price { get; set; }
}
