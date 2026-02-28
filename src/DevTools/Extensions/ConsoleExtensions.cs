using System.Diagnostics;
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

        public void Execute(string fileName, string? workingDirectory, string? arguments)
        {
            console.Clear();
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                Arguments = arguments,
                UseShellExecute = false,
            })?.WaitForExit();
            console.Clear();
        }
    }
}