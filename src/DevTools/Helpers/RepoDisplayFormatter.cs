using DevTools.Models;
using Spectre.Console;

namespace DevTools.Helpers;

static class RepoDisplayFormatter
{
    public static string Format(GitRepoInfo repo, DateTime now, bool isFavorite)
    {
        var favoriteIcon = isFavorite ? ":fire: " : "   ";
        var displayName = FormatDisplayName(repo);
        var timeStr = FormatTimeAgo(now - repo.LastActivity, repo.LastActivity);
        var (remoteStatus, remoteColor) = GetRemoteStatus(repo);
        var branchColor = GetBranchColor(repo.Branch);

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

    private static string GetBranchColor(string branch)
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
