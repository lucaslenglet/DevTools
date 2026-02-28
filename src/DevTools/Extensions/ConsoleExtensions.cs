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
    }
}