using DevTools.Components.MenuPrompt;
using DevTools.Components.Screen;
using DevTools.Services;
using Spectre.Console;

namespace DevTools.Screens;

class RepoPathsScreen(IAnsiConsole console, AppContext appContext, ConfigurationManager configManager) : Screen(console)
{
    private const string Sentinel = "+ Add directory...";

    private MenuPrompt<string>? _menu;
    private bool shouldStay = false;

    public async Task ShowSettingsAsync(CancellationToken ct)
    {
        do
        {
            Console.Clear();
            await ShowAsync(ct).ConfigureAwait(false);
        } while (shouldStay && !ct.IsCancellationRequested);
    }

    protected override Task OnInit(CancellationToken cancellationToken)
    {
        var hints = new Markup(string.Join("[dim] | [/]", "[dim]A to add[/]", "[dim]D to remove[/]", Hints.Back!));

        AddElement(hints);
        AddElement(Text.Empty);

        _menu = new MenuPrompt<string>()
            .Title("[green]Repository directories[/]:")
            .UseChoiceProvider(() => [.. appContext.Config.RepoPaths, Sentinel])
            .UseConverter(p => p == Sentinel ? $"[dim]{p}[/]" : $"{p.EscapeMarkup()}")
            .HighlightStyle(Styles.Hightlight)
            .EnableWrapArount()
            .BindKey(ConsoleKey.A, _ =>
            {
                return ScreenInputResult.Exit;
            })
            .BindKey(ConsoleKey.D, ctx =>
            {
                if (ctx.CurrentItem != Sentinel)
                {
                    appContext.Config.RepoPaths.Remove(ctx.CurrentItem);
                    configManager.Save();
                    ctx.Reset();
                }
                return ScreenInputResult.Refresh;
            })
            .BindKey(ConsoleKey.Escape, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.Q, _ => ScreenInputResult.Exit);

        AddElement(_menu);

        return Task.CompletedTask;
    }

    protected override Task OnExit(CancellationToken cancellationToken)
    {
        shouldStay = false;

        var ctx = _menu!.SubmitContext;

        if (ctx is null)
        {
            return Task.CompletedTask;
        }

        if (ctx.KeyInfo.Key == ConsoleKey.Q)
        {
            Console.ClearAndExit();
            return Task.CompletedTask;
        }

        if ((ctx.KeyInfo.Key == ConsoleKey.Enter && ctx.CurrentItem == Sentinel)
            || ctx.KeyInfo.Key == ConsoleKey.A)
        {
            var path = Console.Ask<string>("[dim]Enter directory path:[/]").Trim();

            if (!string.IsNullOrWhiteSpace(path)
                && Directory.Exists(path)
                && !appContext.Config.RepoPaths.Contains(path))
            {
                appContext.Config.RepoPaths.Add(path);
                configManager.Save();
            }

            shouldStay = true;
        }

        return Task.CompletedTask;
    }
}
