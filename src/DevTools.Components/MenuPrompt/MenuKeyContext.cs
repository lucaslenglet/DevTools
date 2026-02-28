using DevTools.Components.MenuPrompt.Internals;

namespace DevTools.Components.MenuPrompt;

public sealed class MenuKeyContext<T>
    where T : notnull
{
    private readonly MenuPromptState<T> _state;
    private readonly Action _reset;

    internal MenuKeyContext(MenuPromptState<T> state, ConsoleKeyInfo keyInfo, Action reset)
    {
        _state = state;
        _reset = reset;
        KeyInfo = keyInfo;
    }

    /// <summary>The currently highlighted item.</summary>
    public T CurrentItem => _state.Current.Data;

    /// <summary>The flat index of the currently highlighted item.</summary>
    public int CurrentIndex => _state.Index;

    /// <summary>The total number of items in the list.</summary>
    public int TotalItems => _state.ItemCount;

    /// <summary>The current page size.</summary>
    public int PageSize => _state.PageSize;

    /// <summary>Full key info including modifiers.</summary>
    public ConsoleKeyInfo KeyInfo { get; }

    /// <summary>Moves the cursor to the given index. Clamping and wrap-around are handled by the state.</summary>
    public void MoveTo(int index) => _state.MoveTo(index);

    /// <summary>Resets the menu state so that choices are re-fetched from the provider on the next render.</summary>
    public void Reset() => _reset();
}
