namespace DevTools.Models;

record GitRepoInfo(
    DirectoryInfo Repo,
    DateTime LastActivity,
    string Branch,
    int AheadBy,
    int BehindBy,
    bool HasTracking,
    string? ParentFolder
);
