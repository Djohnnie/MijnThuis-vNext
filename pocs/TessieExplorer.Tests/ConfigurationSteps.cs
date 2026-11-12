using Reqnroll;

namespace TessieExplorer.Tests;

[Binding]
public sealed class ConfigurationSteps : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> originalEnvironment = new();
    private ExplorerConfiguration? configuration;
    private Exception? failure;

    public ConfigurationSteps()
    {
        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "TESSIE_TOKEN", "TESSIE_VIN", "Tessie__Token", "Tessie__Vin" })
        {
            originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Given("the {string} configuration file contains:")]
    public void GivenTheConfigurationFileContains(string name, string body)
    {
        File.WriteAllText(Path.Combine(directory, name), body);
    }

    [Given("the environment contains:")]
    public void GivenTheEnvironmentContains(DataTable values)
    {
        foreach (var row in values.Rows)
        {
            Environment.SetEnvironmentVariable(row["name"], row["value"]);
        }
    }

    [When("the configuration is loaded")]
    public void WhenTheConfigurationIsLoaded()
    {
        try
        {
            configuration = ExplorerConfiguration.Load(directory);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
        {
            failure = ex;
        }
    }

    [Then("the token is {string} and the VIN is {string}")]
    public void ThenTheCredentialsAre(string token, string vin)
    {
        Assert.Null(failure);
        Assert.NotNull(configuration);
        Assert.Equal(token, configuration.Token);
        Assert.Equal(vin, configuration.Vin);
    }

    [Then("the token and VIN are unavailable")]
    public void ThenTheCredentialsAreUnavailable()
    {
        Assert.Null(failure);
        Assert.NotNull(configuration);
        Assert.Null(configuration.Token);
        Assert.Null(configuration.Vin);
    }

    [Then("configuration loading fails")]
    public void ThenConfigurationLoadingFails()
    {
        Assert.NotNull(failure);
    }

    public void Dispose()
    {
        foreach (var entry in originalEnvironment)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
        Directory.Delete(directory, recursive: true);
    }
}
