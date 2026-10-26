using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace MijnThuis.Integrations.Common.Http;

internal static class HttpClientRegistrationExtensions
{
    /// <summary>
    /// Registers a typed HTTP client for an integration with the standard resilience pipeline
    /// (rate limiter, total timeout, retry with exponential backoff, circuit breaker, attempt timeout).
    /// Retries are disabled for unsafe HTTP methods (POST, PUT, PATCH, DELETE, CONNECT) so commands are never sent twice.
    /// </summary>
    public static IHttpClientBuilder AddIntegrationHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        Action<IServiceProvider, HttpClient> configureClient,
        Action<HttpStandardResilienceOptions>? configureResilience = null,
        bool pipelinePerAuthority = false)
        where TClient : class
        where TImplementation : class, TClient
    {
        var builder = services.AddHttpClient<TClient, TImplementation>(configureClient);
        builder.AddIntegrationResilience(configureResilience, pipelinePerAuthority);

        return builder;
    }

    /// <inheritdoc cref="AddIntegrationHttpClient{TClient, TImplementation}"/>
    public static IHttpClientBuilder AddIntegrationHttpClient(
        this IServiceCollection services,
        string name,
        Action<IServiceProvider, HttpClient> configureClient,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
    {
        var builder = services.AddHttpClient(name, configureClient);
        builder.AddIntegrationResilience(configureResilience, pipelinePerAuthority: false);

        return builder;
    }

    /// <param name="pipelinePerAuthority">
    /// Use a separate pipeline (and thus circuit breaker) per host, for clients that talk to multiple devices.
    /// </param>
    private static void AddIntegrationResilience(this IHttpClientBuilder builder, Action<HttpStandardResilienceOptions>? configureResilience, bool pipelinePerAuthority)
    {
        var resilienceBuilder = builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.DisableForUnsafeHttpMethods();
            configureResilience?.Invoke(options);
        });

        if (pipelinePerAuthority)
        {
            resilienceBuilder.SelectPipelineByAuthority();
        }
    }

    /// <summary>
    /// Makes sure a base address ends with a slash, so relative request URIs are appended instead of replacing the last segment.
    /// </summary>
    public static Uri ToBaseAddress(this string baseAddress)
        => new(baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/", UriKind.Absolute);
}
