using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MijnThuis.Integrations.Common.Exceptions;

namespace MijnThuis.Integrations.Common.Http;

internal static class HttpResponseExtensions
{
    private const int MaxErrorBodyLength = 500;

    /// <summary>
    /// Throws a descriptive <see cref="IntegrationException"/> (including a snippet of the response body)
    /// when the response does not have a success status code.
    /// </summary>
    public static async Task EnsureSuccessAsync(this HttpResponseMessage response, string integration, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > MaxErrorBodyLength)
        {
            body = body[..MaxErrorBodyLength] + "...";
        }

        var message = $"{(int)response.StatusCode} {response.ReasonPhrase}" + (string.IsNullOrWhiteSpace(body) ? string.Empty : $" - {body}");

        throw response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? new IntegrationAuthenticationException(integration, operation, message)
            : new IntegrationResponseException(integration, operation, message, response.StatusCode);
    }

    public static async Task<T> ReadRequiredJsonAsync<T>(this HttpResponseMessage response, string integration, string operation, JsonSerializerOptions? options, CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(options, cancellationToken).ConfigureAwait(false);

        return result ?? throw new IntegrationResponseException(integration, operation, "The response body was empty.", response.StatusCode);
    }
}
