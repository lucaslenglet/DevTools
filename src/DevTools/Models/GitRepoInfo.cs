namespace DevTools.Models;

record GitRepoInfo(
    DirectoryInfo Directory,
    DateTime LastActivity,
    string Branch,
    int AheadBy,
    int BehindBy,
    bool HasTracking,
    string? ParentFolder
);
