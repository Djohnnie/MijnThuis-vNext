using System.Net;

namespace MijnThuis.Integrations.Common.Authentication;

/// <summary>
/// Delegating handler that decorates outgoing requests with a token from a <see cref="CachedTokenProvider{TToken}"/>.
/// When the API answers 401 Unauthorized, the cached token is invalidated and the request is retried once with a fresh token.
/// </summary>
internal abstract class TokenAuthenticationHandler<TToken>(CachedTokenProvider<TToken> tokenProvider) : DelegatingHandler
    where TToken : class
{
    protected abstract void ApplyToken(HttpRequestMessage request, TToken token);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the content up-front so the request can be replayed after a token refresh.
        var content = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        var token = await tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        ApplyToken(request, token);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        tokenProvider.Invalidate();

        using var retry = Clone(request, content);
        token = await tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        ApplyToken(retry, token);

        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? content)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (content is not null)
        {
            clone.Content = new ByteArrayContent(content);
            foreach (var header in request.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
