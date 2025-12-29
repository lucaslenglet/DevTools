using Spectre.Console;

namespace DevTools.Components.MenuPrompt;

public static class MenuPromptExtensions
{
    extension<T>(MenuPrompt<T> menu)
        where T : notnull
    {
        public MenuPrompt<T> Title(string? title)
        {
            menu.Title = title;
            return menu;
        }

        public MenuPrompt<T> AddChoices(IEnumerable<T> choices)
        {
            foreach(var choice in choices)
            {
                menu.AddChoice(choice);
            }
            return menu;
        }

        public MenuPrompt<T> UseConverter(Func<T, string>? displaySelector)
        {
            menu.Converter = displaySelector;
            return menu;
        }

        public MenuPrompt<T> HighlightStyle(Style highlightStyle)
        {
            menu.HighlightStyle = highlightStyle;
            return menu;
        }

        public MenuPrompt<T> SearchHighlightStyle(Style searchHighlightStyle)
        {
            menu.SearchHighlightStyle = searchHighlightStyle;
            return menu;
        }

        public MenuPrompt<T> UseChoiceProvider(Func<IEnumerable<T>> provider)
        {
            menu.ChoiceProvider = provider;
            return menu;
        }

        public MenuPrompt<T> EnableSearch()
        {
            menu.SearchEnabled = true;
            return menu;
        }

        public MenuPrompt<T> EnableWrapArount()
        {
            menu.WrapAround = true;
            return menu;
        }

        public MenuPrompt<T> AddExitKeys(params ConsoleKey[] consoleKeys)
        {
            menu.ExitKeys = consoleKeys ?? [];
            return menu;
        }

        public MenuPrompt<T> AddActionKeys(params ConsoleKey[] consoleKeys)
        {
            menu.ActionKeys = consoleKeys ?? [];
            return menu;
        }

        public MenuPrompt<T> UseOnActionKeyPressed(Action<T, ConsoleKeyInfo> handler)
        {
            menu.OnActionKeyPressed = handler;
            return menu;
        }

        public MenuPrompt<T> SetDefaultIndex(int defaultIndex)
        {
            menu.DefaultIndex = defaultIndex;
            return menu;
        }
    }
}