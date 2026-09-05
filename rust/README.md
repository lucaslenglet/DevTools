# devtools (Rust)

Rust/ratatui port of the .NET `DevTools` TUI. Same behaviour and same keys as the C#
version, and on Windows the **same config file**, so an existing setup keeps working.

Runs on Windows, Linux and macOS: no platform APIs are used directly, and the parts that
differ per platform (config location, default commands) are selected at build time.

## Config location

| Platform | Path |
|----------|------|
| Windows | `%LOCALAPPDATA%\DevTools\config.yml` (same as the .NET version) |
| Linux / macOS | `$XDG_CONFIG_HOME/devtools/config.yml`, i.e. `~/.config/devtools/config.yml` |

Scan paths may start with `~`, which expands to the home directory.

The default commands written to a fresh config follow the platform too: `pwsh` and
`explorer.exe` on Windows, `code`, `$SHELL` and `xdg-open` elsewhere. `lazygit` and the
CLI agents are the same everywhere.

## Build & run

```bash
cargo run            # from rust/
cargo test
cargo build --release
```

## Keys

| Screen | Keys |
|--------|------|
| Repositories | `Q` quit, `F2` paths, `F` favorite, `R` rename, `Enter` default command, `TAB` (or `Ctrl/Shift+Enter`, `Ctrl+J`) command list, `?` search, arrows / `j` `k` / Home / End / PageUp / PageDown |
| Commands | `Enter` run, `ESC` back, `Q` quit |
| Paths | `A` add, `D` remove, `ESC` back, `Q` quit |

## Layout

| File | Role |
|------|------|
| `config.rs` | `config.yml` model, load/save, `{0}` path placeholder |
| `scan.rs` | Repository discovery (2 levels deep, rayon) and git info via `gix` |
| `format.rs` | Repository row rendering: name, age, ahead/behind, branch colors |
| `menu.rs` | List cursor + incremental search shared by every screen |
| `text_input.rs` | Single-line prompt (add path, rename) |
| `screens.rs` | The three screens and their key handling |
| `tui.rs` | Terminal setup, key reading, running child processes |
| `theme.rs` | Spectre color-name compatibility and shared styles |

`Ctrl+Enter` and `Shift+Enter` need the terminal keyboard enhancement protocol (kitty,
foot, WezTerm, Ghostty). It is requested at startup when the terminal advertises support.
Windows Terminal, including WSL sessions, does not report those chords, so **`TAB` is the
portable binding** for the command list; `Ctrl+J` / `Ctrl+M` work too where the terminal
sends them.

## Scanning cost

Reading git metadata dominates the runtime, and it is far slower over WSL mounts (`/mnt/c`)
or network shares. The app therefore scans only when the result can have changed: at
startup, after a command ran, and after the scan directories were edited. Toggling a
favorite re-sorts the list in place, and renaming only relabels a row. Rendered rows are
cached between keystrokes and rebuilt when the displayed ages go stale.

## Windows toolchain note

This crate is built with the `x86_64-pc-windows-gnu` toolchain (mingw-w64 from
WinLibs) because no MSVC C++ build tools are installed on this machine. The
toolchain choice is stored in rustup's local override for this directory:

```bash
rustup override set stable-x86_64-pc-windows-gnu
```

Drop the override once MSVC build tools are available.

## Cross-compiling check

The Linux build is verified from Windows with:

```bash
rustup target add x86_64-unknown-linux-gnu
cargo check --target x86_64-unknown-linux-gnu
cargo clippy --target x86_64-unknown-linux-gnu --all-targets
```

Producing a Linux *binary* this way additionally needs a Linux linker, so build it on
Linux (or in WSL/a container) with a plain `cargo build --release`.
