# DevTools

[![NuGet](https://img.shields.io/nuget/v/lucaslgt.DevTools?logo=nuget)](https://www.nuget.org/packages/lucaslgt.DevTools)
[![Downloads](https://img.shields.io/nuget/dt/lucaslgt.DevTools)](https://www.nuget.org/packages/lucaslgt.DevTools)
[![CI](https://github.com/lucaslenglet/DevTools/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/lucaslenglet/DevTools/actions/workflows/ci.yml)
[![License: MIT-0](https://img.shields.io/github/license/lucaslenglet/DevTools)](https://github.com/lucaslenglet/DevTools/blob/main/LICENSE)

**DevTools** is a terminal-based tool for quickly browsing, launching, and managing all your Git repositories from a single, interactive interface.

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

## Prerequisites

- [.NET 10 SDK or later](https://dotnet.microsoft.com/download)

## Installation

1. Install dotnet tool: `dotnet tool install --global lucaslgt.DevTools`
2. (Recommended for correct Unicode/emoji display in Windows PowerShell) Add the following line to your PowerShell $PROFILE:

  ```powershell
  [console]::InputEncoding = [console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
  ```
  This ensures proper display of Unicode and emoji characters in the terminal UI.  
3. Run the command: `devtools`. On first run, add the folders to scan (see [Configuration](#configuration)).

## Usage

1. Browse repositories: The main menu lists all detected repositories, sorted by favorites and recent activity.
2. Search/filter: Start typing to filter the list by name.
3. Select a repository: Use arrow keys and press Enter to open in lazygit. Press **Ctrl+Enter** or **Shift+Enter** to open a menu and select a program (e.g., terminal, explorer, Copilot CLI) to use on the repository.
4. Manage favorites: Press `F` to toggle favorite status for any repository.
5. Rename: Press `R` to give a repository a display name.
6. Scan paths: Press `F2` to add (`A`) or remove (`D`) the folders scanned for repositories.

### Keyboard Shortcuts

- **↑/↓**: Navigate repositories
- **Enter**: Open selected repository in lazygit
- **Ctrl+Enter** or **Shift+Enter**: Open the program selection menu for the selected repository
- **F**: Toggle favorite
- **R**: Rename (display name only, the folder is untouched)
- **F2**: Configure scan paths (**A** to add, **D** to remove)
- **Esc**: Go back
- **Q** or **Ctrl+C**: Exit

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

## Configuration

Everything lives in a single `config.yml`, under the local application data folder
(`%LOCALAPPDATA%\DevTools\config.yml` on Windows, `~/.local/share/DevTools/config.yml` on Linux).
The exact path is shown at the top of the main screen. It holds the scan paths, favorites, display
names, the command run on **Enter** (`DefaultCommand`, lazygit by default) and the programs offered
in the selection menu (`CustomCommands`). In a command, `{0}` is replaced by the repository path.

No changes are made to your repositories.

## Troubleshooting

- **Config version mismatch at startup**: the `config.yml` was written by another version of the tool; move it aside and let the tool create a new one.
- **lazygit not found**: Install lazygit and ensure it's in your PATH.
- **No repositories found**: Make sure your repos have a `.git` folder and are within one of the scan paths (**F2**).

## Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/)
- Git operations powered by [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)

## License

[MIT-0](https://github.com/lucaslenglet/DevTools/blob/main/LICENSE): use it however you like, no attribution required.

This project does not accept contributions; see [CONTRIBUTING.md](https://github.com/lucaslenglet/DevTools/blob/main/CONTRIBUTING.md).
Changes are listed in [CHANGELOG.md](https://github.com/lucaslenglet/DevTools/blob/main/CHANGELOG.md).
