using System.Diagnostics.CodeAnalysis;
using DevTools.Components.MenuPrompt.Internals;
using DevTools.Components.Screen;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.MenuPrompt;

public class MenuPrompt<T> : IScreenComponent
    where T : notnull
{
    private const string ArrowMarker = ">";
    private const string MoreChoicesMarkup = "[grey](Move up and down to reveal more choices)[/]";

    private readonly MenuTree<T> _tree;
    private readonly Dictionary<ConsoleKey, Func<MenuKeyContext<T>, ScreenInputResult>> _keyBindings = new();
    private MenuPromptState<T>? _state;

    public MenuPrompt()
    {
        _tree = new MenuTree<T>();
        RegisterDefaultBindings();
    }

    public string? Title { get; set; }
    public int PageSize { get; set; } = 30;
    public bool WrapAround { get; set; } = false;
    public Style? HighlightStyle { get; set; }
    public Style? DisabledStyle { get; set; }
    public Style? SearchHighlightStyle { get; set; }
    public string? SearchPlaceholderText { get; set; }
    public Func<T, string>? Converter { get; set; }
    public string? MoreChoicesText { get; set; }
    public MenuSelectionMode Mode { get; set; } = MenuSelectionMode.Leaf;
    public bool SearchEnabled { get; set; }
    public int? DefaultIndex { get; set; }
    public Func<IEnumerable<T>>? ChoiceProvider { get; set; }

    /// <summary>Set after the menu exits. Provides the selected item and the key that triggered the exit.</summary>
    public MenuKeyContext<T>? SubmitContext { get; private set; }

    public int? CurrentIndex => _state?.Index;

    public IMenuItem<T> AddChoice(T item)
    {
        return _tree.Add(item);
    }

    /// <summary>
    /// Registers a key binding. The handler receives a rich context with the current item,
    /// index, key info, and navigation/reset capabilities.
    /// </summary>
    public MenuPrompt<T> BindKey(ConsoleKey key, Func<MenuKeyContext<T>, ScreenInputResult> handler)
    {
        _keyBindings[key] = handler;
        return this;
    }

    public IRenderable BuildRenderable(IAnsiConsole console)
    {
        EnsureStateIsInitialized(console);

        var indexState = BuildIndexState();

        var disabledStyle = DisabledStyle ?? Color.Grey;
        var highlightStyle = HighlightStyle ?? Color.Blue;
        var searchHighlightStyle = SearchHighlightStyle ?? new Style(foreground: Color.Default, background: Color.Yellow, Decoration.Bold);

        var list = new List<IRenderable>();
        if (Title != null)
        {
            list.Add(new Markup(Title));
        }

        var grid = new Grid();
        grid.AddColumn(new GridColumn().Padding(0, 0, 1, 0).NoWrap());

        if (Title != null)
        {
            grid.AddEmptyRow();
        }

        var items = _state.Items
            .Skip(indexState.Skip)
            .Take(indexState.Take)
            .Select((node, index) => (Index: index, Node: node));

        foreach (var item in items)
        {
            var current = item.Index == indexState.CursorIndex;
            var prompt = current ? ArrowMarker : new string(' ', ArrowMarker.Length);
            var style = item.Node.IsGroup && Mode == MenuSelectionMode.Leaf
                ? disabledStyle
                : current ? highlightStyle : Style.Plain;

            var indent = new string(' ', item.Node.Depth * 2);

            var text = (Converter ?? DefaultConverter).Invoke(item.Node.Data);
            if (current)
            {
                text = text.RemoveMarkup().EscapeMarkup();
            }

            if (_state.SearchText.Length > 0 && !(item.Node.IsGroup && Mode == MenuSelectionMode.Leaf))
            {
                text = text.Highlight(_state.SearchText, searchHighlightStyle);
            }

            grid.AddRow(new Markup(indent + prompt + " " + text, style));
        }

        list.Add(grid);

        if (SearchEnabled || indexState.Scrollable)
        {
            list.Add(Text.Empty);
        }

        if (SearchEnabled)
        {
            if (_state.Searching)
            {
                list.Add(new Markup($"[{searchHighlightStyle.Background.ToMarkup()}]Searching [dim](Press ESC to cancel)[/] : {_state.SearchText.EscapeMarkup()}[/]"));
            }
            else
            {
                list.Add(new Markup(SearchPlaceholderText ?? "[grey](Press ? to search)[/]"));
            }
        }

        if (indexState.Scrollable)
        {
            list.Add(new Markup(MoreChoicesText ?? MoreChoicesMarkup));
        }

        return new Rows(list);
    }

    public ScreenInputResult HandleInput(IAnsiConsole console, ConsoleKeyInfo key)
    {
        EnsureStateIsInitialized(console);

        if (_state.Searching)
        {
            // Give registered bindings priority so consumers can override search-mode keys.
            if (char.IsControl(key.KeyChar) && _keyBindings.TryGetValue(key.Key, out var searchHandler))
            {
                var ctx = CreateContext(key);
                var result = searchHandler(ctx);
                if (result == ScreenInputResult.Exit)
                {
                    SubmitContext = ctx;
                }
                return result;
            }

            // Forward everything else to search input handling.
            return _state.HandleSearchInput(key)
                ? ScreenInputResult.Refresh
                : ScreenInputResult.None;
        }

        if (_keyBindings.TryGetValue(key.Key, out var handler))
        {
            var ctx = CreateContext(key);
            var result = handler(ctx);
            if (result == ScreenInputResult.Exit)
            {
                SubmitContext = ctx;
            }
            return result;
        }

        return ScreenInputResult.None;
    }

    private MenuKeyContext<T> CreateContext(ConsoleKeyInfo key)
    {
        return new MenuKeyContext<T>(_state!, key, ResetState);
    }

    private void ResetState()
    {
        _state = null;
    }

    private void RegisterDefaultBindings()
    {
        // Navigation — use leaf-aware state methods so group items are skipped in Leaf mode.
        _keyBindings[ConsoleKey.UpArrow] = _ => NavRelative(-1);
        _keyBindings[ConsoleKey.K] = _ => NavRelative(-1);
        _keyBindings[ConsoleKey.DownArrow] = _ => NavRelative(+1);
        _keyBindings[ConsoleKey.J] = _ => NavRelative(+1);
        _keyBindings[ConsoleKey.Home] = _ => { _state!.MoveFirst(); return ScreenInputResult.Refresh; };
        _keyBindings[ConsoleKey.End] = _ => { _state!.MoveLast(); return ScreenInputResult.Refresh; };
        _keyBindings[ConsoleKey.PageUp] = _ => NavPage(-1);
        _keyBindings[ConsoleKey.PageDown] = _ => NavPage(+1);

        // Submit
        _keyBindings[ConsoleKey.Enter] = TrySubmit;
        _keyBindings[ConsoleKey.Packet] = TrySubmit;
        _keyBindings[ConsoleKey.Spacebar] = ctx =>
        {
            // Spacebar submits only when search is not active (search-mode check is upstream).
            if (SearchEnabled)
            {
                return ScreenInputResult.None;
            }
            return TrySubmit(ctx);
        };

        // Search toggle — handler checks Shift modifier to match '?' character.
        _keyBindings[ConsoleKey.OemComma] = ctx =>
        {
            if (SearchEnabled && ctx.KeyInfo.Modifiers == ConsoleModifiers.Shift)
            {
                _state!.Searching = true;
                return ScreenInputResult.Refresh;
            }
            return ScreenInputResult.None;
        };

        // Escape and Backspace cancel/modify search when searching (registered so they participate
        // in the search-mode binding lookup and consumers can override if needed).
        _keyBindings[ConsoleKey.Escape] = ctx =>
        {
            if (_state!.Searching)
            {
                _state.HandleSearchInput(ctx.KeyInfo);
                return ScreenInputResult.Refresh;
            }
            return ScreenInputResult.None;
        };

        _keyBindings[ConsoleKey.Backspace] = ctx =>
        {
            if (_state!.Searching)
            {
                _state.HandleSearchInput(ctx.KeyInfo);
                return ScreenInputResult.Refresh;
            }
            return ScreenInputResult.None;
        };
    }

    private ScreenInputResult NavRelative(int delta)
    {
        _state!.MoveRelative(delta);
        return ScreenInputResult.Refresh;
    }

    private ScreenInputResult NavPage(int direction)
    {
        _state!.MovePage(direction * _state.PageSize);
        return ScreenInputResult.Refresh;
    }

    private ScreenInputResult TrySubmit(MenuKeyContext<T> ctx)
    {
        if (_state!.Current.IsGroup && Mode == MenuSelectionMode.Leaf)
        {
            return ScreenInputResult.None;
        }
        return ScreenInputResult.Exit;
    }

    private IndexState BuildIndexState()
    {
        var middleOfList = _state!.PageSize / 2;
        bool scrollable = _state.ItemCount > _state.PageSize;

        var skip = 0;
        var take = _state.ItemCount;
        var cursorIndex = _state.Index;

        if (scrollable)
        {
            skip = Math.Max(0, _state.Index - middleOfList);
            take = Math.Min(_state.PageSize, _state.ItemCount - skip);

            if (take < _state.PageSize)
            {
                var diff = _state.PageSize - take;
                skip -= diff;
                take += diff;
                cursorIndex = middleOfList + diff;
            }
            else
            {
                cursorIndex -= skip;
            }
        }

        return new(
            Scrollable: scrollable,
            Skip: skip,
            Take: take,
            CursorIndex: cursorIndex
        );
    }

    private int CalculatePageSize(IAnsiConsole console, int totalItemCount, int requestedPageSize)
    {
        var extra = 0;

        if (Title != null)
        {
            extra += 2;
        }

        var scrollable = totalItemCount > requestedPageSize;
        if (SearchEnabled || scrollable)
        {
            extra += 1;
        }

        if (SearchEnabled)
        {
            extra += 1;
        }

        if (scrollable)
        {
            extra += 1;
        }

        if (requestedPageSize > console.Profile.Height - extra)
        {
            return console.Profile.Height - extra;
        }

        return requestedPageSize;
    }

    [MemberNotNull(nameof(_state))]
    private void EnsureStateIsInitialized(IAnsiConsole console)
    {
        if (_state is not null)
        {
            return;
        }

        var nodes = _tree.Traverse().ToList();
        if (nodes.Count == 0 && ChoiceProvider is not null)
        {
            var tree = new MenuTree<T>();
            foreach (var item in ChoiceProvider())
            {
                tree.Add(item);
            }
            nodes = tree.Traverse().ToList();
        }

        if (nodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot show an empty selection prompt. Please call the AddChoice() method to configure the prompt.");
        }

        var converter = Converter ?? DefaultConverter;

        _state = new MenuPromptState<T>(nodes, converter, CalculatePageSize(console, nodes.Count, PageSize), WrapAround, Mode, true, SearchEnabled, DefaultIndex);
    }

    private static string DefaultConverter(T item) => item.ToString() ?? "?";

    private record IndexState(bool Scrollable, int Skip, int Take, int CursorIndex);
}
