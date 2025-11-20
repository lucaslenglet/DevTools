#:package Spectre.Console@0.54.0
#:package LibGit2Sharp@0.31.0

using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using Spectre.Console;
using LibGit2Sharp;

// ============================
// Entry Point
// ============================
Console.CancelKeyPress += (sender, e) =>
{
    Console.Clear();
    e.Cancel = true;
    Environment.Exit(0);
};

var context = AppContext.Initialize();
if (context == null) return;

var app = new GitReposApp(context);
app.Run();

// ============================
// Configuration and Constants
// ============================
static class AppConfig
{
    public const string EnvVariableName = "GIT_REPOS_PATH";
    public const string FavoritesFolder = "GitRepos";
    public const string FavoritesFileName = "favorites.json";
    public const string BackOptionText = "[dim]← Back[/]";
    public const string ExitPrompt = "\n[dim]Press any key to quit...[/]";
    public const string QuitHint = "[dim]Press Ctrl+C to quit[/]\n";
    
    public const string MainMenuChooseRepo = "Choose a repository";
    public const string MainMenuManageFavorites = "Manage favorites";
    public const string MainMenuSettings = "Settings";
    
    public const int SelectionPageSize = 30;
    
    public static readonly string[] GitActivityFiles = ["FETCH_HEAD", "HEAD", "index", "ORIG_HEAD"];
    
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };
}

static class BranchColors
{
    public static string GetColor(string branch)
    {
        var branchLower = branch.ToLowerInvariant();
        return branchLower switch
        {
            "main" or "master" => "steelblue",
            "develop" => "lightseagreen",
            _ when branchLower.StartsWith("feature/") || branchLower.StartsWith("feat/") => "yellow",
            _ when branchLower.StartsWith("bugfix/") || branchLower.StartsWith("fix/") => "red",
            _ when branchLower.StartsWith("hotfix/") => "magenta",
            _ when branchLower.StartsWith("release/") => "cyan",
            _ => "white"
        };
    }
}

// ============================
// Data Models
// ============================
record GitRepoInfo(
    DirectoryInfo Repo,
    DateTime LastActivity,
    string Branch,
    int AheadBy,
    int BehindBy,
    bool HasTracking,
    string? ParentFolder
);

record FavoriteRepo(
    string Path,
    DateTime AddedAt,
    string? Alias = null,
    string? Category = null,
    int? Priority = null
);

class UserPreferences
{
    public int Version { get; set; } = 1;
    public List<FavoriteRepo> Favorites { get; set; } = new();
    public bool SkipMainMenu { get; set; }
    public DateTime LastModified { get; set; } = DateTime.Now;
    public Dictionary<string, string> Settings { get; set; } = new();
}

// ============================
// Main Classes
// ============================
class AppContext
{
    public string GitReposPath { get; init; } = string.Empty;
    public string FavoritesFilePath { get; init; } = string.Empty;
    public UserPreferences Preferences { get; set; } = new();
    
    public static AppContext? Initialize()
    {
        var favoritesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConfig.FavoritesFolder,
            AppConfig.FavoritesFileName
        );
        
        var gitReposPath = Environment.GetEnvironmentVariable(AppConfig.EnvVariableName);
        
        if (string.IsNullOrWhiteSpace(gitReposPath))
        {
            AnsiConsole.MarkupLine($"[red]The environment variable {AppConfig.EnvVariableName} is not defined.[/]");
            AnsiConsole.MarkupLine("[yellow]Please define this variable with the path to your Git repositories.[/]");
            AnsiConsole.MarkupLine(AppConfig.ExitPrompt);
            Console.ReadKey(true);
            return null;
        }
        
        if (!Directory.Exists(gitReposPath))
        {
            AnsiConsole.MarkupLine($"[red]The directory '{gitReposPath}' does not exist.[/]");
            AnsiConsole.MarkupLine(AppConfig.ExitPrompt);
            Console.ReadKey(true);
            return null;
        }
        
        var preferences = FavoritesManager.Load(favoritesFilePath);
        
        return new AppContext
        {
            GitReposPath = gitReposPath,
            FavoritesFilePath = favoritesFilePath,
            Preferences = preferences
        };
    }
}

class GitReposApp
{
    private readonly AppContext _context;
    private List<GitRepoInfo> _repos = new();
    
    public GitReposApp(AppContext context)
    {
        _context = context;
    }
    
