# devtools (Rust)

Rust/ratatui port of the .NET `DevTools` TUI. Same behaviour, same keys, and the
**same config file** as the C# version: `%LOCALAPPDATA%/DevTools/config.yml`.

## Build & run

```bash
cargo run            # from rust/
cargo test
cargo build --release
```

## Keys

| Screen | Keys |
|--------|------|
| Repositories | `Q` quit, `F2` paths, `F` favorite, `R` rename, `Enter` default command, `Ctrl/Shift+Enter` command list, `?` search, arrows / `j` `k` / Home / End / PageUp / PageDown |
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

## Windows toolchain note

This crate is built with the `x86_64-pc-windows-gnu` toolchain (mingw-w64 from
WinLibs) because no MSVC C++ build tools are installed on this machine. The
toolchain choice is stored in rustup's local override for this directory:

```bash
rustup override set stable-x86_64-pc-windows-gnu
```

Drop the override once MSVC build tools are available.
