# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

**DevTools** is a terminal-based Git repository browser and launcher (command: `devtools`), published as a .NET global tool (`lucaslgt.DevTools`). It requires the `GIT_REPOS_PATH` environment variable pointing to the root of your Git repositories.

## Commands

```bash
# Run the app (from repo root or src/DevTools/)
dotnet run --project src/DevTools

# Build the solution
dotnet build DevTools.slnx

# Restore dependencies
dotnet restore DevTools.slnx

# Pack the NuGet tool (CI uses Cake for this)
dotnet pack src/DevTools/DevTools.csproj -c Release
```

No test project exists — testing is done manually by running the app interactively.

## Architecture

The solution has two projects:

- **`src/DevTools`** — Main application. Entry point is `Program.cs`, which initializes DI and starts the `RepositoriesScreen`.
- **`src/DevTools.Components`** — Reusable UI library built on Spectre.Console. Uses `IgnoresAccessChecksToGenerator` to access internal Spectre.Console members. Do not add app-specific logic here.

### Screen system (`DevTools.Components`)

`Screen` (abstract base) drives the main interaction loop:
- `OnInit`: Build the screen by calling `AddElement()` with `IRenderable` or `IScreenComponent` items.
- `OnExit`: React to the result after the loop exits.
- `ShowAsync` runs a render/input loop until a component returns `ScreenInputResult.Exit`.
- `ShowRootAsync` loops indefinitely (used for the root screen only).

`MenuPrompt<T>` implements `IScreenComponent` — it is the primary interactive widget. Key features:
- `BindKey(ConsoleKey, handler)` — custom key handling returning `ScreenInputResult` (None / Refresh / Exit).
- `UseChoiceProvider(Func<IEnumerable<T>>)` — lazily loaded items (called once on first render).
- `AddChoices(IEnumerable<T>)` — statically provided items.
- `UseConverter(Func<T, string>)` — Spectre markup string for display.
- `SubmitContext` — set on exit; provides the selected item and the triggering key.
- Registered key bindings take priority over search input.

To add a new screen: create a class inheriting `Screen`, inject dependencies via constructor, override `OnInit`/`OnExit`, and register it as `Transient` in `Program.cs`.

### Configuration & persistence

`ConfigurationManager` loads/saves `config.yml` from the platform app data folder (`%LOCALAPPDATA%/DevTools/config.yml` on Windows). The `Config` model holds:
- `Favorites` — set of absolute repo paths.
- `DefaultCommand` — the command run on plain Enter (defaults to lazygit).
- `CustomCommands` — list of `ConfigCommand` entries shown in `RepositoryActionsScreen`.

`ConfigCommand.WorkingDirectory` and `Arguments` support `{0}` as a placeholder for the selected repo's full path, formatted via `StringHelper.FormatIfNotNull`.

`Config.CurrentVersion` is a static integer. If the loaded config's `Version` doesn't match, the app exits with an error — bump `CurrentVersion` whenever the config schema changes.

### Repository scanning

`RepositoryScanner` scans `GIT_REPOS_PATH` up to two levels deep in parallel, using LibGit2Sharp to read branch, tracking status, and last activity time (derived from `.git/` file timestamps). Repos at depth 2 display their parent folder name as a prefix.

### Branch colors & display

Branch color logic lives in `RepoDisplayFormatter.GetBranchColor` and maps branch prefixes to Spectre markup colors. Remote status indicators are built in `GetRemoteStatus`. To change display formatting, edit `RepoDisplayFormatter.cs`.

`Styles.cs` and `Hints.cs` hold shared style constants and hint text used across screens.

### Extensions

`ConsoleExtensions.cs` uses C# 14 extension members syntax to add `Execute`, `ClearAndExit`, and `ClearAndDisplayHint` directly onto `IAnsiConsole`.

## Versioning & CI

Versioning uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`version.json`). Builds are driven by a Cake script (`cake.cs`):
- `Build` target — used in the PR build workflow.
- `PackAndPush` target — used in the manual publish workflow; requires `NUGET_API_KEY` and `NUGET_SOURCE_URL` secrets.