    public void Run()
    {
        _repos = RepositoryScanner.Scan(_context.GitReposPath);
        
        if (_repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No Git repository found.[/]");
            AnsiConsole.MarkupLine(AppConfig.ExitPrompt);
            Console.ReadKey(true);
            return;
        }

        // If the option is enabled, go directly to selection
        if (_context.Preferences.SkipMainMenu)
        {
            SelectAndOpenRepository();
        }
        
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine(AppConfig.QuitHint);
            
            var mainChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What do you want to do?")
                    .AddChoices([AppConfig.MainMenuChooseRepo, AppConfig.MainMenuManageFavorites, AppConfig.MainMenuSettings])
                    .HighlightStyle(StyleHelper.HighlightStyle));
            
            _repos = RepositoryScanner.Scan(_context.GitReposPath);
            
            if (mainChoice == AppConfig.MainMenuManageFavorites)
            {
                ManageFavorites();
            }
            else if (mainChoice == AppConfig.MainMenuSettings)
            {
                ManageSettings();
            }
            else
            {
                SelectAndOpenRepository();
            }
        }
    }
    
    private void SelectAndOpenRepository()
    {
        Console.Clear();
        AnsiConsole.MarkupLine(AppConfig.QuitHint);
        
        var selectedRepo = RepoSelector.Select(
            _repos,
            _context.Preferences,
            _context.GitReposPath,
            "Select a [green]repository[/]");
        
        if (selectedRepo == null) return;
        
        LaunchLazygit(selectedRepo.Repo.FullName);
    }
    
    private void ManageFavorites()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine(AppConfig.QuitHint);
            
            var selectedRepo = RepoSelector.Select(
                _repos,
                _context.Preferences,
                _context.GitReposPath,
                "Select a [green]repository[/] to favorite ⭐");
            
            if (selectedRepo == null) break;
            
            FavoritesManager.Toggle(_context.Preferences, selectedRepo.Repo.FullName);
            FavoritesManager.Save(_context.FavoritesFilePath, _context.Preferences);
        }
    }

    private void ManageSettings()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine(AppConfig.QuitHint);
            
            var skipMainMenu = _context.Preferences.SkipMainMenu;
            var status = skipMainMenu ? "[green]YES[/]" : "[red]NO[/]";
            
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Settings")
                    .AddChoices([
                        AppConfig.BackOptionText,
                        $"Skip main menu on startup: {status}"
                    ])
                    .HighlightStyle(StyleHelper.HighlightStyle));
            
            if (choice == AppConfig.BackOptionText) break;
            
            _context.Preferences.SkipMainMenu = !skipMainMenu;
            FavoritesManager.Save(_context.FavoritesFilePath, _context.Preferences);
        }
    }
    
    private static void LaunchLazygit(string repoPath)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "lazygit",
            WorkingDirectory = repoPath,
            UseShellExecute = false
        };
        Process.Start(processInfo)?.WaitForExit();
    }
}

// ============================
// Services
// ============================
static class RepositoryScanner
{
    public static List<GitRepoInfo> Scan(string gitReposPath)
    {
        var gitDirectories = FindGitDirectories(gitReposPath);
        
        var repos = new ConcurrentBag<GitRepoInfo>();
        Parallel.ForEach(gitDirectories, item =>
        {
            var info = GetGitRepoInfo(item.dir, item.parentFolder);
            repos.Add(info);
        });
        
        return repos.OrderByDescending(x => x.LastActivity).ToList();
    }
    
    private static List<(DirectoryInfo dir, string? parentFolder)> FindGitDirectories(string rootPath)
    {
        var gitDirectories = new List<(DirectoryInfo dir, string? parentFolder)>();
        var directories = Directory.EnumerateDirectories(rootPath).Select(dir => new DirectoryInfo(dir));
        
        foreach (var dir in directories)
        {
            if (IsGitRepository(dir.FullName))
            {
                gitDirectories.Add((dir, null));
            }
            else
            {
                var subRepos = Directory.EnumerateDirectories(dir.FullName)
                    .Select(subDir => new DirectoryInfo(subDir))
                    .Where(subDir => IsGitRepository(subDir.FullName))
                    .Select(subDir => (subDir, (string?)dir.Name));
                
                gitDirectories.AddRange(subRepos);
            }
        }
        
        return gitDirectories;
    }
    
