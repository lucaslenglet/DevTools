# Copilot Instructions for DevTools (Git Repos Manager)

## Project Overview
- **Terminal-based Git repository browser and launcher** with favorites management and lazygit integration.
- Built with **.NET 10**, using [Spectre.Console](https://spectreconsole.net/) for UI and [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) for Git operations.
- Main entry point: `src/DevTools/Program.cs`.
- Core logic is organized into: `Menus/`, `Models/`, `Services/`, `Helpers/`, and `Extensions/`.

## Key Architectural Patterns
- **Menu-driven navigation**: All user interaction flows through menu classes in `Menus/`.
- **Favorites and preferences**: Managed via `Models/FavoriteRepo.cs` and `Models/UserPreferences.cs`, persisted as JSON in platform-specific app data folders.
- **Repository scanning**: Handled by `Services/RepositoryScanner.cs`, which discovers Git repos up to two levels deep under the path in the `GIT_REPOS_PATH` environment variable.
- **Color-coded branch display**: See `Styles.cs` and `Hints.cs` for branch color logic and display hints.
- **Integration with lazygit**: Repositories are opened in lazygit via shell commands from the menu actions.

## Developer Workflows
- **Build/Run**: Use `dotnet run` from the `src/DevTools/` directory. The app requires the `GIT_REPOS_PATH` environment variable to be set.
- **Debug**: Open `Program.cs` in VS Code and press F5 (requires C# extension).
- **Dependencies**: NuGet packages are restored automatically on build; see `DevTools.csproj` for details.
- **Testing**: No formal test suite; manual testing via interactive terminal use is standard.

## Project-Specific Conventions
- **Favorites and settings** are stored in JSON files in platform-specific app data locations (see README for paths).
- **Branch types** are color-coded by prefix (e.g., `feature/*` is yellow, `hotfix/*` is magenta).
- **Menu navigation** uses Spectre.Console prompts and custom extensions in `Extensions/` and `DevTools.Components/`.
- **Repository list** is always sorted with favorites on top.
- **No direct file system writes** outside of the app data JSON files.

## Integration Points
- **lazygit**: Must be installed and available in PATH; invoked for repo actions.
- **Spectre.Console**: Used for all terminal UI, including menus, prompts, and color output.
- **LibGit2Sharp**: Used for all Git operations (status, branch, etc.).

## Examples
- To add a new menu, create a class in `Menus/` and register it in `Program.cs`.
- To add a new repo display field, update `Helpers/RepoDisplayFormatter.cs` and adjust menu rendering logic.
- To change branch color logic, edit `Styles.cs` and `Hints.cs`.

## References
- See [README.md](../../README.md) for user-facing documentation and environment setup.
- See `src/DevTools/Models/` for data models and settings structure.
- See `src/DevTools/Services/RepositoryScanner.cs` for repo discovery logic.

---
For questions about project structure or conventions, review the README and referenced files above.
