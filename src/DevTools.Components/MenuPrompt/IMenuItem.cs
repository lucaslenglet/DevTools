namespace DevTools.Components.MenuPrompt;

public interface IMenuItem<T>
{
    T Data { get; }
    bool IsGroup { get; }
    IMenuItem<T> AddChild(T item);
}
