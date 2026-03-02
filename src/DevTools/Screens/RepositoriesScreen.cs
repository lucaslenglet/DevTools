using System.Diagnostics;
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

    public async Task ShowForeverAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await ShowAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    protected override Task OnInit(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetLocalNow().DateTime;

        var hints = new Markup(Hints.Join(Hints.Exit!, Hints.Rename!, Hints.Paths!, Hints.ConfigPath(_appContext.ConfigFilePath)!));

        AddElement(hints);
        AddElement(Text.Empty);

        menu = new MenuPrompt<GitRepoInfo>()
            .Title("Select a [green]repository[/] :")
            .UseChoiceProvider(FetchRepos)
            .UseConverter(r => RepoDisplayFormatter.Format(r, now, _appContext.Config))
            .HighlightStyle(Styles.Hightlight)
            .SearchHighlightStyle(Styles.SearchHightlight)
            .EnableSearch()
            .EnableWrapArount()
            .BindKey(ConsoleKey.Q, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.F2, _ => ScreenInputResult.Exit)
            .BindKey(ConsoleKey.R, _ => ScreenInputResult.Exit)
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
            await _serviceProvider.GetRequiredService<RepoPathsScreen>().ShowSettingsAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (submitContext.KeyInfo.Key == ConsoleKey.R)
        {
            var repo = submitContext.CurrentItem;
            var originalName = repo.Directory.Name;
            var currentDisplayName = _appContext.Config.GetDisplayName(repo.Directory.FullName);
            var prompt = $"Enter [green]display name[/] for [blue]{originalName.EscapeMarkup()}[/] :";

            var inputScreen = new TextInputScreen(Console, prompt, currentDisplayName ?? originalName);
            await inputScreen.ShowAsync(cancellationToken).ConfigureAwait(false);

            if (inputScreen.Cancelled
                || (currentDisplayName == null && string.IsNullOrWhiteSpace(inputScreen.Value))
                || currentDisplayName?.Equals(inputScreen.Value, StringComparison.Ordinal) is true)
            {
                return;
            }

            _appContext.Config.SetDisplayName(repo.Directory.FullName, inputScreen.Value);
            _configurationManager.Save();
        }
        else if (submitContext.KeyInfo.Key == ConsoleKey.Enter)
        {
            var repo = submitContext.CurrentItem;

            var command = _appContext.Config.DefaultCommand;

            if (submitContext.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)
                || submitContext.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                var actionsScreen = _serviceProvider.GetRequiredService<RepositoryCommandsScreen>();
                command = await actionsScreen.PromptAsync(repo, cancellationToken).ConfigureAwait(false);
            }

            if (command is not null)
            {
                ExecuteCommand(command.ProcessName,
                    StringHelper.FormatIfNotNull(command.WorkingDirectory, repo.Directory.FullName),
                    StringHelper.FormatIfNotNull(command.Arguments, repo.Directory.FullName));
            }
        }
        else if (submitContext.KeyInfo.Key == ConsoleKey.Q)
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

    private void ExecuteCommand(string fileName, string? workingDirectory, string? arguments)
    {
        Console.Clear();
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            UseShellExecute = false,
        })?.WaitForExit();
        Console.Clear();
    }
}
