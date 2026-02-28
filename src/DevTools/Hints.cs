namespace DevTools;

static class Hints
{
    public const string Exit = "[dim]Press Q to exit[/]";
    public const string Back = "[dim]Press ESC to go back[/]";
    public const string Paths = "[dim]F2 configure paths[/]";
    public static string ConfigPath(string path) => $"[dim]Config path ({path})[/]";
}