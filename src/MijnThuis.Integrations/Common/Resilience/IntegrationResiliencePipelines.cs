using System.Net.Sockets;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace MijnThuis.Integrations.Common.Resilience;

/// <summary>
/// Resilience pipelines for integrations that do not use HTTP (UDP and WebSocket based devices).
/// HTTP integrations get their resilience from the standard HTTP resilience handler instead.
/// </summary>
public static class IntegrationResiliencePipelines
{
    /// <summary>
    /// Retries transient network failures (timeouts, socket and WebSocket errors) against local devices.
    /// </summary>
    public const string LocalDevice = "MijnThuis.Integrations.LocalDevice";

    internal static IServiceCollection AddIntegrationResiliencePipelines(this IServiceCollection services)
    {
        services.AddResiliencePipeline(LocalDevice, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<SocketException>()
                    .Handle<WebSocketException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(30)));

        return services;
    }
}
