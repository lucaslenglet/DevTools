using DevTools.Components.MenuPrompt;
using DevTools.Components.Screen;
using DevTools.Helpers;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Screens;

class RepositoryActionsScreen(IAnsiConsole console, AppContext appContext) : Screen(console)
{
    private readonly AppContext _appContext = appContext;

    private record RepositoryAction(
        string Text,
        Action<GitRepoInfo>? Action = null,
        string? Color = null,
        string? Decoration = null)
    {
        public string ToMarkup() => $"{Decoration} {Color ?? Spectre.Console.Color.White.ToMarkup()}".Trim();
    }

    private GitRepoInfo? repo;
    private MenuPrompt<RepositoryAction>? menu;

    public Task ShowAsync(GitRepoInfo repo, CancellationToken cancellationToken)
    {
        this.repo = repo;
        return ShowAsync(cancellationToken);
    }

    protected override Task OnInit(CancellationToken cancellationToken)
    {
        var hints = new Markup(string.Join("[dim] | [/]", Hints.Exit!, Hints.Back!));

        AddElement(hints);
        AddElement(Text.Empty);

        menu = new MenuPrompt<RepositoryAction>()
            .Title($"Select a [green]command[/] [dim]({repo!.Directory.FullName})[/] :")
            .UseConverter(o => $"[{o.ToMarkup()}]{o.Text,-20}[/]")
            .AddChoices([
                .. _appContext.Config.CustomCommands.Select(CommandToAction),
                new RepositoryAction("← Back", Decoration: Decoration.Dim.ToString()),
            ])
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
        var ctx = menu!.SubmitContext!;

        if (ctx.KeyInfo.Key == ConsoleKey.Q)
        {
            Console.ClearAndExit();
            return Task.CompletedTask;
        }

        // ESC or "← Back" (Action is null) → go back without doing anything
        if (ctx.KeyInfo.Key == ConsoleKey.Escape || ctx.CurrentItem.Action is null)
        {
            return Task.CompletedTask;
        }

        ctx.CurrentItem.Action.Invoke(repo!);
        return Task.CompletedTask;
    }

    private RepositoryAction CommandToAction(ConfigCommand cmd) => new(
        cmd.Name,
        r => Console.Execute(
            cmd.ProcessName,
            StringHelper.FormatIfNotNull(cmd.WorkingDirectory, r.Directory.FullName),
            StringHelper.FormatIfNotNull(cmd.Arguments, r.Directory.FullName)),
        cmd.Color);
}
