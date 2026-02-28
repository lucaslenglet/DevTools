using DevTools.Components.Screen;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.TextInput;

public class TextInputComponent(string prompt, string defaultValue = "") : IScreenComponent
{
    private readonly string _prompt = prompt;
    private string _value = defaultValue;

    public string Value => _value;
    public bool Cancelled { get; private set; }

    public IRenderable BuildRenderable(IAnsiConsole console)
    {
        var safeValue = _value.EscapeMarkup();
        return new Markup($"{_prompt} {safeValue}[rapidblink]█[/]");
    }

    public ScreenInputResult HandleInput(IAnsiConsole console, ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Backspace:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    _value = string.Empty;
                }
                else if (_value.Length > 0)
                {
                    _value = _value[..^1];
                }
                return ScreenInputResult.Refresh;

            case ConsoleKey.Enter:
                return ScreenInputResult.Exit;

            case ConsoleKey.Escape:
                Cancelled = true;
                return ScreenInputResult.Exit;

            default:
                if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                {
                    _value += key.KeyChar;
                    return ScreenInputResult.Refresh;
                }
                return ScreenInputResult.None;
        }
    }
}
