using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.Screen;

public abstract class Screen
{
    private List<ScreenElement> _elements = [];

    protected void AddElement(IRenderable renderable)
        => _elements.Add(new RenderableScreenElement(renderable));

    protected void AddElement(IScreenComponent component)
        => _elements.Add(new ComponentScreenElement(component));

    public async Task ShowAsync(IAnsiConsole console, bool isMain, CancellationToken cancellationToken)
    {
        do
        {
            _elements.Clear();
            await OnInit().ConfigureAwait(false);

            var hook = new ScreenRenderHook(console, BuildRenderable);

            using (new RenderHookScope(console, hook))
            {
                console.Cursor.Hide();
                hook.Refresh();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var rawKey = await SafeReadKey(console, cancellationToken).ConfigureAwait(false);
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
                            result = element.Component.HandleInput(console, key);
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
            console.Cursor.Show();

            await OnExit().ConfigureAwait(false);
        } while (isMain && !cancellationToken.IsCancellationRequested);
    }

    protected abstract Task OnInit();
    protected abstract Task OnExit();

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