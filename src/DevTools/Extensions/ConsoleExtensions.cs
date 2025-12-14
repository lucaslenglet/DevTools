using Spectre.Console;

namespace DevTools.Extensions;

public static class ConsoleExtensions
{
    extension(IAnsiConsole console)
    {
        public void ClearAndExit()
        {
            console.Clear();
            Environment.Exit(0);
        }

        public void ClearAndDisplayHint(params string[] hints)
        {
            console.Clear();
            console.MarkupLine(string.Join("[dim] | [/]", hints));
            console.WriteLine();
        }
    }
}