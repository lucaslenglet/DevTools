using DevTools.Components.MenuPrompt;
using DevTools.Components.Screen;
using DevTools.Helpers;
using DevTools.Models;
using DevTools.Services;
using Spectre.Console;

namespace DevTools.Menus;

class RepositoriesScreen : Screen
{
    private readonly AppContext _appContext;
    private readonly RepositoryScanner _repositoryScanner;
    private readonly ConfigurationManager _configurationManager;
    private readonly IAnsiConsole _console;
    private readonly TimeProvider _timeProvider;
    
    private MenuPrompt<GitRepoInfo> menu;
    private List<GitRepoInfo> repos;

    public RepositoriesScreen(
        AppContext appContext,
        RepositoryScanner repositoryScanner,
        ConfigurationManager configurationManager,
        IAnsiConsole console,
        TimeProvider timeProvider)
    {
        _appContext = appContext;
        _repositoryScanner = repositoryScanner;
        _configurationManager = configurationManager;
        _console = console;
        _timeProvider = timeProvider;
    }

    protected override Task OnInit()
    {
        var now = _timeProvider.GetLocalNow().DateTime;

        var hints = new Markup(string.Join("[dim] | [/]", Hints.Exit!, Hints.ConfigPath(_appContext.ConfigFilePath)!));
        
        AddElement(hints);
        AddElement(Text.Empty);

        menu = new MenuPrompt<GitRepoInfo>()
            .Title($"Select a [green]repository[/] [dim]({_appContext.GitReposPath})[/] :")
            .UseChoiceProvider(FetchRepos)
            .UseConverter(r => RepoDisplayFormatter.Format(r, now, _appContext.Config.IsFavorite(r.Directory.FullName)))
            .HighlightStyle(Styles.Hightlight)
            .SearchHighlightStyle(Styles.SearchHightlight)
            .EnableSearch()
            .EnableWrapArount()
            .AddExitKeys(ConsoleKey.Q!)
            .AddActionKeys(ConsoleKey.F!)
            .UseOnActionKeyPressed(OnActionKeyPressed)
            .SetDefaultIndex(menu?.CurrentIndex ?? 0);

        AddElement(menu);

        return Task.CompletedTask;
    }

    protected override Task OnExit()
    {
        var exitKey = menu.LastKey;

        if (exitKey is null)
        {
            return Task.CompletedTask;
        }

        if (exitKey.Value.Key == ConsoleKey.Enter)
        {
            if (exitKey.Value.Modifiers == ConsoleModifiers.None)
            {
                // RepositoryActionsMenu.CommandToAction(appContext.Config.DefaultCommand).Action!(repo);
            }
            else if (exitKey.Value.Modifiers.HasFlag(ConsoleModifiers.Control)
                || exitKey.Value.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                // await actionsMenu.ShowAsync(repo).ConfigureAwait(false);
            }

            return Task.CompletedTask;
        }

        if (exitKey.Value.Key == ConsoleKey.Q)
        {
            _console.ClearAndExit();
        }

        return Task.CompletedTask;
    }

    private void OnActionKeyPressed(GitRepoInfo repo, ConsoleKeyInfo consoleKey)
    {
        if (consoleKey.Key == ConsoleKey.F)
        {
            _appContext.Config.ToggleFavorite(repo.Directory.FullName);
            _configurationManager.Save();
            return;
        }
    }

    private List<GitRepoInfo> FetchRepos()
    {
        return _repositoryScanner.Scan()
            .OrderByDescending(r => _appContext.Config.Favorites.Contains(r.Directory.FullName))
            .ThenByDescending(r => r.LastActivity)
            .ToList();
    }
}