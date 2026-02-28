using DevTools.Components.Screen;
using DevTools.Components.TextInput;
using Spectre.Console;

namespace DevTools.Screens;

class TextInputScreen : Screen
{
    private readonly string _prompt;
    private readonly string _defaultValue;
    private TextInputComponent? _component;

    public TextInputScreen(IAnsiConsole console, string prompt, string defaultValue = "")
        : base(console)
    {
        _prompt = prompt;
        _defaultValue = defaultValue;
    }

    public string Value { get; private set; } = string.Empty;
    public bool Cancelled { get; private set; }

    protected override Task OnInit(CancellationToken cancellationToken)
    {
        var hints = new Markup(Hints.Join("[dim]Empty to delete[/]", Hints.Back!));

        AddElement(hints);
        AddElement(Text.Empty);

        _component = new TextInputComponent(_prompt, _defaultValue);
        AddElement(_component);
        return Task.CompletedTask;
    }

    protected override Task OnExit(CancellationToken cancellationToken)
    {
        Value = _component!.Value;
        Cancelled = _component.Cancelled;
        return Task.CompletedTask;
    }
}
