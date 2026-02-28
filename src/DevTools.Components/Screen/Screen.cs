using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.Screen;

public abstract class Screen
{
    private List<ScreenElement> _elements = [];

    protected Screen(IAnsiConsole console)
    {
        Console = console;
    }

    protected IAnsiConsole Console { get; }

    protected void AddElement(IRenderable renderable)
        => _elements.Add(new RenderableScreenElement(renderable));

    protected void AddElement(IScreenComponent component)
        => _elements.Add(new ComponentScreenElement(component));

    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        _elements.Clear();
        await OnInit(cancellationToken).ConfigureAwait(false);

        var hook = new ScreenRenderHook(Console, BuildRenderable);

        using (new RenderHookScope(Console, hook))
        {
            Console.Cursor.Hide();
            hook.Refresh();

            while (!cancellationToken.IsCancellationRequested)
            {
                var rawKey = await SafeReadKey(Console, cancellationToken).ConfigureAwait(false);
                if (rawKey == null)
                {
                    continue;
                }

                var key = rawKey.Value;

                var result = ScreenInputResult.None;

                foreach (var element in _elements.OfType<ComponentScreenElement>())
                {
                    if (result == ScreenInputResult.None)
                    {
                        result = element.Component.HandleInput(Console, key);
                    }
                }

                if (result == ScreenInputResult.Exit)
                {
                    break;
                }

                if (result == ScreenInputResult.Refresh)
                {
                    hook.Refresh();
                }
            }
        }

        hook.Clear();
        Console.Cursor.Show();

        await OnExit(cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task OnInit(CancellationToken cancellationToken);
    protected abstract Task OnExit(CancellationToken cancellationToken);

    private Rows BuildRenderable(IAnsiConsole console)
    {
        var list = new List<IRenderable>();

        foreach (var element in _elements)
        {
            if (element is RenderableScreenElement renderable)
            {
                list.Add(renderable.Renderable);
            }
            else if (element is ComponentScreenElement component)
            {
                list.Add(component.Component.BuildRenderable(console));
            }
        }

        return new Rows(list);
    }

    private static async Task<ConsoleKeyInfo?> SafeReadKey(IAnsiConsole console, CancellationToken cancellationToken)
    {
        try
        {
            return await console.Input.ReadKeyAsync(true, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private abstract record ScreenElement;
    private record RenderableScreenElement(IRenderable Renderable) : ScreenElement;
    private record ComponentScreenElement(IScreenComponent Component) : ScreenElement;
}