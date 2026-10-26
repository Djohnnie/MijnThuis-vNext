namespace MijnThuis.Integrations.CLI.Menus;

/// <summary>
/// Executes an integration feature. Runs inside a DI scope and must honour the cancellation token (Ctrl+C).
/// </summary>
internal delegate Task<object?> MenuOperation(IServiceProvider services, CancellationToken cancellationToken);

/// <param name="Title">Text shown in the menu.</param>
/// <param name="Prepare">Asks the user for input (if any) and returns the operation to execute.</param>
/// <param name="RequiresConfirmation">Asks for confirmation before executing, for actions with real-world side effects.</param>
internal sealed record MenuAction(string Title, Func<MenuOperation> Prepare, bool RequiresConfirmation = false);

/// <summary>
/// Marker result for commands that don't return anything.
/// </summary>
internal sealed class CommandCompleted
{
    public static readonly CommandCompleted Instance = new();

    private CommandCompleted()
    {
    }
}
