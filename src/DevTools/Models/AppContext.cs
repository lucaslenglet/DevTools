namespace DevTools.Models;

class AppContext
{
    public string GitReposPath { get; init; } = string.Empty;
    public string ConfigFilePath { get; init; } = string.Empty;
    public Config Config { get; init; } = new();
}