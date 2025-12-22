namespace DevTools.Models;

record RepositoryAction(
    string Text,
    Action<GitRepoInfo>? Action = null,
    string? Color = null,
    string? Decoration = null)
{
    public string ToMarkup() => $"{Decoration} {Color ?? Spectre.Console.Color.White.ToMarkup()}".Trim();
}