using System.Collections.Concurrent;
using DevTools.Models;
using LibGit2Sharp;
using Spectre.Console;

namespace DevTools.Services;

class RepositoryScanner(AppContext context)
{
    private static readonly string[] GitActivityFiles = ["FETCH_HEAD", "HEAD", "index", "ORIG_HEAD"];

    public List<GitRepoInfo> Scan()
    {
        var repos = new ConcurrentBag<GitRepoInfo>();

        foreach (var rootPath in context.Config.RepoPaths)
        {
            Parallel.ForEach(FindGitDirectories(rootPath), item =>
            {
                repos.Add(GetGitRepoInfo(item.dir, item.parentFolder));
            });
        }

        return repos.OrderByDescending(x => x.LastActivity).ToList();
    }

    private static IEnumerable<(DirectoryInfo dir, string? parentFolder)> FindGitDirectories(string rootPath)
    {
        var gitDirectories = new List<(DirectoryInfo dir, string? parentFolder)>();
        var directories = Directory.EnumerateDirectories(rootPath).Select(dir => new DirectoryInfo(dir));

        foreach (var dir in directories)
        {
            if (IsGitRepository(dir.FullName))
            {
                yield return (dir, null);
            }
            else
            {
                var subRepos = Directory.EnumerateDirectories(dir.FullName)
                    .Select(subDir => new DirectoryInfo(subDir))
                    .Where(subDir => IsGitRepository(subDir.FullName))
                    .Select(subDir => (subDir, (string?)dir.Name));

                foreach(var subRepo in subRepos)
                {
                    yield return subRepo;
                }
            }
        }
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

        foreach (var fileName in GitActivityFiles)
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
