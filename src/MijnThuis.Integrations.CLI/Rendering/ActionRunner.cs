using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.CLI.Menus;
using MijnThuis.Integrations.Common.Exceptions;
using Spectre.Console;

namespace MijnThuis.Integrations.CLI.Rendering;

/// <summary>
/// Runs a menu action: asks for input and confirmation, shows a spinner while the integration is called
/// (Ctrl+C cancels the call) and renders the result or the error.
/// </summary>
internal sealed class ActionRunner(IServiceProvider serviceProvider)
{
    private CancellationTokenSource? _current;

    /// <summary>
    /// Cancels the running action. Returns <c>false</c> when no action is running.
    /// </summary>
    public bool CancelCurrent()
    {
        var current = _current;
        if (current is null)
        {
            return false;
        }

        current.Cancel();
        return true;
    }

    public async Task RunAsync(string menuTitle, MenuAction action)
    {
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(menuTitle)}[/] [grey]›[/] {Markup.Escape(action.Title)}").LeftJustified());

        MenuOperation operation;
        try
        {
            operation = action.Prepare();
        }
        catch (Exception ex)
        {
            RenderError(ex, cancelled: false);
            return;
        }

        if (action.RequiresConfirmation && !AnsiConsole.Confirm($"[yellow]{Markup.Escape(action.Title)}[/]: are you sure?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[grey]Skipped.[/]");
            return;
        }

        using var cts = new CancellationTokenSource();
        _current = cts;

        object? result = null;
        Exception? error = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"{Markup.Escape(action.Title)}... [grey](Ctrl+C to cancel)[/]", async _ =>
                {
                    try
                    {
                        await using var scope = serviceProvider.CreateAsyncScope();
                        result = await operation(scope.ServiceProvider, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                });
        }
        finally
        {
            _current = null;
        }

        stopwatch.Stop();

        if (error is null)
        {
            ResultRenderer.Render(result);
        }
        else
        {
            RenderError(error, cts.IsCancellationRequested);
        }

        AnsiConsole.MarkupLine($"[grey]Completed in {stopwatch.Elapsed.TotalMilliseconds:N0} ms[/]");
    }

    private static void RenderError(Exception exception, bool cancelled)
    {
        if (cancelled && exception is OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return;
        }

        var optionsError = Find<OptionsValidationException>(exception);
        if (optionsError is not null)
        {
            var failures = string.Join(Environment.NewLine, optionsError.Failures.Select(f => $"• {Markup.Escape(f)}"));
            AnsiConsole.Write(new Panel(new Markup($"{failures}{Environment.NewLine}{Environment.NewLine}[grey]Set the missing values in Properties/launchSettings.json (or user secrets) and restart.[/]"))
                .Header("[yellow] Configuration error [/]")
                .BorderColor(Color.Yellow));
            return;
        }

        switch (exception)
        {
            case IntegrationException integrationError:
                var (label, color) = integrationError switch
                {
                    IntegrationConfigurationException => ("Configuration error", Color.Yellow),
                    IntegrationUnavailableException => ("Unavailable", Color.Red),
                    IntegrationAuthenticationException => ("Authentication failed", Color.Red),
                    IntegrationResponseException => ("Invalid response", Color.Red),
                    _ => ("Integration error", Color.Red)
                };

                var grid = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn();
                grid.AddRow("[bold]Integration[/]", Markup.Escape(integrationError.Integration));
                grid.AddRow("[bold]Operation[/]", Markup.Escape(integrationError.Operation));
                grid.AddRow("[bold]Message[/]", Markup.Escape(integrationError.Message));

                if (integrationError is IntegrationResponseException { StatusCode: { } statusCode })
                {
                    grid.AddRow("[bold]Status code[/]", $"{(int)statusCode} {statusCode}");
                }

                for (var inner = integrationError.InnerException; inner is not null; inner = inner.InnerException)
                {
                    grid.AddRow("[grey]Caused by[/]", $"[grey]{Markup.Escape(inner.GetType().Name)}: {Markup.Escape(inner.Message)}[/]");
                }

                AnsiConsole.Write(new Panel(grid).Header($"[{color.ToMarkup()}] {label} [/]").BorderColor(color));
                break;

            default:
                AnsiConsole.Write(new Panel(new Markup($"[red]{Markup.Escape(exception.GetType().Name)}[/]: {Markup.Escape(exception.Message)}"))
                    .Header("[red] Error [/]")
                    .BorderColor(Color.Red));
                break;
        }
    }

    private static T? Find<T>(Exception exception) where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
