using System.Diagnostics;
using DevTools.Components.MenuPrompt;
using DevTools.Components.Screen;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Menus;

class RepositoryActionsMenu(IAnsiConsole _console, AppContext _context) : Screen
{
    private GitRepoInfo? _repo;
    private MenuPrompt<RepositoryAction> _menu = null!;

    public Task ShowAsync(GitRepoInfo repo)
    {
        _repo = repo;
        return base.ShowAsync(_console, false, CancellationToken.None);
    }

    protected override Task OnInit()
    {
        var hints = new Markup(string.Join("[dim] | [/]", Hints.Exit!, Hints.Back!));

        AddElement(hints);
        AddElement(Text.Empty);

        _menu = new MenuPrompt<RepositoryAction>()
            .Title($"Select a [green]command[/] [dim]({_repo!.Directory.FullName})[/] :")
            .UseConverter(o => $"[{o.ToMarkup()}]{o.Text,-20}[/]")
            .AddChoices([
                .. _context.Config.CustomCommands.Select(CommandToAction),
                new RepositoryAction("← Back", Decoration: Decoration.Dim.ToString()),
            ])
            .HighlightStyle(Styles.Hightlight)
            .SearchHighlightStyle(Styles.SearchHightlight)
            .EnableWrapArount()
            .BindKey(ConsoleKey.Q, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.Escape, _ => ScreenInputResult.Exit);

        AddElement(_menu);

        return Task.CompletedTask;
    }

    protected override Task OnExit()
    {
        var ctx = _menu.SubmitContext;
        if (ctx is null)
        {
            return Task.CompletedTask;
        }

        if (ctx.KeyInfo.Key == ConsoleKey.Q)
        {
            _console.ClearAndExit();
            return Task.CompletedTask;
        }

        // ESC or "← Back" (Action is null) → go back without doing anything
        if (ctx.KeyInfo.Key == ConsoleKey.Escape || ctx.CurrentItem.Action is null)
        {
            return Task.CompletedTask;
        }

        ctx.CurrentItem.Action.Invoke(_repo!);
        return Task.CompletedTask;
    }

    internal static RepositoryAction CommandToAction(ConfigCommand cmd) => new(
        cmd.Name,
        r => ExecuteCommand(
            cmd.ProcessName,
            FormatIfNotNull(cmd.WorkingDirectory, r.Directory.FullName),
            FormatIfNotNull(cmd.Arguments, r.Directory.FullName)),
        cmd.Color);

    private static void ExecuteCommand(string fileName, string? workingDirectory, string? arguments)
    {
        Console.Clear();
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            UseShellExecute = false,
        })?.WaitForExit();
        Console.Clear();
    }

    private static string? FormatIfNotNull(string? pattern, string value)
        => pattern is not null ? string.Format(pattern, value) : null;
}