    private static bool IsGitRepository(string path) =>
        Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));
    
    private static GitRepoInfo GetGitRepoInfo(DirectoryInfo repoDir, string? parentFolder)
    {
        try
        {
            using var repo = new Repository(repoDir.FullName);
            
            var lastActivity = GetLastActivityTime(repoDir);
            var branch = repo.Head.FriendlyName;
            var (aheadBy, behindBy, hasTracking) = GetTrackingInfo(repo);
            
            return new GitRepoInfo(repoDir, lastActivity, branch, aheadBy, behindBy, hasTracking, parentFolder);
        }
        catch
        {
            return new GitRepoInfo(repoDir, DateTime.MinValue, "", 0, 0, false, parentFolder);
        }
    }
    
    private static DateTime GetLastActivityTime(DirectoryInfo repoDir)
    {
        var gitDirPath = Path.Combine(repoDir.FullName, ".git");
        if (!Directory.Exists(gitDirPath))
            return repoDir.LastWriteTime;
        
        var lastWriteTimes = new List<DateTime>();
        
        foreach (var fileName in AppConfig.GitActivityFiles)
        {
            var filePath = Path.Combine(gitDirPath, fileName);
            if (File.Exists(filePath))
            {
                lastWriteTimes.Add(File.GetLastWriteTime(filePath));
            }
        }
        
        var refsHeads = Path.Combine(gitDirPath, "refs", "heads");
        if (Directory.Exists(refsHeads))
        {
            lastWriteTimes.AddRange(
                Directory.EnumerateFiles(refsHeads, "*", SearchOption.AllDirectories)
                    .Select(File.GetLastWriteTime));
        }
        
        return lastWriteTimes.Count > 0 ? lastWriteTimes.Max() : repoDir.LastWriteTime;
    }
    
    private static (int aheadBy, int behindBy, bool hasTracking) GetTrackingInfo(Repository repo)
    {
        var trackingBranch = repo.Head.TrackedBranch;
        if (trackingBranch == null)
            return (0, 0, false);
        
        return (
            repo.Head.TrackingDetails.AheadBy ?? 0,
            repo.Head.TrackingDetails.BehindBy ?? 0,
            true
        );
    }
}

static class FavoritesManager
{
    public static UserPreferences Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new UserPreferences();
        }

        var json = File.ReadAllText(filePath);
        var prefs = JsonSerializer.Deserialize<UserPreferences>(json, AppConfig.JsonOptions);

        if (prefs is null)
        {
            throw new Exception("User preference failed to load.");
        }

        return prefs;
    }

    public static void Save(string filePath, UserPreferences preferences)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        preferences.LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(preferences, AppConfig.JsonOptions);
        File.WriteAllText(filePath, json);
    }
    
    public static void Toggle(UserPreferences preferences, string repoPath)
    {
        var existing = preferences.Favorites.FirstOrDefault(f => f.Path == repoPath);
        
        if (existing != null)
        {
            preferences.Favorites.Remove(existing);
        }
        else
        {
            preferences.Favorites.Add(new FavoriteRepo(repoPath, DateTime.Now));
        }
    }
    
    public static bool IsFavorite(UserPreferences preferences, string repoPath)
    {
        return preferences.Favorites.Any(f => f.Path == repoPath);
    }
    
    public static HashSet<string> GetFavoritePaths(UserPreferences preferences)
    {
        return preferences.Favorites.Select(f => f.Path).ToHashSet();
    }
}

static class RepoSelector
{
    public static GitRepoInfo? Select(
        List<GitRepoInfo> repos,
        UserPreferences preferences,
        string gitReposPath,
        string title)
    {
        var now = DateTime.Now;
        var favoritePaths = FavoritesManager.GetFavoritePaths(preferences);
        var sortedRepos = GetSortedRepos(repos, favoritePaths);
        var backOption = CreateBackOption(gitReposPath);
        var allItems = CreateItemsList(backOption, sortedRepos);
        var repoDisplays = CreateDisplayDictionary(allItems, backOption, now, preferences);
        
        var selectionPrompt = CreateSelectionPrompt(title, gitReposPath, allItems, repoDisplays);
        var selectedItem = AnsiConsole.Prompt(selectionPrompt);
        
        return selectedItem == backOption ? null : selectedItem;
    }
    
    private static List<GitRepoInfo> GetSortedRepos(List<GitRepoInfo> repos, HashSet<string> favoritePaths) =>
        repos
            .OrderByDescending(r => favoritePaths.Contains(r.Repo.FullName))
            .ThenByDescending(r => r.LastActivity)
            .ToList();
    
