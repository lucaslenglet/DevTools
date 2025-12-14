using System.Diagnostics;
using Spectre.Console;

namespace DevTools.Components.MenuPrompt.Internals;

internal class MenuPromptState<T>
    where T : notnull
{
    private readonly Func<T, string> _converter;

    public int Index { get; private set; }
    public int ItemCount => Items.Count;
    public int PageSize { get; }
    public bool WrapAround { get; }
    public SelectionMode Mode { get; }
    public bool SkipUnselectableItems { get; private set; }
    public bool SearchEnabled { get; }
    public IReadOnlyList<ListPromptItem<T>> Items { get; }
    private readonly IReadOnlyList<int>? _leafIndexes;

    public ListPromptItem<T> Current => Items[Index];
    public string SearchText { get => field.TrimStart('?'); private set; }
    public bool Searching { get; set; }

    public MenuPromptState(
        IReadOnlyList<ListPromptItem<T>> items,
        Func<T, string> converter,
        int pageSize, bool wrapAround,
        SelectionMode mode,
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

        if (SkipUnselectableItems && mode == SelectionMode.Leaf)
        {
            _leafIndexes =
                Items
                    .Select((item, index) => new { item, index })
                    .Where(x => !x.item.IsGroup)
                    .Select(x => x.index)
                    .ToList()
                    .AsReadOnly();

            Index = _leafIndexes.FirstOrDefault();
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

    public bool Update(ConsoleKeyInfo keyInfo)
    {
        var index = Index;
        if (SkipUnselectableItems && Mode == SelectionMode.Leaf)
        {
            Debug.Assert(_leafIndexes != null, nameof(_leafIndexes) + " == null");
            var currentLeafIndex = _leafIndexes.IndexOf(index);
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.K:
                    if (currentLeafIndex > 0)
                    {
                        index = _leafIndexes[currentLeafIndex - 1];
                    }
                    else if (WrapAround)
                    {
                        index = _leafIndexes.LastOrDefault();
                    }

                    break;

                case ConsoleKey.DownArrow:
                case ConsoleKey.J:
                    if (currentLeafIndex < _leafIndexes.Count - 1)
                    {
                        index = _leafIndexes[currentLeafIndex + 1];
                    }
                    else if (WrapAround)
                    {
                        index = _leafIndexes.FirstOrDefault();
                    }

                    break;

                case ConsoleKey.Home:
                    index = _leafIndexes.FirstOrDefault();
                    break;

                case ConsoleKey.End:
                    index = _leafIndexes.LastOrDefault();
                    break;

                case ConsoleKey.PageUp:
                    index = Math.Max(currentLeafIndex - PageSize, 0);
                    if (index < _leafIndexes.Count)
                    {
                        index = _leafIndexes[index];
                    }

                    break;

                case ConsoleKey.PageDown:
                    index = Math.Min(currentLeafIndex + PageSize, _leafIndexes.Count - 1);
                    if (index < _leafIndexes.Count)
                    {
                        index = _leafIndexes[index];
                    }

                    break;
            }
        }
        else
        {
            index = keyInfo.Key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.K => Index - 1,
                ConsoleKey.DownArrow or ConsoleKey.J => Index + 1,
                ConsoleKey.Home => 0,
                ConsoleKey.End => ItemCount - 1,
                ConsoleKey.PageUp => Index - PageSize,
                ConsoleKey.PageDown => Index + PageSize,
                _ => Index,
            };
        }

        var search = SearchText;
        var searching = Searching;

        if (SearchEnabled && Searching)
        {
            // If is text input, append to search filter
            if (!char.IsControl(keyInfo.KeyChar))
            {
                search = SearchText + keyInfo.KeyChar;

                var item = Items.FirstOrDefault(x =>
                    _converter.Invoke(x.Data).Contains(search, StringComparison.OrdinalIgnoreCase)
                    && (!x.IsGroup || Mode != SelectionMode.Leaf));

                if (item != null)
                {
                    index = Items.IndexOf(item);
                }
            }

            if (keyInfo.Key == ConsoleKey.Escape)
            {
                searching = false;
                search = string.Empty;
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (search.Length > 0)
                {
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                    {
                        search = string.Empty;
                    }
                    else
                    {
                        search = search.Substring(0, search.Length - 1);
                    }
                }

                var item = Items.FirstOrDefault(x =>
                    _converter.Invoke(x.Data).Contains(search, StringComparison.OrdinalIgnoreCase) &&
                    (!x.IsGroup || Mode != SelectionMode.Leaf));

                if (item != null)
                {
                    index = Items.IndexOf(item);
                }
            }
        }

        index = WrapAround
            ? (ItemCount + (index % ItemCount)) % ItemCount
            : index.Clamp(0, ItemCount - 1);

        if (index != Index || SearchText != search || searching != Searching)
        {
            Index = index;
            SearchText = search;
            Searching = searching;
            return true;
        }

        return false;
    }
}