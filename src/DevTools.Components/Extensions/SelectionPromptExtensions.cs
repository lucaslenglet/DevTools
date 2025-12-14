using Spectre.Console;

namespace DevTools.Components.Extensions;

public static class SelectionPromptExtensions
{
    extension<T>(SelectionPrompt<T> select)
        where T : notnull
    {
        public SelectionPrompt<T> SearchHighlightStyle(Style searchHighlightStyle)
        {
            select.SearchHighlightStyle = searchHighlightStyle;
            return select;
        }
    }
}