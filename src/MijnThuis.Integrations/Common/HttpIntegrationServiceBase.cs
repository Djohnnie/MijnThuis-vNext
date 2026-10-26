using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MijnThuis.Integrations.Common.Http;

namespace MijnThuis.Integrations.Common;

/// <summary>
/// Base class for integrations that talk to an HTTP API through a typed <see cref="System.Net.Http.HttpClient"/>.
/// The client is created by <see cref="IHttpClientFactory"/>, so it already carries the resilience pipeline.
/// Integrations that need more than one client can pass a specific client to the helper overloads.
/// </summary>
public abstract class HttpIntegrationServiceBase(HttpClient httpClient, ILogger logger) : IntegrationServiceBase(logger)
{
    protected HttpClient HttpClient { get; } = httpClient;

    protected virtual JsonSerializerOptions? SerializerOptions => null;

    protected Task<T> GetJsonAsync<T>(string operation, string requestUri, CancellationToken cancellationToken)
        => GetJsonAsync<T>(HttpClient, operation, requestUri, cancellationToken);

    protected Task<T> GetJsonAsync<T>(HttpClient client, string operation, string requestUri, CancellationToken cancellationToken)
        => SendJsonAsync<T>(client, operation, HttpMethod.Get, requestUri, body: null, cancellationToken);

    /// <summary>
    /// Like <see cref="GetJsonAsync{T}(string, string, CancellationToken)"/>, but returns <c>default</c> when the resource does not exist (404/204).
    /// </summary>
    protected Task<T?> GetJsonOrDefaultAsync<T>(string operation, string requestUri, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            using var response = await HttpClient.GetAsync(requestUri, ct).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return default;
            }

            await response.EnsureSuccessAsync(IntegrationName, operation, ct).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    protected Task<T> SendJsonAsync<T>(string operation, HttpMethod method, string requestUri, object? body, CancellationToken cancellationToken)
        => SendJsonAsync<T>(HttpClient, operation, method, requestUri, body, cancellationToken);

    protected Task<T> SendJsonAsync<T>(HttpClient client, string operation, HttpMethod method, string requestUri, object? body, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            using var response = await SendCoreAsync(client, operation, method, requestUri, body, ct).ConfigureAwait(false);
            return await response.ReadRequiredJsonAsync<T>(IntegrationName, operation, SerializerOptions, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    protected Task SendAsync(string operation, HttpMethod method, string requestUri, object? body, CancellationToken cancellationToken)
        => SendAsync(HttpClient, operation, method, requestUri, body, cancellationToken);

    protected Task SendAsync(HttpClient client, string operation, HttpMethod method, string requestUri, object? body, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            using var _ = await SendCoreAsync(client, operation, method, requestUri, body, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpClient client, string operation, HttpMethod method, string requestUri, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, requestUri);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: SerializerOptions);
        }

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        try
        {
            await response.EnsureSuccessAsync(IntegrationName, operation, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
