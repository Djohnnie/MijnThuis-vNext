using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Authentication;
using MijnThuis.Integrations.Common.Exceptions;

namespace MijnThuis.Integrations.Solar.SolarEdge;

internal sealed record SolarEdgeTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

internal interface ISolarEdgeTokenStore
{
    Task<SolarEdgeTokens?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(SolarEdgeTokens tokens, CancellationToken cancellationToken);
}

internal sealed class SolarEdgeTokenStore(IOptions<SolarOptions> options) : ISolarEdgeTokenStore
{
    internal string TokenPath
    {
        get
        {
            var settings = options.Value;
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{settings.BaseAddress.TrimEnd('/')}|{settings.ClientId}|{settings.SiteId}")));
            var directory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new IntegrationConfigurationException(IntegrationNames.Solar, "Store OAuth tokens",
                    "The current user has no local application-data directory for persisting OAuth tokens.");
            }

            return Path.Combine(directory, "MijnThuis", "SolarEdge", $"{id}.json");
        }
    }

    public async Task<SolarEdgeTokens?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var file = File.OpenRead(TokenPath);
            return await JsonSerializer.DeserializeAsync<SolarEdgeTokens>(file, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("The SolarEdge OAuth token file is empty. Reconnect SolarEdge from the CLI.");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task SaveAsync(SolarEdgeTokens tokens, CancellationToken cancellationToken)
    {
        var path = TokenPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                await JsonSerializer.SerializeAsync(file, tokens, cancellationToken: cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

internal sealed class SolarEdgeTokenProvider(
    IHttpClientFactory clientFactory, IOptions<SolarOptions> options,
    ISolarEdgeTokenStore tokenStore, TimeProvider timeProvider)
    : CachedTokenProvider<string>(timeProvider), ISolarAuthorizationService
{
    internal const string OAuthClientName = "SolarEdge.OAuth";
    private readonly TimeProvider _timeProvider = timeProvider;
    private string? _pendingExternalId;

    public Uri GetAuthorizationUri()
    {
        var settings = OAuthSettings();
        _pendingExternalId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return new Uri($"https://connect.solaredge.com/authorize?client_id={Uri.EscapeDataString(settings.ClientId!)}&external_id={_pendingExternalId}");
    }

    public async Task AuthorizeAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        var settings = OAuthSettings();
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
        {
            throw Failure("Paste the complete redirect URL returned after approving SolarEdge access.");
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        if (string.IsNullOrWhiteSpace(query["code"]) || query["site_id"] != settings.SiteId
            || _pendingExternalId is null || query["external_id"] != _pendingExternalId)
        {
            throw Failure("The redirect must contain an authorization code, the configured SOLAR_SITE_ID, and the external_id from the current connection link.");
        }

        await ExchangeAsync(new
        {
            grant_type = "authorization_code",
            code = query["code"],
            client_id = settings.ClientId,
            client_secret = settings.ClientSecret
        }, cancellationToken);
        _pendingExternalId = null;
        Invalidate();
    }

    protected override async Task<(string Token, DateTimeOffset ExpiresAt)> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var settings = OAuthSettings();
        var tokens = await tokenStore.LoadAsync(cancellationToken)
            ?? throw Failure("SolarEdge V2 requires homeowner consent. Use 'Connect SolarEdge (OAuth)' in the Solar CLI menu first.");

        if (tokens.ExpiresAt - TimeSpan.FromMinutes(1) <= _timeProvider.GetUtcNow())
        {
            tokens = await ExchangeAsync(new
            {
                grant_type = "refresh_token",
                refresh_token = tokens.RefreshToken,
                client_id = settings.ClientId,
                client_secret = settings.ClientSecret
            }, cancellationToken);
        }

        return (tokens.AccessToken, tokens.ExpiresAt);
    }

    private SolarOptions OAuthSettings()
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw Failure("Configure SOLAR_OAUTH_CLIENT_ID and SOLAR_OAUTH_CLIENT_SECRET from a Site Access application in developer.solaredge.com. Fleet keys do not use this consent step.");
        }

        return settings;
    }

    private async Task<SolarEdgeTokens> ExchangeAsync(object body, CancellationToken cancellationToken)
    {
        using var client = clientFactory.CreateClient(OAuthClientName);
        using var response = await client.PostAsJsonAsync("oauth2/token", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Token exchanges and rotating refresh tokens must never be automatically replayed.
            throw Failure($"SolarEdge OAuth exchange failed ({(int)response.StatusCode}). Check the client credentials or reconnect if the code/refresh token has expired or was already used.");
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken) || result.ExpiresIn <= 0)
        {
            throw Failure("SolarEdge OAuth returned an incomplete token response.");
        }

        var tokens = new SolarEdgeTokens(result.AccessToken, result.RefreshToken, _timeProvider.GetUtcNow().AddSeconds(result.ExpiresIn));
        // The old refresh token is now invalid: persist its replacement before returning to the caller.
        await tokenStore.SaveAsync(tokens, CancellationToken.None);
        return tokens;
    }

    private static IntegrationAuthenticationException Failure(string message)
        => new(IntegrationNames.Solar, "Authenticate", message);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

internal sealed class SolarEdgeAuthenticationHandler(IOptions<SolarOptions> options, SolarEdgeTokenProvider tokenProvider)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!string.IsNullOrWhiteSpace(settings.AuthToken))
        {
            request.Headers.Remove("X-API-Key");
            request.Headers.Add("X-API-Key", settings.AuthToken);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(cancellationToken));
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            response.Dispose();
            throw new IntegrationAuthenticationException(IntegrationNames.Solar, "Access SolarEdge V2",
                "Access denied (403). Check the site grant and SITE_DATA/DEVICE_DATA scopes. Power flow and per-destination charts additionally require a Business Pro or Enterprise subscription.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new IntegrationAuthenticationException(IntegrationNames.Solar, "Access SolarEdge V2",
                "Access denied (401). V1 API keys are not accepted by V2. Check the Fleet key or reconnect OAuth if access was revoked.");
        }

        return response;
    }
}
