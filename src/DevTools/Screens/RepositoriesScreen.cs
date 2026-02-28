using DevTools.Components.MenuPrompt;
using DevTools.Components.Screen;
using DevTools.Helpers;
using DevTools.Models;
using DevTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace DevTools.Screens;

class RepositoriesScreen(
    AppContext appContext,
    RepositoryScanner repositoryScanner,
    ConfigurationManager configurationManager,
    IAnsiConsole console,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider)
    : Screen(console)
{
    private readonly AppContext _appContext = appContext;
    private readonly RepositoryScanner _repositoryScanner = repositoryScanner;
    private readonly ConfigurationManager _configurationManager = configurationManager;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private MenuPrompt<GitRepoInfo>? menu;

    protected override Task OnInit(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetLocalNow().DateTime;

        var hints = new Markup(string.Join("[dim] | [/]", Hints.Exit!, Hints.Paths!, Hints.ConfigPath(_appContext.ConfigFilePath)!));

        AddElement(hints);
        AddElement(Text.Empty);

        menu = new MenuPrompt<GitRepoInfo>()
            .Title("Select a [green]repository[/] :")
            .UseChoiceProvider(FetchRepos)
            .UseConverter(r => RepoDisplayFormatter.Format(r, now, _appContext.Config.IsFavorite(r.Directory.FullName)))
            .HighlightStyle(Styles.Hightlight)
            .SearchHighlightStyle(Styles.SearchHightlight)
            .EnableSearch()
            .EnableWrapArount()
            .BindKey(ConsoleKey.Q, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.F2, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.F, ctx =>
            {
                _appContext.Config.ToggleFavorite(ctx.CurrentItem.Directory.FullName);
                _configurationManager.Save();
                ctx.Reset();
                return ScreenInputResult.Refresh;
            })
            .SetDefaultIndex(menu?.CurrentIndex ?? 0);

        AddElement(menu);

        return Task.CompletedTask;
    }

    protected override async Task OnExit(CancellationToken cancellationToken)
    {
        var submitContext = menu!.SubmitContext;

        if (submitContext is null)
        {
            return;
        }

        if (submitContext.KeyInfo.Key == ConsoleKey.F2)
        {
            await _serviceProvider.GetRequiredService<RepoPathsScreen>().ShowAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (submitContext.KeyInfo.Key == ConsoleKey.Enter)
        {
            var repo = submitContext.CurrentItem;

            if (submitContext.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)
                || submitContext.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                await _serviceProvider.GetRequiredService<RepositoryActionsScreen>().ShowAsync(repo, cancellationToken).ConfigureAwait(false);
                return;
            }

            Console.Execute(
                _appContext.Config.DefaultCommand.ProcessName,
                StringHelper.FormatIfNotNull(_appContext.Config.DefaultCommand.WorkingDirectory, repo.Directory.FullName),
                StringHelper.FormatIfNotNull(_appContext.Config.DefaultCommand.Arguments, repo.Directory.FullName));
            return;
        }

        if (submitContext.KeyInfo.Key == ConsoleKey.Q)
        {
            Console.ClearAndExit();
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
