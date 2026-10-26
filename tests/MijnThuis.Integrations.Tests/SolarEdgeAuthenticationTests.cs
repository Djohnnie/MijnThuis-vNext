using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common.Exceptions;
using MijnThuis.Integrations.Solar;
using MijnThuis.Integrations.Solar.SolarEdge;

namespace MijnThuis.Integrations.Tests;

public class SolarEdgeAuthenticationTests
{
    [Fact]
    public async Task FleetAuthentication_SendsOnlyV2Header()
    {
        var settings = Options.Create(new SolarOptions { SiteId = "42", AuthToken = "fleet-key" });
        using var fixture = new AuthFixture(settings);
        using var handler = new SolarEdgeAuthenticationHandler(settings, fixture.Provider)
        {
            InnerHandler = new Handler(request =>
            {
                Assert.Equal("fleet-key", Assert.Single(request.Headers.GetValues("X-API-Key")));
                Assert.Null(request.Headers.Authorization);
                Assert.DoesNotContain("api_key", request.RequestUri!.Query);
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://solar.example/v2/sites/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, fixture.ExchangeCount);
    }

    [Fact]
    public async Task Consent_ValidatesCallbackAndPersistsTokens()
    {
        using var fixture = new AuthFixture();
        var authorize = fixture.Provider.GetAuthorizationUri();
        var query = HttpUtility.ParseQueryString(authorize.Query);
        Assert.Equal("https://connect.solaredge.com/authorize", authorize.GetLeftPart(UriPartial.Path));
        Assert.Equal("client-id", query["client_id"]);
        Assert.False(string.IsNullOrWhiteSpace(query["external_id"]));

        await fixture.Provider.AuthorizeAsync($"https://callback.example/?code=code-1&site_id=42&external_id={query["external_id"]}");

        Assert.NotNull(fixture.Store.Tokens);
        Assert.Equal("new-refresh", fixture.Store.Tokens.RefreshToken);
        Assert.Equal("new-access", await fixture.Provider.GetTokenAsync(CancellationToken.None));
        Assert.Equal(1, fixture.ExchangeCount);
        Assert.Equal("authorization_code", fixture.LastBody.GetProperty("grant_type").GetString());
        Assert.Equal("code-1", fixture.LastBody.GetProperty("code").GetString());
        Assert.Equal("client-secret", fixture.LastBody.GetProperty("client_secret").GetString());
    }

    [Theory]
    [InlineData("43", true)]
    [InlineData("42", false)]
    public async Task Consent_RejectsWrongSiteOrCorrelationBeforeSendingCode(string site, bool validExternalId)
    {
        using var fixture = new AuthFixture();
        var authorize = fixture.Provider.GetAuthorizationUri();
        var external = HttpUtility.ParseQueryString(authorize.Query)["external_id"];

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(() => fixture.Provider.AuthorizeAsync(
            $"https://callback.example/?code=code-1&site_id={site}&external_id={(validExternalId ? external : "wrong")}"));

        Assert.Equal(0, fixture.ExchangeCount);
    }

    [Fact]
    public async Task ExpiredToken_RefreshesOnceForConcurrentCallsAndSavesRotation()
    {
        using var fixture = new AuthFixture();
        fixture.Store.Tokens = new SolarEdgeTokens("expired", "old-refresh", fixture.Clock.GetUtcNow().AddMinutes(-1));

        var tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.Provider.GetTokenAsync(CancellationToken.None)));

        Assert.All(tokens, token => Assert.Equal("new-access", token));
        Assert.Equal(1, fixture.ExchangeCount);
        Assert.Equal("refresh_token", fixture.LastBody.GetProperty("grant_type").GetString());
        Assert.Equal("old-refresh", fixture.LastBody.GetProperty("refresh_token").GetString());
        Assert.Equal("new-refresh", fixture.Store.Tokens.RefreshToken);

        fixture.Clock.Now = fixture.Clock.Now.AddHours(3);
        await fixture.Provider.GetTokenAsync(CancellationToken.None);
        Assert.Equal(2, fixture.ExchangeCount);
        Assert.Equal("new-refresh", fixture.LastBody.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task Restart_ReusesSavedAccessToken()
    {
        using var fixture = new AuthFixture();
        fixture.Store.Tokens = new SolarEdgeTokens("saved-access", "saved-refresh", fixture.Clock.GetUtcNow().AddHours(1));

        var token = await fixture.Provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("saved-access", token);
        Assert.Equal(0, fixture.ExchangeCount);
    }

    [Fact]
    public async Task OAuthAuthentication_SendsBearerToken()
    {
        using var fixture = new AuthFixture();
        fixture.Store.Tokens = new SolarEdgeTokens("saved-access", "saved-refresh", fixture.Clock.GetUtcNow().AddHours(1));
        using var handler = new SolarEdgeAuthenticationHandler(fixture.Settings, fixture.Provider)
        {
            InnerHandler = new Handler(request =>
            {
                Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
                Assert.Equal("saved-access", request.Headers.Authorization.Parameter);
                Assert.False(request.Headers.Contains("X-API-Key"));
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://solar.example/v2/sites/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingConsent_GivesActionableError()
    {
        using var fixture = new AuthFixture();

        var error = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => fixture.Provider.GetTokenAsync(CancellationToken.None));

        Assert.Contains("Connect SolarEdge (OAuth)", error.Message);
        Assert.Equal(0, fixture.ExchangeCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "V1 API keys")]
    [InlineData(HttpStatusCode.Forbidden, "Business Pro")]
    public async Task RejectedAccess_ExplainsMigrationOrTierRequirement(HttpStatusCode status, string expected)
    {
        using var fixture = new AuthFixture(Options.Create(new SolarOptions { SiteId = "42", AuthToken = "key" }));
        using var handler = new SolarEdgeAuthenticationHandler(fixture.Settings, fixture.Provider)
        {
            InnerHandler = new Handler(_ => new HttpResponseMessage(status))
        };
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => client.GetAsync("https://solar.example/v2/sites/42"));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task FailedRefresh_DoesNotOverwriteStoredTokens()
    {
        using var fixture = new AuthFixture();
        var saved = new SolarEdgeTokens("expired", "old-refresh", fixture.Clock.GetUtcNow().AddMinutes(-1));
        fixture.Store.Tokens = saved;
        fixture.ResponseStatus = HttpStatusCode.BadRequest;

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(() => fixture.Provider.GetTokenAsync(CancellationToken.None));

        Assert.Same(saved, fixture.Store.Tokens);
        Assert.Equal(1, fixture.ExchangeCount);
    }

    [Fact]
    public void DependencyInjection_AcceptsOAuthOrFleetWithoutLegacyAppSettings()
    {
        foreach (var fleet in new[] { true, false })
        {
            var values = new Dictionary<string, string?>
            {
                ["SOLAR_SITE_ID"] = "42",
                ["SOLAR_API_AUTH_TOKEN"] = fleet ? "fleet-key" : null,
                ["SOLAR_OAUTH_CLIENT_ID"] = fleet ? null : "client-id",
                ["SOLAR_OAUTH_CLIENT_SECRET"] = fleet ? null : "secret"
            };
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddIntegrations(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
            using var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetRequiredService<ISolarService>());
            Assert.Equal("https://monitoringapi.solaredge.com/v2/", provider.GetRequiredService<IOptions<SolarOptions>>().Value.BaseAddress);
        }
    }

    [Fact]
    public async Task TokenStore_PersistsRotationAcrossInstancesOutsideRepository()
    {
        var settings = Options.Create(new SolarOptions { ClientId = Guid.NewGuid().ToString("N"), SiteId = "test-site" });
        var store = new SolarEdgeTokenStore(settings);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), store.TokenPath);
        try
        {
            Assert.Null(await store.LoadAsync(CancellationToken.None));
            var first = new SolarEdgeTokens("access-1", "refresh-1", DateTimeOffset.UtcNow.AddHours(1));
            var rotated = new SolarEdgeTokens("access-2", "refresh-2", DateTimeOffset.UtcNow.AddHours(2));
            await store.SaveAsync(first, CancellationToken.None);
            await store.SaveAsync(rotated, CancellationToken.None);

            Assert.Equal(rotated, await new SolarEdgeTokenStore(settings).LoadAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(store.TokenPath);
        }
    }

    [Theory]
    [InlineData("x-ratelimit-remaining-minute", "5")]
    [InlineData("x-ratelimit-remaining-month", "0")]
    public async Task ExhaustedMonthlyCredits_AreNotRetried(string header, string remaining)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrations(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SOLAR_SITE_ID"] = "42",
            ["SOLAR_API_AUTH_TOKEN"] = "fleet-key"
        }).Build());
        var requests = 0;
        services.AddHttpClient<ISolarService, SolarService>().ConfigurePrimaryHttpMessageHandler(() => new Handler(_ =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = JsonContent.Create(new { detail = "Monthly credit limit reached." })
            };
            response.Headers.Add(header, remaining);
            return response;
        }));
        using var provider = services.BuildServiceProvider();

        var error = await Assert.ThrowsAsync<IntegrationResponseException>(
            () => provider.GetRequiredService<ISolarService>().GetOverviewAsync());

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData("https://monitoringapi.solaredge.com/")]
    [InlineData("http://monitoringapi.solaredge.com/v2/")]
    public void Options_RejectLegacyOrInsecureBaseAddresses(string baseAddress)
    {
        var services = new ServiceCollection();
        services.AddIntegrations(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SOLAR_SITE_ID"] = "42",
            ["SOLAR_API_AUTH_TOKEN"] = "fleet-key",
            ["SOLAR_API_BASE_ADDRESS"] = baseAddress
        }).Build());
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SolarOptions>>().Value);
    }

    private sealed class AuthFixture : IDisposable, IHttpClientFactory
    {
        public IOptions<SolarOptions> Settings { get; }
        public MemoryTokenStore Store { get; } = new();
        public Clock Clock { get; } = new();
        public SolarEdgeTokenProvider Provider { get; }
        public int ExchangeCount { get; private set; }
        public JsonElement LastBody { get; private set; }
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;

        public AuthFixture(IOptions<SolarOptions>? settings = null)
        {
            Settings = settings ?? Options.Create(new SolarOptions
            {
                SiteId = "42",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            });
            Provider = new SolarEdgeTokenProvider(this, Settings, Store, Clock);
        }

        public HttpClient CreateClient(string name)
        {
            Assert.Equal(SolarEdgeTokenProvider.OAuthClientName, name);
            return new HttpClient(new AsyncHandler(async (request, ct) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/v2/oauth2/token", request.RequestUri!.AbsolutePath);
                Assert.Empty(request.RequestUri.Query);
                ExchangeCount++;
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                LastBody = json.RootElement.Clone();
                return new HttpResponseMessage(ResponseStatus)
                {
                    Content = JsonContent.Create(new { access_token = "new-access", refresh_token = "new-refresh", expires_in = 7200 })
                };
            }))
            { BaseAddress = new Uri("https://solar.example/v2/") };
        }

        public void Dispose() => Provider.Dispose();
    }

    private sealed class MemoryTokenStore : ISolarEdgeTokenStore
    {
        public SolarEdgeTokens? Tokens { get; set; }
        public Task<SolarEdgeTokens?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Tokens);
        public Task SaveAsync(SolarEdgeTokens tokens, CancellationToken cancellationToken)
        {
            Tokens = tokens;
            return Task.CompletedTask;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
