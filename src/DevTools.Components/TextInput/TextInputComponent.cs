using DevTools.Components.Screen;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.TextInput;

public class TextInputComponent(string prompt, string defaultValue = "") : IScreenComponent
{
    private readonly string _prompt = prompt;
    private string _value = defaultValue;
    private int _cursorPosition = defaultValue.Length;

    public string Value => _value;
    public bool Cancelled { get; private set; }

    public IRenderable BuildRenderable(IAnsiConsole console)
    {
        var beforeCursor = _value[.._cursorPosition].EscapeMarkup();
        var afterCursor = _value[_cursorPosition..].EscapeMarkup();
        return new Markup($"{_prompt} {beforeCursor}[rapidblink]█[/]{afterCursor}");
    }

    public ScreenInputResult HandleInput(IAnsiConsole console, ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                _cursorPosition = Math.Max(0, _cursorPosition - 1);
                return ScreenInputResult.Refresh;

            case ConsoleKey.RightArrow:
                _cursorPosition = Math.Min(_value.Length, _cursorPosition + 1);
                return ScreenInputResult.Refresh;

            case ConsoleKey.Delete:
                if (_cursorPosition < _value.Length)
                {
                    _value = _value.Remove(_cursorPosition, 1);
                }
                return ScreenInputResult.Refresh;

            case ConsoleKey.Backspace:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    _value = string.Empty;
                    _cursorPosition = 0;
                }
                else if (_cursorPosition > 0)
                {
                    _value = _value.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
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
                    _value = _value.Insert(_cursorPosition, key.KeyChar.ToString());
                    _cursorPosition++;
                    return ScreenInputResult.Refresh;
                }
                return ScreenInputResult.None;
        }
    }
}
