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

This file is ignored by Git. Alternatively, set `TESSIE_TOKEN` and `TESSIE_VIN`
environment variables; these take precedence over the file. Never commit credentials.

```sh
dotnet run --project pocs/TessieExplorer/TessieExplorer.csproj
```

Run the menu in an interactive terminal. Select one data category or all categories
from the menu. The state is Tessie's
cached state and may not be current. History requests use Tessie's default
timeframe and result limits; they do not necessarily represent all historical
records. Timestamps are shown in the computer's local timezone. Unavailable
fields are labelled rather than inferred.

Build and run the offline xUnit and Reqnroll behavior tests (no Tessie account is required):

```sh
dotnet build pocs/TessieExplorer/TessieExplorer.csproj
dotnet run --project tests/TessieExplorer.Tests/TessieExplorer.Tests.csproj
```
