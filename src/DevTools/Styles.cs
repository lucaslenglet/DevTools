using Spectre.Console;

namespace DevTools;

public static class Styles
{
    public static Style Hightlight { get; } = new Style(foreground: Color.Black, background: Color.White);
    public static Style SearchHightlight { get; } = new(foreground: Color.Black, background: Color.Cyan1);
}