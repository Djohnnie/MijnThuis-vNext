using Microsoft.Extensions.Configuration;

namespace TessieExplorer;

public sealed record ExplorerConfiguration(string? Token, string? Vin)
{
    public static ExplorerConfiguration Load(string directory)
    {
        var environment = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return new ExplorerConfiguration(
            environment["TESSIE_TOKEN"] ?? configuration["Tessie:Token"],
            environment["TESSIE_VIN"] ?? configuration["Tessie:Vin"]);
    }
}
