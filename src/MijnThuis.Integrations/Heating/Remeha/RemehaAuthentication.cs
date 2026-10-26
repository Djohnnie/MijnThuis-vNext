using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Authentication;
using MijnThuis.Integrations.Common.Exceptions;

namespace MijnThuis.Integrations.Heating.Remeha;

/// <summary>
/// Acquires an access token for the Remeha Home API by automating the Azure AD B2C
/// authorization code flow (with PKCE) that the Remeha Home app uses.
/// </summary>
internal sealed class RemehaTokenProvider(IOptions<HeatingOptions> options, TimeProvider timeProvider)
    : CachedTokenProvider<string>(timeProvider)
{
    private const string Operation = "Authenticate";
    private const string Tenant = "bdrb2cprod.onmicrosoft.com";
    private const string Policy = "B2C_1A_RPSignUpSignInNewRoomV3.1";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    protected override async Task<(string Token, DateTimeOffset ExpiresAt)> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var authBase = settings.AuthBaseAddress.TrimEnd('/');
        var cookies = new CookieContainer();

        // Two handlers share the same cookie jar: the login steps follow redirects, the final
        // confirmation step must not, because the authorization code is in its redirect location.
        using var client = new HttpClient(new SocketsHttpHandler { CookieContainer = cookies }) { Timeout = Timeout };
        using var noRedirectClient = new HttpClient(new SocketsHttpHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { Timeout = Timeout };

        var state = CreateRandomId(32);
        var codeVerifier = CreateRandomId(64);
        var codeChallenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));

        // 1. Start the authorization flow, which sets the CSRF cookie and returns the request id.
        var authorizeUri = $"{authBase}/{Tenant}/oauth2/v2.0/authorize?response_type=code&client_id={Escape(settings.AuthClientId)}" +
            $"&scope={Escape(settings.AuthScope)}&state={state}&p={Policy}&brand=remeha&prompt=login&signup=False" +
            $"&redirect_uri={Escape(settings.AuthRedirectUri)}&lang=en&nonce=defaultNonce&code_challenge={codeChallenge}&code_challenge_method=S256";

        using var authorizeResponse = await client.GetAsync(authorizeUri, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(authorizeResponse, "authorize");

        var requestId = authorizeResponse.Headers.TryGetValues("X-Request-ID", out var values) ? values.FirstOrDefault() : null;
        var csrfToken = cookies.GetCookies(new Uri(authBase)).FirstOrDefault(x => x.Name == "x-ms-cpim-csrf")?.Value;

        if (requestId is null || csrfToken is null)
        {
            throw Failure("The login page did not return a request id or CSRF token.");
        }

        var stateProperties = Convert.ToBase64String(Encoding.ASCII.GetBytes($$"""{"TID":"{{requestId}}"}"""));

        // 2. Submit the credentials.
        using var credentialsRequest = new HttpRequestMessage(HttpMethod.Post, $"{authBase}/{Tenant}/{Policy}/SelfAsserted?tx=StateProperties={stateProperties}&p={Policy}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["request_type"] = "RESPONSE",
                ["signInName"] = settings.AuthUsername,
                ["password"] = settings.AuthPassword
            })
        };
        credentialsRequest.Headers.Add("x-csrf-token", csrfToken);

        using var credentialsResponse = await client.SendAsync(credentialsRequest, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(credentialsResponse, "submit credentials");

        // 3. Confirm the sign-in; the redirect location contains the authorization code.
        using var confirmResponse = await noRedirectClient.GetAsync(
            $"{authBase}/{Tenant}/{Policy}/api/CombinedSigninAndSignup/confirmed?rememberMe=false&csrf_token={Escape(csrfToken)}&tx=StateProperties={stateProperties}&p={Policy}",
            cancellationToken).ConfigureAwait(false);

        var location = confirmResponse.Headers.Location;
        var authorizationCode = location is null ? null : HttpUtility.ParseQueryString(location.IsAbsoluteUri ? location.Query : new Uri(new Uri(authBase), location).Query)["code"];

        if (string.IsNullOrEmpty(authorizationCode))
        {
            throw Failure("No authorization code was returned. Check the username and password.");
        }

        // 4. Exchange the authorization code for an access token.
        using var tokenResponse = await client.PostAsync($"{authBase}/{Tenant}/oauth2/v2.0/token?p={Policy}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = settings.AuthRedirectUri,
            ["code_verifier"] = codeVerifier,
            ["client_id"] = settings.AuthClientId
        }), cancellationToken).ConfigureAwait(false);
        EnsureSuccess(tokenResponse, "request token");

        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token?.AccessToken))
        {
            throw Failure("The token endpoint did not return an access token.");
        }

        return (token.AccessToken, DateTimeOffset.FromUnixTimeSeconds(token.ExpiresOn));
    }

    private static string CreateRandomId(int byteCount) => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteCount));

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static void EnsureSuccess(HttpResponseMessage response, string step)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw Failure($"Login step '{step}' failed with status code {(int)response.StatusCode}.");
        }
    }

    private static IntegrationAuthenticationException Failure(string message) => new(IntegrationNames.Heating, Operation, message);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_on")]
        public long ExpiresOn { get; set; }
    }
}

internal sealed class RemehaAuthenticationHandler(RemehaTokenProvider tokenProvider) : TokenAuthenticationHandler<string>(tokenProvider)
{
    protected override void ApplyToken(HttpRequestMessage request, string token)
        => request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
}
