using DevTools.Components.MenuPrompt;
using DevTools.Components.Screen;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Screens;

class RepositoryCommandsScreen(IAnsiConsole console, AppContext appContext) : Screen(console)
{
    private record RepositoryAction(
        string Text,
        ConfigCommand? Command = null,
        string? Color = null,
        string? Decoration = null)
    {
        public string ToMarkup() => $"{Decoration} {Color ?? Spectre.Console.Color.White.ToMarkup()}".Trim();
    }

    private readonly AppContext _appContext = appContext;

    private GitRepoInfo? repo;
    private MenuPrompt<RepositoryAction>? menu;
    private ConfigCommand? command;

    public async Task<ConfigCommand?> PromptAsync(GitRepoInfo repo, CancellationToken cancellationToken)
    {
        this.repo = repo;
        await ShowAsync(cancellationToken).ConfigureAwait(false);
        return command;
    }

    protected override Task OnInit(CancellationToken cancellationToken)
    {
        var hints = new Markup(Hints.Join(Hints.Exit!, Hints.Back!));

        AddElement(hints);
        AddElement(Text.Empty);

        menu = new MenuPrompt<RepositoryAction>()
            .Title($"Select a [green]command[/] [dim]({repo!.Directory.FullName})[/] :")
            .UseConverter(o => $"[{o.ToMarkup()}]{o.Text,-20}[/]")
            .AddChoices(_appContext.Config.CustomCommands.Select(c => new RepositoryAction(c.Name, c, c.Color)))
            .HighlightStyle(Styles.Hightlight)
            .SearchHighlightStyle(Styles.SearchHightlight)
            .EnableWrapArount()
            .BindKey(ConsoleKey.Q, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.Escape, _ => ScreenInputResult.Exit);

        AddElement(menu);

        return Task.CompletedTask;
    }

    protected override Task OnExit(CancellationToken cancellationToken)
    {
        var ctx = menu!.SubmitContext;

        if (ctx is null || ctx.KeyInfo.Key == ConsoleKey.Escape)
        {
            return Task.CompletedTask;
        }

        if (ctx.KeyInfo.Key == ConsoleKey.Q)
        {
            Console.ClearAndExit();
            return Task.CompletedTask;
        }

        command = ctx.CurrentItem.Command;
        return Task.CompletedTask;
    }
}
