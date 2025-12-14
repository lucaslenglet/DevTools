using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.MenuPrompt.Internals;

internal sealed class MenuPromptInternal<T>
    where T : notnull
{
    private readonly IAnsiConsole _console;
    private readonly IMenuPromptStrategy<T> _strategy;

    public MenuPromptInternal(IAnsiConsole console, IMenuPromptStrategy<T> strategy)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

    public async Task<MenuPromptSubmitResult<T>> Show(
        ListPromptTree<T> tree,
        Func<T, string> converter,
        SelectionMode selectionMode,
        bool skipUnselectableItems,
        bool searchEnabled,
        int requestedPageSize,
        bool wrapAround,
        int? defaultIndex,
        CancellationToken cancellationToken = default)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (!_console.Profile.Capabilities.Interactive)
        {
            throw new NotSupportedException(
                "Cannot show selection prompt since the current " +
                "terminal isn't interactive.");
        }

        if (!_console.Profile.Capabilities.Ansi)
        {
            throw new NotSupportedException(
                "Cannot show selection prompt since the current " +
                "terminal does not support ANSI escape sequences.");
        }

        var nodes = tree.Traverse().ToList();
        if (nodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot show an empty selection prompt. Please call the AddChoice() method to configure the prompt.");
        }

        var state = new MenuPromptState<T>(nodes, converter, _strategy.CalculatePageSize(_console, nodes.Count, requestedPageSize), wrapAround, selectionMode, skipUnselectableItems, searchEnabled, defaultIndex);
        var hook = new ListPromptRenderHook<T>(_console, () => BuildRenderable(state));

        ConsoleKeyInfo submitKey = default;

        using (new RenderHookScope(_console, hook))
        {
            _console.Cursor.Hide();
            hook.Refresh();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rawKey = await _console.Input.ReadKeyAsync(true, cancellationToken).ConfigureAwait(false);
                if (rawKey == null)
                {
                    continue;
                }

                var key = rawKey.Value;
                var result = _strategy.HandleInput(key, state);
                if (result == MenuPromptInputResult.Submit)
                {
                    submitKey = key;
                    break;
                }

                if (state.Update(key) || result == MenuPromptInputResult.Refresh)
                {
                    hook.Refresh();
                }
            }
        }

        hook.Clear();
        _console.Cursor.Show();

        return new(state.Items[state.Index].Data, submitKey, state.Index);
    }

    private IRenderable BuildRenderable(MenuPromptState<T> state)
    {
        var pageSize = state.PageSize;
        var middleOfList = pageSize / 2;

        var skip = 0;
        var take = state.ItemCount;
        var cursorIndex = state.Index;

        var scrollable = state.ItemCount > pageSize;
        if (scrollable)
        {
            skip = Math.Max(0, state.Index - middleOfList);
            take = Math.Min(pageSize, state.ItemCount - skip);

            if (take < pageSize)
            {
                // Pointer should be below the middle of the (visual) list
                var diff = pageSize - take;
                skip -= diff;
                take += diff;
                cursorIndex = middleOfList + diff;
            }
            else
            {
                // Take skip into account
                cursorIndex -= skip;
            }
        }

        // Build the renderable
        return _strategy.Render(
            _console,
            scrollable, cursorIndex,
            state.Items.Skip(skip).Take(take)
                .Select((node, index) => (index, node)),
            state.SkipUnselectableItems,
            state.Searching,
            state.SearchText);
    }
}