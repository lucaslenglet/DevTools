using DevTools.Components.MenuPrompt;

namespace DevTools.Components.MenuPrompt.Internals;

internal class MenuPromptState<T>
    where T : notnull
{
    private readonly Func<T, string> _converter;

    public int Index { get; private set; }
    public int ItemCount => Items.Count;
    public int PageSize { get; }
    public bool WrapAround { get; }
    public MenuSelectionMode Mode { get; }
    public bool SkipUnselectableItems { get; private set; }
    public bool SearchEnabled { get; }
    public IReadOnlyList<MenuPromptItem<T>> Items { get; }
    // Non-null when Mode==Leaf and SkipUnselectableItems; contains the flat indexes of selectable items.
    private readonly List<int>? _leafIndexes;

    public MenuPromptItem<T> Current => Items[Index];
    public string SearchText { get => field.TrimStart('?'); private set; }
    public bool Searching { get; set; }

    public MenuPromptState(
        IReadOnlyList<MenuPromptItem<T>> items,
        Func<T, string> converter,
        int pageSize, bool wrapAround,
        MenuSelectionMode mode,
        bool skipUnselectableItems,
        bool searchEnabled,
        int? defaultIndex)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        Items = items;
        PageSize = pageSize;
        WrapAround = wrapAround;
        Mode = mode;
        SkipUnselectableItems = skipUnselectableItems;
        SearchEnabled = searchEnabled;
        SearchText = string.Empty;

        if (SkipUnselectableItems && mode == MenuSelectionMode.Leaf)
        {
            _leafIndexes = Items
                .Select((item, index) => (item, index))
                .Where(x => !x.item.IsGroup)
                .Select(x => x.index)
                .ToList();

            Index = _leafIndexes.Count > 0 ? _leafIndexes[0] : 0;
        }
        else
        {
            Index = 0;
        }

        if (defaultIndex.HasValue)
        {
            Index = defaultIndex.Value;
        }
    }

    /// <summary>
    /// Moves cursor to an absolute index (clamped/wrapped). Intended for direct jumps by consumers.
    /// </summary>
    public bool MoveTo(int index)
    {
        var previous = Index;
        Index = Clamp(index);
        return Index != previous;
    }

    /// <summary>
    /// Moves cursor by a relative delta, respecting Leaf mode (skips group items).
    /// </summary>
    public bool MoveRelative(int delta)
    {
        var previous = Index;

        if (_leafIndexes != null)
        {
            var currentLeafIndex = _leafIndexes.IndexOf(Index);
            var nextLeafIndex = currentLeafIndex + delta;

            if (WrapAround)
            {
                nextLeafIndex = ((_leafIndexes.Count + nextLeafIndex) % _leafIndexes.Count + _leafIndexes.Count) % _leafIndexes.Count;
            }
            else
            {
                nextLeafIndex = Math.Clamp(nextLeafIndex, 0, _leafIndexes.Count - 1);
            }

            if (nextLeafIndex >= 0 && nextLeafIndex < _leafIndexes.Count)
            {
                Index = _leafIndexes[nextLeafIndex];
            }
        }
        else
        {
            Index = Clamp(Index + delta);
        }

        return Index != previous;
    }

    /// <summary>
    /// Moves cursor by a page delta (PageSize steps), respecting Leaf mode.
    /// </summary>
    public bool MovePage(int pageDelta)
    {
        var previous = Index;

        if (_leafIndexes != null)
        {
            var currentLeafIndex = _leafIndexes.IndexOf(Index);
            var nextLeafIndex = Math.Clamp(currentLeafIndex + pageDelta, 0, _leafIndexes.Count - 1);
            if (nextLeafIndex >= 0 && nextLeafIndex < _leafIndexes.Count)
            {
                Index = _leafIndexes[nextLeafIndex];
            }
        }
        else
        {
            Index = Clamp(Index + pageDelta);
        }

        return Index != previous;
    }

    /// <summary>Moves cursor to the first selectable item.</summary>
    public bool MoveFirst()
    {
        var previous = Index;
        Index = _leafIndexes != null && _leafIndexes.Count > 0 ? _leafIndexes[0] : 0;
        return Index != previous;
    }

    /// <summary>Moves cursor to the last selectable item.</summary>
    public bool MoveLast()
    {
        var previous = Index;
        Index = _leafIndexes != null && _leafIndexes.Count > 0
            ? _leafIndexes[_leafIndexes.Count - 1]
            : ItemCount - 1;
        return Index != previous;
    }

    /// <summary>
    /// Handles search text input when in searching mode.
    /// Appends printable characters, handles Backspace/Ctrl+Backspace, and cancels on Escape.
    /// Returns true if state changed.
    /// </summary>
    public bool HandleSearchInput(ConsoleKeyInfo keyInfo)
    {
        var search = SearchText;
        var searching = Searching;
        var index = Index;

        if (!char.IsControl(keyInfo.KeyChar))
        {
            search = SearchText + keyInfo.KeyChar;
            index = FindSearchIndex(search, index);
        }
        else if (keyInfo.Key == ConsoleKey.Escape)
        {
            searching = false;
            search = string.Empty;
        }
        else if (keyInfo.Key == ConsoleKey.Backspace)
        {
            if (search.Length > 0)
            {
                search = (keyInfo.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control
                    ? string.Empty
                    : search.Substring(0, search.Length - 1);
            }
            index = FindSearchIndex(search, index);
        }
        else
        {
            return false;
        }

        index = Clamp(index);

        if (index != Index || SearchText != search || searching != Searching)
        {
            Index = index;
            SearchText = search;
            Searching = searching;
            return true;
        }

        return false;
    }

    private int FindSearchIndex(string search, int fallback)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item.IsGroup && Mode == MenuSelectionMode.Leaf)
            {
                continue;
            }
            if (_converter.Invoke(item.Data).Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return fallback;
    }

    private int Clamp(int index)
    {
        return WrapAround
            ? ItemCount == 0 ? 0 : ((index % ItemCount) + ItemCount) % ItemCount
            : Math.Clamp(index, 0, Math.Max(0, ItemCount - 1));
    }
}
