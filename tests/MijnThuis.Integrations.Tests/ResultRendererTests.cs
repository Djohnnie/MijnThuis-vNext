using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Power;
using Spectre.Console;

namespace MijnThuis.Integrations.Tests;

public class ResultRendererTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateTable_CombinesSuccessAndErrorColumnsRegardlessOfRowOrder(bool errorFirst)
    {
        object overview = new PowerSwitchOverview(PowerSwitch.Tv, true, 123.45m);
        object error = new { Switch = PowerSwitch.Bureau, Error = "Device unavailable" };
        object?[] items = errorFirst ? [error, null, overview] : [overview, null, error];

        var table = ResultRenderer.CreateTable(items);
        var output = Render(table);

        Assert.Equal(4, table.Columns.Count);
        Assert.Equal(3, table.Rows.Count);
        Assert.Contains("Switch", output);
        Assert.Contains("IsOn", output);
        Assert.Contains("Power", output);
        Assert.Contains("Error", output);
        Assert.Contains("123.45", output);
        Assert.Contains("yes", output);
        Assert.Contains("Device unavailable", output);
        Assert.Contains("Tv", output);
        Assert.Contains("Bureau", output);
    }

    [Fact]
    public void CreateTable_PreservesHomogeneousObjectTables()
    {
        var table = ResultRenderer.CreateTable(new[]
        {
            new PowerSwitchOverview(PowerSwitch.Tv, true, 123.45m),
            new PowerSwitchOverview(PowerSwitch.Bureau, false, 67.89m)
        });
        var output = Render(table);

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Contains("123.45", output);
        Assert.Contains("67.89", output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateTable_HandlesMixedScalarsAndObjects(bool scalarFirst)
    {
        object overview = new PowerSwitchOverview(PowerSwitch.Tv, true, 123.45m);
        object?[] items = scalarFirst ? ["Ready", overview, null] : [overview, "Ready", null];

        var table = ResultRenderer.CreateTable(items);
        var output = Render(table);

        Assert.Single(table.Columns);
        Assert.Equal(3, table.Rows.Count);
        Assert.Contains("Ready", output);
        Assert.Contains("123.45", output);
    }

    [Fact]
    public void CreateTable_HandlesEmptyAndNullCollections()
    {
        var empty = ResultRenderer.CreateTable(Array.Empty<object>());
        var nulls = ResultRenderer.CreateTable(new object?[] { null, null });

        Assert.Single(empty.Columns);
        Assert.Empty(empty.Rows);
        Assert.Single(nulls.Columns);
        Assert.Equal(2, nulls.Rows.Count);
        Assert.Contains("0 item(s)", Render(empty));
        Assert.Contains("2 item(s)", Render(nulls));
    }

    private static string Render(Table table)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 160;
        console.Write(table);
        return writer.ToString();
    }
}
