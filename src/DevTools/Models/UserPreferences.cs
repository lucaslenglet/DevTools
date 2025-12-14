namespace DevTools.Models;

class UserPreferences
{
    public int Version { get; } = 1;
    public HashSet<FavoriteRepo> Favorites { get; init; } = [];

    public bool IsFavorite(string repoPath) => Favorites.Contains(new FavoriteRepo(repoPath));

    public void ToggleFavorite(string repoPath)
    {
        var existing = Favorites.FirstOrDefault(f => f.Path == repoPath);

        if (existing != null)
        {
            Favorites.Remove(existing);
        }
        else
        {
            Favorites.Add(new FavoriteRepo(repoPath));
        }
    }
}
