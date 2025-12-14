using DevTools.Components.MenuPrompt;
using DevTools.Helpers;
using DevTools.Models;
using DevTools.Services;
using Spectre.Console;

namespace DevTools.Menus;

class RepositoriesMenu(
    AppContext appContext,
    RepositoryScanner repositoryScanner,
    ConfigurationManager configurationManager,
    TimeProvider timeProvider,
    IAnsiConsole console,
    RepositoryActionsMenu actionsMenu)
{
    public async Task ShowAsync(CancellationToken cancellationToken = default)
    {

        var repos = FetchRepos();
        var now = timeProvider.GetLocalNow().DateTime;
        var previousResult = SubmitResult.None;
        var defaultIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            console.ClearAndDisplayHint(Hints.Exit);

            if (previousResult == SubmitResult.Refresh)
            {
                repos = FetchRepos();
                now = timeProvider.GetLocalNow().DateTime;
            }

            var menu = new MenuPrompt<GitRepoInfo>()
                .Title($"Select a [green]repository[/] [dim]({appContext.GitReposPath})[/] :")
                .AddChoices(repos)
                .UseConverter(r => RepoDisplayFormatter.Format(r, now, appContext.UserPreferences.IsFavorite(r.Repo.FullName)))
                .HighlightStyle(Styles.Hightlight)
                .SearchHighlightStyle(Styles.SearchHightlight)
                .EnableSearch()
                .EnableWrapArount()
                .AddSubmitKeys(ConsoleKey.F, ConsoleKey.Q)
                .SetDefaultIndex(defaultIndex)
                ;

            var result = await menu
                .ShowAsync(console, cancellationToken)
                .ConfigureAwait(false);

            defaultIndex = result.OptionIndex;

            previousResult = await HandleSubmit(result.Data, result.ConsoleKeyInfo).ConfigureAwait(false);
        }
    }

    private List<GitRepoInfo> FetchRepos()
    {
        return repositoryScanner.Scan()
            .OrderByDescending(r => appContext.UserPreferences.Favorites.Contains(new FavoriteRepo(r.Repo.FullName)))
            .ThenByDescending(r => r.LastActivity)
            .ToList();
    }

    private async Task<SubmitResult> HandleSubmit(GitRepoInfo repo, ConsoleKeyInfo consoleKeyInfo)
    {
        if (consoleKeyInfo.Key == ConsoleKey.F)
        {
            appContext.UserPreferences.ToggleFavorite(repo.Repo.FullName);
            configurationManager.Save();
            return SubmitResult.Refresh;
        }

        if (consoleKeyInfo.Key == ConsoleKey.Enter)
        {
            if (consoleKeyInfo.Modifiers == ConsoleModifiers.None)
            {
                RepositoryActionsMenu.StartLazygit(repo);
            }
            else if (consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)
                || consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                await actionsMenu.ShowAsync(repo).ConfigureAwait(false);
            }

            return SubmitResult.None;
        }

        if (consoleKeyInfo.Key == ConsoleKey.Q)
        {
            console.ClearAndExit();
        }

        return SubmitResult.None;
    }

    private enum SubmitResult
    {
        None,
        Refresh,
    }
}