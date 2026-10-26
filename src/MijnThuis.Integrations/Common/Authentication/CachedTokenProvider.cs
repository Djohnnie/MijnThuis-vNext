namespace MijnThuis.Integrations.Common.Authentication;

/// <summary>
/// Thread-safe cache for an authentication token that is expensive to acquire (e.g. an interactive
/// login flow). Concurrent callers share a single acquisition, and the token is refreshed shortly
/// before it expires or when it is explicitly invalidated (e.g. after a 401 response).
/// Implementations should be registered as singletons.
/// </summary>
public abstract class CachedTokenProvider<TToken>(TimeProvider timeProvider) : IDisposable
    where TToken : class
{
    private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private CachedToken? _current;

    public async Task<TToken> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetValidToken(out var token))
        {
            return token;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetValidToken(out token))
            {
                return token;
            }

            var (newToken, expiresAt) = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
            _current = new CachedToken(newToken, expiresAt);

            return newToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _current = null;

    protected abstract Task<(TToken Token, DateTimeOffset ExpiresAt)> AcquireTokenAsync(CancellationToken cancellationToken);

    private bool TryGetValidToken(out TToken token)
    {
        var current = _current;
        if (current is not null && current.ExpiresAt - ExpirationMargin > timeProvider.GetUtcNow())
        {
            token = current.Token;
            return true;
        }

        token = null!;
        return false;
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record CachedToken(TToken Token, DateTimeOffset ExpiresAt);
}
