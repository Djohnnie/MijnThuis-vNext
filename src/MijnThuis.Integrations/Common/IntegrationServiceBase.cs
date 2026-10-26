using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common.Exceptions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MijnThuis.Integrations.Common;

/// <summary>
/// Base class for all integration services. Provides a single place where low-level exceptions
/// (HTTP, sockets, serialization, resilience, configuration, ...) are logged and translated into
/// the <see cref="IntegrationException"/> hierarchy.
/// </summary>
public abstract class IntegrationServiceBase(ILogger logger)
{
    protected abstract string IntegrationName { get; }

    protected ILogger Logger { get; } = logger;

    protected async Task<T> ExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IntegrationException ex)
        {
            Logger.LogWarning(ex, "{Integration} operation {Operation} failed", ex.Integration, ex.Operation);
            throw;
        }
        catch (Exception ex)
        {
            var mapped = MapException(operation, ex);
            Logger.LogError(ex, "{Integration} operation {Operation} failed: {Message}", IntegrationName, operation, mapped.Message);
            throw mapped;
        }
    }

    protected Task ExecuteAsync(string operation, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            await action(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    protected IntegrationResponseException ResponseError(string operation, string message, HttpStatusCode? statusCode = null)
        => new(IntegrationName, operation, message, statusCode);

    protected IntegrationUnavailableException Unavailable(string operation, string message, Exception? innerException = null)
        => new(IntegrationName, operation, message, innerException);

    private IntegrationException MapException(string operation, Exception exception) => exception switch
    {
        OptionsValidationException ex => new IntegrationConfigurationException(IntegrationName, operation, ex.Message, ex),
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } ex
            => new IntegrationAuthenticationException(IntegrationName, operation, $"Access denied ({(int)ex.StatusCode!}).", ex),
        HttpRequestException { StatusCode: not null } ex
            => new IntegrationResponseException(IntegrationName, operation, $"Unexpected status code {(int)ex.StatusCode!}.", ex.StatusCode, ex),
        HttpRequestException ex => new IntegrationUnavailableException(IntegrationName, operation, ex.Message, ex),
        BrokenCircuitException ex => new IntegrationUnavailableException(IntegrationName, operation, "The circuit breaker is open because of repeated failures. Try again later.", ex),
        TimeoutRejectedException or TimeoutException or OperationCanceledException
            => new IntegrationUnavailableException(IntegrationName, operation, "The operation timed out.", exception),
        SocketException or WebSocketException
            => new IntegrationUnavailableException(IntegrationName, operation, exception.Message, exception),
        JsonException or FormatException or InvalidCastException or InvalidDataException or CryptographicException
            => new IntegrationResponseException(IntegrationName, operation, $"Invalid response: {exception.Message}", innerException: exception),
        _ => new IntegrationException(IntegrationName, operation, exception.Message, exception)
    };
}
