namespace DevTools.Components.MenuPrompt.Internals;

public interface IMenuItem<T>
{
    T Data { get; }
    bool IsGroup { get; }
    IMenuItem<T> AddChild(T item);
}

public sealed class MenuPromptItem<T> : IMenuItem<T>
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

internal sealed class MenuTree<T>
    where T : notnull
{
    private readonly List<MenuPromptItem<T>> _roots = [];

    public MenuPromptItem<T> Add(T item)
    {
        var node = new MenuPromptItem<T>(item) { Depth = 0 };
        _roots.Add(node);
        return node;
    }

    public IReadOnlyList<MenuPromptItem<T>> Traverse()
    {
        var result = new List<MenuPromptItem<T>>();
        foreach (var root in _roots)
        {
            Traverse(root, result);
        }
        return result;
    }

    private static void Traverse(MenuPromptItem<T> node, List<MenuPromptItem<T>> result)
    {
        result.Add(node);
        foreach (var child in node.Children)
        {
            Traverse(child, result);
        }
    }
}
