namespace DevTools;

static class Hints
{
    public const string Exit = "[dim]Press Q to exit[/]";
    public const string Back = "[dim]Press ESC to go back[/]";
    public const string Paths = "[dim]F2 configure paths[/]";
    public const string Rename = "[dim]R to rename[/]";
    public static string ConfigPath(string path) => $"[dim]Config path ({path})[/]";
    public static string Join(params string[] hints) => string.Join("[dim] | [/]", hints);
}