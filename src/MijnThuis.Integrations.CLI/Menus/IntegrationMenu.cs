using Microsoft.Extensions.DependencyInjection;

namespace MijnThuis.Integrations.CLI.Menus;

internal interface IIntegrationMenu
{
    string Title { get; }

    IEnumerable<MenuAction> GetActions();
}

/// <summary>
/// Base class for a menu that tests the features of a single integration service.
/// The service is resolved when an action is executed, so configuration errors are reported per action
/// instead of preventing the application from starting.
/// </summary>
internal abstract class IntegrationMenu<TService> : IIntegrationMenu
    where TService : notnull
{
    public abstract string Title { get; }

    public abstract IEnumerable<MenuAction> GetActions();

    protected static MenuAction Query<T>(string title, Func<TService, CancellationToken, Task<T>> query)
        => new(title, () => async (services, ct) => await query(services.GetRequiredService<TService>(), ct));

    protected static MenuAction Query<TInput, T>(string title, Func<TInput> prompt, Func<TService, TInput, CancellationToken, Task<T>> query)
        => new(title, () =>
        {
            var input = prompt();
            return async (services, ct) => await query(services.GetRequiredService<TService>(), input, ct);
        });

    protected static MenuAction Command(string title, Func<TService, CancellationToken, Task> command, bool confirm = true)
        => new(title, () => async (services, ct) =>
        {
            await command(services.GetRequiredService<TService>(), ct);
            return CommandCompleted.Instance;
        }, confirm);

    protected static MenuAction Command<TInput>(string title, Func<TInput> prompt, Func<TService, TInput, CancellationToken, Task> command, bool confirm = true)
        => new(title, () =>
        {
            var input = prompt();
            return async (services, ct) =>
            {
                await command(services.GetRequiredService<TService>(), input, ct);
                return CommandCompleted.Instance;
            };
        }, confirm);
}
