using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.Screen;

public interface IScreenComponent
{
    public IRenderable BuildRenderable(IAnsiConsole console);
    public ScreenInputResult HandleInput(IAnsiConsole console, ConsoleKeyInfo key);
}