using System.Diagnostics.CodeAnalysis;
using DevTools.Components.MenuPrompt.Internals;
using DevTools.Components.Screen;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DevTools.Components.MenuPrompt;

public class MenuPrompt<T> : IScreenComponent
    where T : notnull
{
    private readonly ListPromptTree<T> _tree;
    private MenuPromptState<T>? state;

    public MenuPrompt()
    {
        _tree = new ListPromptTree<T>(EqualityComparer<T>.Default);
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
    public SelectionMode Mode { get; set; } = SelectionMode.Leaf;
    public bool SearchEnabled { get; set; }
    public int? DefaultIndex { get; set; }
    public ConsoleKey[] ExitKeys { get; set; } = [];
    public ConsoleKey[] ActionKeys { get; set; } = [];
    public Func<IEnumerable<T>>? ChoiceProvider { get; set; }
    public Action<T, ConsoleKeyInfo>? OnActionKeyPressed { get; set; }

    public ConsoleKeyInfo? LastKey { get; private set; }
    public int? CurrentIndex => state?.Index;

    public ISelectionItem<T> AddChoice(T item)
    {
        var node = new ListPromptItem<T>(item);
        _tree.Add(node);
        return node;
    }

    public IRenderable BuildRenderable(IAnsiConsole console)
    {
        EnsureStateIsInitialized(console);

        var indexState = BuildIndexState(console);

        // Build the renderable
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

        var items = state.Items
            .Skip(indexState.Skip)
            .Take(indexState.Take)
            .Select((node, index) => (Index: index, Node: node));

        foreach (var item in items)
        {
            var current = item.Index == indexState.CursorIndex;
            var prompt = item.Index == indexState.CursorIndex ? ListPromptConstants.Arrow : new string(' ', ListPromptConstants.Arrow.Length);
            var style = item.Node.IsGroup && Mode == SelectionMode.Leaf
                ? disabledStyle
                : current ? highlightStyle : Style.Plain;

            var indent = new string(' ', item.Node.Depth * 2);

            var text = (Converter ?? TypeConverterHelper.ConvertToString)?.Invoke(item.Node.Data) ?? item.Node.Data.ToString() ?? "?";
            if (current)
            {
                text = text.RemoveMarkup().EscapeMarkup();
            }

            if (state.SearchText.Length > 0 && !(item.Node.IsGroup && Mode == SelectionMode.Leaf))
            {
                text = text.Highlight(state.SearchText, searchHighlightStyle);
            }

            grid.AddRow(new Markup(indent + prompt + " " + text, style));
        }

        list.Add(grid);

        if (SearchEnabled || indexState.Scrollable)
        {
            // Add padding
            list.Add(Text.Empty);
        }

        if (SearchEnabled)
        {
            if (state.Searching)
            {
                list.Add(new Markup($"[{searchHighlightStyle.Background.ToMarkup()}]Searching [dim](Press ESC to cancel)[/] : {state.SearchText.EscapeMarkup()}[/]"));
            }
            else
            {
                list.Add(new Markup(SearchPlaceholderText ?? "[grey](Press ? to search)[/]"));
            }
        }

        if (indexState.Scrollable)
        {
            // (Move up and down to reveal more choices)
            list.Add(new Markup(MoreChoicesText ?? ListPromptConstants.MoreChoicesMarkup));
        }

        return new Rows(list);
    }

    private IndexState BuildIndexState(IAnsiConsole console)
    {
        EnsureStateIsInitialized(console);

        var middleOfList = state.PageSize / 2;
        bool scrollable = state.ItemCount > state.PageSize;

        var skip = 0;
        var take = state.ItemCount;
        var cursorIndex = state.Index;
        
        if (scrollable)
        {
            skip = Math.Max(0, state.Index - middleOfList);
            take = Math.Min(state.PageSize, state.ItemCount - skip);

            if (take < state.PageSize)
            {
                // Pointer should be below the middle of the (visual) list
                var diff = state.PageSize - take;
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

        return new(
            Scrollable: scrollable,
            Skip: skip,
            Take: take,
            CursorIndex: cursorIndex
        );
    }

    public ScreenInputResult HandleInput(IAnsiConsole console, ConsoleKeyInfo key)
    {
        EnsureStateIsInitialized(console);

        LastKey = key;

        if (key.Key == ConsoleKey.Enter
         || key.Key == ConsoleKey.Packet
         || (!state.SearchEnabled && key.Key == ConsoleKey.Spacebar))
        {
            // Selecting a non leaf in "leaf mode" is not allowed
            if (state.Current.IsGroup && Mode == SelectionMode.Leaf)
            {
                return ScreenInputResult.None;
            }

            return ScreenInputResult.Exit;
        }

        if (state.Searching)
        {
            return ScreenInputResult.None;
        }
        else if (SearchEnabled && key.Key == ConsoleKey.OemComma && key.Modifiers == ConsoleModifiers.Shift)
        {
            state.Searching = true;
        }

        if (ExitKeys.Contains(key.Key))
        {
            return ScreenInputResult.Exit;
        }

        if (OnActionKeyPressed is not null && ActionKeys.Contains(key.Key))
        {
            OnActionKeyPressed.Invoke(state.Current.Data, key);
            state = null;
            return ScreenInputResult.Refresh;
        }

        if (state.Update(key))
        {
            return ScreenInputResult.Refresh;
        }

        return ScreenInputResult.None;
    }

    private int CalculatePageSize(IAnsiConsole console, int totalItemCount, int requestedPageSize)
    {
        var extra = 0;

        if (Title != null)
        {
            // Title takes up two rows including a blank line
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

    [MemberNotNull(nameof(state))]
    private void EnsureStateIsInitialized(IAnsiConsole console)
    {
        if (state is not null)
        {
            return;
        }

        var nodes = _tree.Traverse().ToList();
        if (nodes.Count == 0 && ChoiceProvider is not null)
        {
            var tree = new ListPromptTree<T>(EqualityComparer<T>.Default);
            foreach(var item in ChoiceProvider().Select(c => new ListPromptItem<T>(c)))
            {
                tree.Add(item);
            }
            nodes = tree.Traverse().ToList();
        }

        if (nodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot show an empty selection prompt. Please call the AddChoice() method to configure the prompt.");
        }

        var converter = Converter ?? TypeConverterHelper.ConvertToString;

        state = new MenuPromptState<T>(nodes, converter, CalculatePageSize(console, nodes.Count, PageSize), WrapAround, Mode, true, SearchEnabled, DefaultIndex);
    }

    private record IndexState(bool Scrollable, int Skip, int Take, int CursorIndex);
}