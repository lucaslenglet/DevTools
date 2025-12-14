using System.Text.Json;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Services;

class ConfigurationManager(AppContext context)
{
    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    private const string ExitPrompt = "[dim]Press any key to quit...[/]";
    private const string EnvGitReposPath = "GIT_REPOS_PATH";
    private const string Folder = "DevTools";
    private const string FileName = "config.json";

    public static AppContext? InitializeAppContext()
    {
        var userPreferencesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Folder,
            FileName
        );

        var gitReposPath = Environment.GetEnvironmentVariable(EnvGitReposPath);

        if (string.IsNullOrWhiteSpace(gitReposPath))
        {
            AnsiConsole.MarkupLine($"[red]The environment variable {EnvGitReposPath} is not defined.[/]");
            AnsiConsole.MarkupLine("[yellow]Please define this variable with the path to your Git repositories.[/]");
            AnsiConsole.MarkupLine(ExitPrompt);
            Console.ReadKey(true);
            return null;
        }

        if (!Directory.Exists(gitReposPath))
        {
            AnsiConsole.MarkupLine($"[red]The directory '{gitReposPath}' does not exist.[/]");
            AnsiConsole.MarkupLine(ExitPrompt);
            Console.ReadKey(true);
            return null;
        }

        var preferences = Load(userPreferencesFilePath);

        return new AppContext
        {
            GitReposPath = gitReposPath,
            UserPreferencesFilePath = userPreferencesFilePath,
            UserPreferences = preferences
        };
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(context.UserPreferencesFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(context.UserPreferences, JsonOptions);
        File.WriteAllText(context.UserPreferencesFilePath, json);
    }

    private static UserPreferences Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new UserPreferences();
        }

        var json = File.ReadAllText(filePath);
        var prefs = JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions);

        if (prefs is null)
        {
            throw new Exception("User preference failed to load.");
        }

        return prefs;
    }
}