using System.Globalization;
using System.Net;
using Reqnroll;
using Spectre.Console;

namespace TessieExplorer.Tests;

[Binding]
public sealed class VehicleDataSteps
{
    private const string Vin = "5YJYGDEE0LF000001";
    private VehicleSection section;
    private string response = "";
    private HttpStatusCode status = HttpStatusCode.OK;
    private string output = "";
    private Exception? failure;

    [Given("Tessie returns the following response for {string}:")]
    public void GivenTessieReturnsAResponse(string sectionName, string body)
    {
        section = Enum.Parse<VehicleSection>(sectionName);
        response = body;
    }

    [Given("Tessie returns HTTP {int} with the body {string} for {string}")]
    public void GivenTessieReturnsAnError(int statusCode, string body, string sectionName)
    {
        section = Enum.Parse<VehicleSection>(sectionName);
        response = body;
        status = (HttpStatusCode)statusCode;
    }

    [When("the explorer retrieves the vehicle data")]
    public async Task WhenTheExplorerRetrievesTheVehicleData()
    {
        using var http = new HttpClient(new StubHandler(response, status)) { BaseAddress = new Uri("https://api.tessie.com/") };
        var client = new TessieClient(http, "test-token", Vin);
        try
        {
            var data = await client.GetAsync(section, TestContext.Current.CancellationToken);
            using var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer)
            });
            new VehiclePresenter(console).Display(section, data, Vin);
            output = writer.ToString();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            failure = ex;
        }
    }

    [Then("the console displays:")]
    public void ThenTheConsoleDisplays(DataTable expected)
    {
        Assert.Null(failure);
        foreach (var row in expected.Rows)
        {
            Assert.Contains(row["text"], output);
        }
    }

    [Then("the console does not display {string}")]
    public void ThenTheConsoleDoesNotDisplay(string text)
    {
        Assert.DoesNotContain(text, output);
    }

    [Then("the console displays the local time for {long}")]
    public void ThenTheConsoleDisplaysLocalTime(long seconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        Assert.Contains(local, output);
    }

    [Then("the request fails with {string}")]
    public void ThenTheRequestFailsWith(string text)
    {
        Assert.NotNull(failure);
        Assert.Contains(text, failure.Message);
    }

    [Then("the failure does not expose {string}")]
    public void ThenTheFailureDoesNotExpose(string text)
    {
        Assert.NotNull(failure);
        Assert.DoesNotContain(text, failure.Message);
    }

    private sealed class StubHandler(string body, HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });
        }
    }
}
