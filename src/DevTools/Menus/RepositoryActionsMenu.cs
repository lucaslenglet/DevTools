using System.Diagnostics;
using DevTools.Components.Extensions;
using DevTools.Components.MenuPrompt;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Menus;

class RepositoryActionsMenu(IAnsiConsole console)
{
    public async Task ShowAsync(GitRepoInfo repo, CancellationToken cancellationToken = default)
    {
        console.ClearAndDisplayHint(Hints.Exit!, Hints.Back!);

        var selection = new MenuPrompt<RepositoryAction>()
            .Title($"Select a [green]program[/] [dim]({repo.Repo.FullName})[/] :")
            .UseConverter(o => $"[{o.Style.ToMarkup()}]{o.Text,-20}[/]")
            .AddChoices([
                new RepositoryAction("Copilot", StartCopilotCli, Color.Silver),
                new RepositoryAction("Codex", StartCodexCli, Color.Grey63),
                new RepositoryAction("Vibe", StartVibeCli, Color.Orange1),
                new RepositoryAction("Powershell", StartPwsh, Color.Blue),
                new RepositoryAction("Lazygit", StartLazygit, Color.HotPink),
                new RepositoryAction("File Explorer", StartExplorer, Color.Green),
                new RepositoryAction("← Back", Decoration: Decoration.Dim)
            ])
            .HighlightStyle(Styles.Hightlight)
            .SearchHighlightStyle(Styles.SearchHightlight)
            .EnableWrapArount()
            .AddSubmitKeys(ConsoleKey.Q!, ConsoleKey.Escape!)
            ;

        var result = await selection.ShowAsync(console, cancellationToken)
            .ConfigureAwait(false);

        if (result.ConsoleKeyInfo.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
        {
            result.Data.Action?.Invoke(repo);
        }
        else if (result.ConsoleKeyInfo.Key is ConsoleKey.Q)
        {
            console.ClearAndExit();
        }
    }

    public static void StartLazygit(GitRepoInfo repo)
        => StartProcess("lazygit", workingDirectory: repo.Repo.FullName);

    public static void StartPwsh(GitRepoInfo repo)
    {
        Console.Clear();
        StartProcess("pwsh", workingDirectory: repo.Repo.FullName);
        Console.Clear();
    }

    public static void StartExplorer(GitRepoInfo repo)
        => StartProcess("explorer.exe", arguments: repo.Repo.FullName);

    public static void StartCopilotCli(GitRepoInfo repo)
    {
        StartProcess("copilot", workingDirectory: repo.Repo.FullName);
        Console.Clear();
    }

    public static void StartCodexCli(GitRepoInfo repo)
    {
        StartProcess("codex", workingDirectory: repo.Repo.FullName);
        Console.Clear();
    }

    public static void StartVibeCli(GitRepoInfo repo)
    {
        StartProcess("vibe", workingDirectory: repo.Repo.FullName);
        Console.Clear();
    }

    private static void StartProcess(
        string fileName, string? workingDirectory = null, string? arguments = null)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            UseShellExecute = false
        };
        Process.Start(processInfo)?.WaitForExit();
    }

    private record RepositoryAction(
        string Text,
        Action<GitRepoInfo>? Action = null,
        Color? Color = null,
        Decoration? Decoration = null)
    {
        public Style Style { get; } = new Style(foreground: Color, decoration: Decoration);
    }
}