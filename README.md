# Git Repos Manager

A powerful terminal-based Git repository browser and launcher with favorites management and lazygit integration.

## Features

- 🔍 **Automatic Discovery** - Scans and lists all Git repositories in your configured directory
- ⭐ **Favorites System** - Mark frequently used repositories for quick access
- 📊 **Rich Information Display** - Shows branch name, last activity, and remote tracking status
- 🎨 **Color-Coded Branches** - Visual distinction for main, develop, feature, bugfix, hotfix, and release branches
- 🚀 **Lazy Git Integration** - Opens selected repositories directly in lazy git
- 🔎 **Search Functionality** - Quickly filter repositories by name
- ⚡ **Parallel Scanning** - Fast repository detection using concurrent processing

## Prerequisites

- [.NET 10 SDK or later](https://dotnet.microsoft.com/download)
- [lazygit](https://github.com/jesseduffield/lazygit) - Git terminal UI

## Installation

### 1. Install Dependencies

**lazygit:**
```powershell
# Windows (winget)
winget install jesseduffield.lazygit

# Windows (Scoop)
scoop install lazygit

# Windows (Chocolatey)
choco install lazygit

# macOS
brew install lazygit

# Linux
# See https://github.com/jesseduffield/lazygit#installation
```

### 2. Configure Environment Variable

Set the `GIT_REPOS_PATH` environment variable to point to your Git repositories root directory:

**Windows (PowerShell):**
```powershell
[System.Environment]::SetEnvironmentVariable('GIT_REPOS_PATH', 'C:\Dev\Projects', 'User')
```

**macOS/Linux:**
```bash
export GIT_REPOS_PATH="$HOME/projects"
# Add to ~/.bashrc or ~/.zshrc for persistence
```

### 3. Run the Script

```powershell
# Run directly with .NET 10+
dotnet run repos.cs

# Or with VS Code C# extension
# Open repos.cs and press F5
```

## Usage

### Main Menu

When you launch the application, you'll see three options:

1. **Choose a repository** - Browse and open a repository
2. **Manage favorites** - Manage your favorite repositories
3. **Settings** - Configure application settings

### Repository List

The repository list displays the following information for each repo:

```
⭐  my-project              2h ago          ✓          main
    another-repo           1d ago          ↑3         feature/new-feature
    old-project            11/15/2023      ↓2         develop
```

- **⭐** - Favorite indicator
- **Time** - Last activity (seconds/minutes/hours/days ago, or date)
- **Remote Status**:
  - `✓` - Up to date with remote
  - `↑N` - N commits ahead of remote
  - `↓N` - N commits behind remote
  - `↑N↓M` - Diverged from remote
- **Branch Name** - Color-coded by type

### Branch Color Scheme

- **Blue** - `main` or `master`
- **Light Sea Green** - `develop`
- **Yellow** - `feature/*` or `feat/*`
- **Red** - `bugfix/*` or `fix/*`
- **Magenta** - `hotfix/*`
- **Cyan** - `release/*`
- **White** - Other branches

### Managing Favorites

1. Select **Manage favorites** from the main menu
2. Choose a repository from the list
3. The repository will be toggled as a favorite (⭐)
4. Favorites appear at the top of the repository list
5. Press `Esc` or select **← Back** to return to the main menu

### Settings

- **Skip main menu on startup** - Go directly to repository selection, bypassing the main menu

## Project Structure

The script scans two levels deep in your `GIT_REPOS_PATH`:

```
GIT_REPOS_PATH/
├── repo1/              # Level 1: Direct repositories
│   └── .git/
├── repo2/
│   └── .git/
└── category/           # Level 2: Grouped repositories
    ├── project-a/
    │   └── .git/
    └── project-b/
        └── .git/
```

## Data Storage

Favorites and preferences are stored in:
- **Windows**: `%APPDATA%\GitRepos\favorites.json`
- **macOS**: `~/Library/Application Support/GitRepos/favorites.json`
- **Linux**: `~/.config/GitRepos/favorites.json`

### Preferences File Format

```json
{
  "Version": 1,
  "Favorites": [
    {
      "Path": "C:\\Dev\\Projects\\my-project",
      "AddedAt": "2025-11-28T10:30:00",
      "Alias": null,
      "Category": null,
      "Priority": null
    }
  ],
  "SkipMainMenu": false,
  "LastModified": "2025-11-28T10:30:00",
  "Settings": {}
}
```

## Keyboard Shortcuts

- **↑/↓** - Navigate repositories
- **Enter** - Select repository and launch lazygit
- **Ctrl+C** - Exit application
- **Type to search** - Filter repositories by name

## Dependencies

The script automatically downloads the following NuGet packages:

- [Spectre.Console](https://spectreconsole.net/) (v0.54.0) - Terminal UI framework
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) (v0.31.0) - Git operations library

## Troubleshooting

### "The environment variable GIT_REPOS_PATH is not defined"

Set the `GIT_REPOS_PATH` environment variable as described in the installation section.

### "The directory '...' does not exist"

Ensure the path in `GIT_REPOS_PATH` exists and is accessible.

### "No Git repository found"

Verify that your repositories contain a `.git` directory and are located within `GIT_REPOS_PATH` or one level deeper.

### lazygit not found

Install lazygit and ensure it's available in your system PATH.

## Contributing

Contributions are welcome! Feel free to submit issues or pull requests.

## License

This project is provided as-is for personal and commercial use.

## Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/)
- Git operations powered by [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)
- Integrates with [lazygit](https://github.com/jesseduffield/lazygit)
