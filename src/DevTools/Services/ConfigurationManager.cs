using DevTools.Helpers;
using DevTools.Models;
using Spectre.Console;

namespace DevTools.Services;

class ConfigurationManager(AppContext context)
{
    private const string ExitPrompt = "[dim]Press any key to quit...[/]";
    private const string EnvGitReposPath = "GIT_REPOS_PATH";
    private const string Folder = "DevTools";
    private const string FileName = "config.yml";

    public static AppContext? InitializeAppContext()
    {
        var configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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

        var config = Load(configFilePath);

        return new AppContext
        {
            GitReposPath = gitReposPath,
            ConfigFilePath = configFilePath,
            Config = config
        };
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(context.ConfigFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var yml = SerdeHelper.Serialize(context.Config);
        File.WriteAllText(context.ConfigFilePath, yml);
    }

    private static Config Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new Config();
        }

        var yml = File.ReadAllText(filePath);
        var prefs = SerdeHelper.Deserialize<Config>(yml);

        if (prefs is null)
        {
            throw new Exception("User preference failed to load.");
        }

        return prefs;
    }
}