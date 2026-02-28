namespace DevTools.Components.MenuPrompt.Internals;

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
