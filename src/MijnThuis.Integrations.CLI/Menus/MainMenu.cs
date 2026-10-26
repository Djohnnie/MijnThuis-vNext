using MijnThuis.Integrations.CLI.Rendering;
using Spectre.Console;

namespace MijnThuis.Integrations.CLI.Menus;

internal sealed class MainMenu(IEnumerable<IIntegrationMenu> menus, ConfigurationOverview configurationOverview, ActionRunner actionRunner)
{
    private const string Back = "← Back";

    public async Task RunAsync()
    {
        AnsiConsole.Write(new FigletText("MijnThuis").Color(Color.SteelBlue1));
        AnsiConsole.MarkupLine("[grey]Integration test console — pick an integration and a feature to try it out. Ctrl+C cancels a running call.[/]");
        AnsiConsole.WriteLine();

        var configurationChoice = new MainMenuChoice("⚙  Configuration overview", null);
        var exitChoice = new MainMenuChoice("Exit", null);
        var choices = menus.OrderBy(m => m.Title).Select(m => new MainMenuChoice(m.Title, m)).Append(configurationChoice).Append(exitChoice).ToList();

        while (true)
        {
            var choice = AnsiConsole.Prompt(new SelectionPrompt<MainMenuChoice>()
                .Title("[bold]Which integration do you want to test?[/]")
                .PageSize(20)
                .EnableSearch()
                .AddChoices(choices)
                .UseConverter(c => Markup.Escape(c.Title)));

            if (choice == exitChoice)
            {
                return;
            }

            if (choice == configurationChoice)
            {
                configurationOverview.Render();
                continue;
            }

            await RunIntegrationMenuAsync(choice.Menu!);
        }
    }

    private async Task RunIntegrationMenuAsync(IIntegrationMenu menu)
    {
        var actions = menu.GetActions().ToList();
        var back = new MenuAction(Back, () => throw new InvalidOperationException());

        while (true)
        {
            var action = AnsiConsole.Prompt(new SelectionPrompt<MenuAction>()
                .Title($"[bold]{Markup.Escape(menu.Title)}[/] — what do you want to do?")
                .PageSize(20)
                .EnableSearch()
                .AddChoices(actions.Append(back))
                .UseConverter(a => a.RequiresConfirmation ? $"{Markup.Escape(a.Title)} [yellow]![/]" : Markup.Escape(a.Title)));

            if (action == back)
            {
                return;
            }

            await actionRunner.RunAsync(menu.Title, action);
            AnsiConsole.WriteLine();
        }
    }

    private sealed record MainMenuChoice(string Title, IIntegrationMenu? Menu);
}
