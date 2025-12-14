
# Git Repos Manager

**Git Repos Manager** is a terminal-based tool for quickly browsing, launching, and managing all your Git repositories from a single, interactive interface.

## Features

- **Automatic Repository Discovery**: Instantly scans and lists all Git repositories under your chosen directory (supports two levels deep).
- **Favorites System**: Mark frequently used repositories as favorites for quick access. Favorites always appear at the top of the list.
- **Rich Repository Information**: See branch name, last activity time, and remote tracking status for every repository at a glance.
- **Color-Coded Branches**: Branch names are color-coded by type (main, develop, feature, bugfix, hotfix, release, and more) for instant recognition.
- **Remote Status Indicators**: Visual symbols show if your branch is ahead, behind, or diverged from the remote.
- **Fast Search & Filtering**: Instantly filter repositories by typing part of their name.
- **Keyboard-Driven Navigation**: Navigate, select, and manage repositories entirely from the keyboard (arrow keys, Enter, Esc, etc.).
- **Integrated Tool Launching**: Open any repository directly in lazygit, your terminal, or file explorer with a single keystroke.
- **Parallel Scanning**: Repository discovery is fast, even with hundreds of repos, thanks to parallel processing.
- **Cross-Platform**: Works on Windows, macOS, and Linux.

## Getting Started


## Prerequisites

- [.NET 10 SDK or later](https://dotnet.microsoft.com/download)

## Installation

1. Set the `GIT_REPOS_PATH` environment variable to your repositories root folder
3. Run the app:
   - `dotnet run` from `src/DevTools/`
   - Or open in VS Code and press F5

## Usage

1. Browse repositories: The main menu lists all detected repositories, sorted by favorites and recent activity.
2. Search/filter: Start typing to filter the list by name.
3. Select a repository: Use arrow keys and press Enter to open in lazygit. Press **Ctrl+Enter** or **Shift+Enter** to open a menu and select a program (e.g., terminal, explorer, Copilot CLI) to use on the repository.
4. Manage favorites: Press `F` to toggle favorite status for any repository.
5. Settings: Optionally skip the main menu on startup.

### Keyboard Shortcuts

- **↑/↓**: Navigate repositories
- **Enter**: Open selected repository in lazygit
- **F**: Toggle favorite
- **Esc**: Go back
- **Q** or **Ctrl+C**: Exit
- **Ctrl+Enter** or **Shift+Enter**: Open the program selection menu for the selected repository

### Repository List Indicators

- **🔥** — Favorite
- **Time** — Last activity (e.g., 2h ago, 1d ago, or date)
- **Remote Status**:
  - `✓` — Up to date with remote
  - `↑N` — N commits ahead
  - `↓N` — N commits behind
  - `↑N↓M` — Diverged from remote
- **Branch Color**:
  - Blue — `main` or `master`
  - Light Sea Green — `develop`
  - Yellow — `feature/*` or `feat/*`
  - Red — `bugfix/*` or `fix/*`
  - Magenta — `hotfix/*`
  - Cyan — `release/*`
  - White — Other branches

## Data Storage

Favorites and settings are stored in platform-specific app data folders as JSON. No changes are made to your repositories.

## Troubleshooting

- **Missing GIT_REPOS_PATH**: Set the environment variable to your repo root.
- **lazygit not found**: Install lazygit and ensure it's in your PATH.
- **No repositories found**: Make sure your repos have a `.git` folder and are within the specified path.

## Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/)
- Git operations powered by [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)
