namespace MijnThuis.Integrations.Solar;

/// <summary>One-time homeowner consent for V2 site access. Subsequent refreshes are automatic.</summary>
public interface ISolarAuthorizationService
{
    Uri GetAuthorizationUri();

    /// <summary>
    /// Exchanges the complete redirect URL returned after consent and persists the rotating tokens
    /// under the current user's local application-data directory, outside the repository.
    /// </summary>
    Task AuthorizeAsync(string callbackUrl, CancellationToken cancellationToken = default);
}
