namespace DevTools.Models;

class AppContext
{
    public string GitReposPath { get; init; } = string.Empty;
    public string UserPreferencesFilePath { get; init; } = string.Empty;
    public UserPreferences UserPreferences { get; init; } = new();
}