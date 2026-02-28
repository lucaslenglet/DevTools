using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.Screen;

public sealed class ScreenRenderHook : IRenderHook
{
    private readonly IAnsiConsole _console;

    private readonly Func<IAnsiConsole, IRenderable> _builder;

    private readonly LiveRenderable _live;

    private readonly object _lock;

    private bool _dirty;

    public ScreenRenderHook(IAnsiConsole console, Func<IAnsiConsole, IRenderable> builder)
    {
        _console = console ?? throw new ArgumentNullException("console");
        _builder = builder ?? throw new ArgumentNullException("builder");
        _live = new LiveRenderable(console);
        _lock = new object();
        _dirty = true;
    }

    public void Clear()
    {
        _console.Write(_live.RestoreCursor());
    }

    public void Refresh()
    {
        _dirty = true;
        _console.Write(new ControlCode(string.Empty));
    }

    public IEnumerable<IRenderable> Process(RenderOptions options, IEnumerable<IRenderable> renderables)
    {
        lock (_lock)
        {
            if (!_live.HasRenderable || _dirty)
            {
                _live.SetRenderable(_builder(_console));
                _dirty = false;
            }

            yield return _live.PositionCursor(options);
            foreach (IRenderable renderable in renderables)
            {
                yield return renderable;
            }

            yield return _live;
        }
    }
}