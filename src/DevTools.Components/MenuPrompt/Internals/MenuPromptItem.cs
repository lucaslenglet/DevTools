namespace DevTools.Components.MenuPrompt.Internals;

internal sealed class MenuPromptItem<T> : IMenuItem<T>
    where T : notnull
{
    public T Data { get; }
    public bool IsGroup { get; private set; }
    public int Depth { get; internal set; }
    public List<MenuPromptItem<T>> Children { get; } = [];

    public MenuPromptItem(T data)
    {
        Data = data;
    }

    public IMenuItem<T> AddChild(T item)
    {
        IsGroup = true;
        var child = new MenuPromptItem<T>(item) { Depth = Depth + 1 };
        Children.Add(child);
        return child;
    }
}
