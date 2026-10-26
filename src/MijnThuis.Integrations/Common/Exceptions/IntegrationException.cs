using System.Net;

namespace MijnThuis.Integrations.Common.Exceptions;

/// <summary>
/// Base exception for every failure that originates from an external integration.
/// Consumers can catch this single type, or one of the more specific subtypes below.
/// </summary>
public class IntegrationException : Exception
{
    public IntegrationException(string integration, string operation, string message, Exception? innerException = null)
        : base($"[{integration}] {operation} failed: {message}", innerException)
    {
        Integration = integration;
        Operation = operation;
    }

    public string Integration { get; }

    public string Operation { get; }
}

/// <summary>
/// The integration is missing configuration or is configured incorrectly.
/// </summary>
public sealed class IntegrationConfigurationException(string integration, string operation, string message, Exception? innerException = null)
    : IntegrationException(integration, operation, message, innerException);

/// <summary>
/// The external system could not be reached (network failure, timeout, open circuit breaker, ...).
/// These failures are usually transient.
/// </summary>
public sealed class IntegrationUnavailableException(string integration, string operation, string message, Exception? innerException = null)
    : IntegrationException(integration, operation, message, innerException);

/// <summary>
/// The external system rejected the credentials, or authentication could not be completed.
/// </summary>
public sealed class IntegrationAuthenticationException(string integration, string operation, string message, Exception? innerException = null)
    : IntegrationException(integration, operation, message, innerException);

/// <summary>
/// The external system responded, but with an error status or an unexpected/invalid payload.
/// </summary>
public sealed class IntegrationResponseException(string integration, string operation, string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : IntegrationException(integration, operation, message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
