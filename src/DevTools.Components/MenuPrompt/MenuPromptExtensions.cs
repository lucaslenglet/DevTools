using DevTools.Components.Screen;
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
            foreach (var choice in choices)
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

        /// <summary>
        /// Registers a key binding. The handler receives a <see cref="MenuKeyContext{T}"/> with the
        /// current item, index, key info, and navigation/reset capabilities.
        /// </summary>
        public MenuPrompt<T> BindKey(ConsoleKey key, Func<MenuKeyContext<T>, ScreenInputResult> handler)
        {
            menu.BindKey(key, handler);
            return menu;
        }

        public MenuPrompt<T> SetDefaultIndex(int defaultIndex)
        {
            menu.DefaultIndex = defaultIndex;
            return menu;
        }
    }
}
