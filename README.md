# MijnThuis-vNext
A new and improved MijnThuis

## Tessie vehicle explorer

The read-only console requires the .NET 11 SDK, a Tessie access token from
[Tessie developer settings](https://developer.tessie.com/), and the VIN of a
vehicle accessible to that account. It does not send vehicle commands.

From the repository root, create a local `appsettings.Development.json`:

```json
{
  "Tessie": {
    "Token": "your-access-token",
    "Vin": "your-17-character-vin"
  }
}
```

Configuration is loaded from the working directory: optional `appsettings.json`,
then optional `appsettings.Development.json`, and then environment variables.
Later sources override earlier ones per setting. The development file is ignored
by Git; do not put credentials in the tracked `appsettings.json`.

Use the standard `Tessie__Token` and `Tessie__Vin` environment variables, or
the existing `TESSIE_TOKEN` and `TESSIE_VIN` aliases. The aliases take precedence
over the standard names when both are set. Never commit credentials.

```sh
dotnet run --project pocs/TessieExplorer/TessieExplorer.csproj
```

Run the menu in an interactive terminal. Select one data category or all categories
from the menu. The state is Tessie's
cached state and may not be current. History requests use Tessie's default
timeframe and result limits; they do not necessarily represent all historical
records. Timestamps are shown in the computer's local timezone. Unavailable
fields are labelled rather than inferred.

The `pocs/Pocs.slnx` solution groups each POC and its tests in a descriptive
solution folder. Add future POCs to their own solution folders.

Build the POC solution and run the offline xUnit and Reqnroll behavior tests
(no Tessie account is required):

```sh
dotnet build pocs/Pocs.slnx
dotnet run --project pocs/TessieExplorer/TessieExplorer.Tests/TessieExplorer.Tests.csproj
```
