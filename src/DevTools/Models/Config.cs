using YamlDotNet.Serialization;

namespace DevTools.Models;

class Config
{
    [YamlIgnore]
    public static int CurrentVersion => 1;
    public int Version { get; init; } = CurrentVersion;
    public List<string> RepoPaths { get; init; } = [];
    public HashSet<string> Favorites { get; init; } = [];
    public Dictionary<string, string> DisplayNames { get; init; } = [];
    public ConfigCommand DefaultCommand { get; init; } = LazygitCommand;

    public List<ConfigCommand> CustomCommands { get; init; } = [
        new ConfigCommand
        {
            ProcessName = "pwsh",
            Name = "VS Code",
            Color = Color.LightSteelBlue.ToMarkup(),
            Arguments = "-Command \"code {0}\"",
        },
        new ConfigCommand
        {
            ProcessName = "claude",
            Name = "Claude Code",
            Color = Color.DarkOrange.ToMarkup(),
            WorkingDirectory = "{0}",
        },
        new ConfigCommand
        {
            ProcessName = "copilot",
            Name = "Copilot",
            Color = Color.Silver.ToMarkup(),
            WorkingDirectory = "{0}",
        },
        new ConfigCommand
        {
            ProcessName = "codex",
            Name = "Codex",
            Color = Color.Grey63.ToMarkup(),
            WorkingDirectory = "{0}",
        },
        new ConfigCommand
        {
            ProcessName = "vibe",
            Name = "Vibe",
            Color = Color.Orange1.ToMarkup(),
            WorkingDirectory = "{0}",
        },
        new ConfigCommand
        {
            ProcessName = "pwsh",
            Name = "Powershell",
            Color = Color.Blue.ToMarkup(),
            WorkingDirectory = "{0}",
        },
        LazygitCommand,
        new ConfigCommand
        {
            ProcessName = "explorer.exe",
            Name = "File Explorer",
            Color = Color.Green.ToMarkup(),
            Arguments = "{0}",
        },
    ];

    private static ConfigCommand LazygitCommand { get; } = new ConfigCommand
    {
        ProcessName = "lazygit",
        Name = "Lazygit",
        WorkingDirectory = "{0}",
        Color = "hotpink",
    };

    public bool IsFavorite(string repoPath) => Favorites.Contains(repoPath);

    public void ToggleFavorite(string repoPath)
    {
        var existing = Favorites.FirstOrDefault(f => f == repoPath);

        if (existing != null)
        {
            Favorites.Remove(existing);
        }
        else
        {
            Favorites.Add(repoPath);
        }
    }

    public string? GetDisplayName(string repoPath) =>
        DisplayNames.TryGetValue(repoPath, out var name) ? name : null;

    public void SetDisplayName(string repoPath, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            DisplayNames.Remove(repoPath);
        else
            DisplayNames[repoPath] = displayName.Trim();
    }
}