    private static GitRepoInfo CreateBackOption(string gitReposPath) =>
        new(new DirectoryInfo(gitReposPath), DateTime.MinValue, "", 0, 0, false, null);
    
    private static List<GitRepoInfo> CreateItemsList(GitRepoInfo backOption, List<GitRepoInfo> sortedRepos)
    {
        var items = new List<GitRepoInfo>(sortedRepos.Count + 1) { backOption };
        items.AddRange(sortedRepos);
        return items;
    }
    
    private static Dictionary<string, string> CreateDisplayDictionary(
        List<GitRepoInfo> allItems,
        GitRepoInfo backOption,
        DateTime now,
        UserPreferences preferences) =>
        allItems.ToDictionary(
            repo => repo.Repo.FullName,
            repo => repo == backOption
                ? AppConfig.BackOptionText
                : RepoDisplayFormatter.Format(repo, now, FavoritesManager.IsFavorite(preferences, repo.Repo.FullName)));
    
    private static SelectionPrompt<GitRepoInfo> CreateSelectionPrompt(
        string title,
        string gitReposPath,
        List<GitRepoInfo> items,
        Dictionary<string, string> displays)
    {
        var prompt = new SelectionPrompt<GitRepoInfo>()
            .Title($"{title} ([dim]{gitReposPath}[/]) :")
            .PageSize(AppConfig.SelectionPageSize)
            .MoreChoicesText("[dim](Move to see more choices)[/]")
            .EnableSearch()
            .SearchPlaceholderText("[dim]Search...[/]")
            .AddChoices(items)
            .UseConverter(item => displays[item.Repo.FullName])
            .HighlightStyle(StyleHelper.HighlightStyle);
        
        prompt.SearchHighlightStyle = StyleHelper.SearchHighlightStyle;
        
        return prompt;
    }
}

static class RepoDisplayFormatter
{
    public static string Format(GitRepoInfo repo, DateTime now, bool isFavorite)
    {
        var timeStr = FormatTimeAgo(now - repo.LastActivity, repo.LastActivity);
        var branchColor = BranchColors.GetColor(repo.Branch);
        var (remoteStatus, remoteColor) = GetRemoteStatus(repo);
        var favoriteIcon = isFavorite ? "⭐ " : "   ";
        var displayName = FormatDisplayName(repo);
        
        return $"{favoriteIcon}{displayName} [dim]{timeStr.PadRight(15)}[/][{remoteColor}]{remoteStatus.PadRight(10)}[/][{branchColor}]{repo.Branch.EscapeMarkup().PadRight(50)}[/]";
    }
    
    private static string FormatTimeAgo(TimeSpan timeAgo, DateTime lastActivity) =>
        timeAgo switch
        {
            { TotalMinutes: < 1 } => $"{(int)timeAgo.TotalSeconds}s ago",
            { TotalHours: < 1 } => $"{(int)timeAgo.TotalMinutes}m ago",
            { TotalDays: < 1 } => $"{(int)timeAgo.TotalHours}h ago",
            { TotalDays: < 7 } => $"{(int)timeAgo.TotalDays}d ago",
            _ => lastActivity.ToString("yyyy/MM/dd")
        };
    
    private static (string status, string color) GetRemoteStatus(GitRepoInfo repo) =>
        (repo.AheadBy, repo.BehindBy, repo.HasTracking) switch
        {
            ( > 0, > 0, true) => ($"↑{repo.AheadBy}↓{repo.BehindBy}", "orange1"),
            ( > 0, 0, true) => ($"↑{repo.AheadBy}", "yellow"),
            (0, > 0, true) => ($"↓{repo.BehindBy}", "red"),
            (0, 0, true) => ("✓", "green"),
            _ => ("", "dim")
        };
    
    private static string FormatDisplayName(GitRepoInfo repo) =>
        string.IsNullOrEmpty(repo.ParentFolder)
            ? repo.Repo.Name.PadRight(60)
            : $"[dim]{repo.ParentFolder.EscapeMarkup()}[/] > {repo.Repo.Name.EscapeMarkup()}".PadRight(68);
}

static class StyleHelper
{
    public static readonly Style HighlightStyle = new(foreground: Color.Black, background: Color.White);
    public static readonly Style SearchHighlightStyle = new(foreground: Color.Black, background: Color.Cyan1);
}