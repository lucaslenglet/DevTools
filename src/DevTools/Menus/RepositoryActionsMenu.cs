using System.Diagnostics;
using DevTools.Components.Extensions;
using DevTools.Components.MenuPrompt;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Menus;

class RepositoryActionsMenu(IAnsiConsole console, AppContext context)
{
    // public async Task ShowAsync(GitRepoInfo repo, CancellationToken cancellationToken = default)
    // {
    //     console.ClearAndDisplayHint(Hints.Exit!, Hints.Back!);

    //     var selection = new MenuPrompt<RepositoryAction>()
    //         .Title($"Select a [green]command[/] [dim]({repo.Directory.FullName})[/] :")
    //         .UseConverter(o => $"[{o.ToMarkup()}]{o.Text,-20}[/]")
    //         .AddChoices([
    //             .. context.Config.CustomCommands.Select(CommandToAction),
    //             new RepositoryAction("← Back", Decoration: Decoration.Dim.ToString())
    //         ])
    //         .HighlightStyle(Styles.Hightlight)
    //         .SearchHighlightStyle(Styles.SearchHightlight)
    //         .EnableWrapArount()
    //         .AddSubmitKeys(ConsoleKey.Q!, ConsoleKey.Escape!)
    //         ;

    //     var result = await selection.ShowAsync(console, cancellationToken)
    //         .ConfigureAwait(false);

    //     if (result.ConsoleKeyInfo.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
    //     {
    //         result.Data.Action?.Invoke(repo);
    //     }
    //     else if (result.ConsoleKeyInfo.Key is ConsoleKey.Q)
    //     {
    //         console.ClearAndExit();
    //     }
    // }

    // public static RepositoryAction CommandToAction(ConfigCommand command)
    //     => new(
    //         command.Name,
    //         (r) => ExecuteCommand(
    //             command.ProcessName,
    //             FormatIfNotNull(command.WorkingDirectory, r.Directory.FullName),
    //             FormatIfNotNull(command.Arguments, r.Directory.FullName)),
    //             command.Color,
    //             Decoration: null
    //         );

    // private static void ExecuteCommand(
    //     string fileName, string? workingDirectory = null, string? arguments = null)
    // {
    //     Console.Clear();
    //     var processInfo = new ProcessStartInfo
    //     {
    //         FileName = fileName,
    //         WorkingDirectory = workingDirectory,
    //         Arguments = arguments,
    //         UseShellExecute = false
    //     };
    //     Process.Start(processInfo)?.WaitForExit();
    //     Console.Clear();
    // }

    // private static string? FormatIfNotNull(string? pattern, string value)
    //     => pattern is not null ? string.Format(pattern, value) : null;
}