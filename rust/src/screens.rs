use crate::config::{expand_repo_path, AppContext, Config, ConfigCommand};
use crate::format::{self, Segment};
use crate::list::{title_line, Input, ListScreen};
use crate::menu::MenuState;
use crate::scan::{self, RepoInfo};
use crate::text_input;
use crate::theme;
use crate::tui::Tui;
use anyhow::Result;
use chrono::{DateTime, Local};
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use ratatui::layout::{Constraint, Layout};
use ratatui::text::{Line, Span};
use ratatui::widgets::Paragraph;

pub enum Flow {
    /// Keep the app running (the caller decides whether to stay on the screen).
    Continue,
    /// Quit the whole application.
    Quit,
}

// ---------------------------------------------------------------------------
// Repository browser — the root screen
// ---------------------------------------------------------------------------

/// What a key means on the repository screen.
enum Action {
    RunDefaultCommand,
    ChooseCommand,
    ToggleFavorite,
    Rename,
    EditPaths,
    Ignore,
}

fn classify(key: &KeyEvent) -> Action {
    let control = key.modifiers.contains(KeyModifiers::CONTROL);

    // Many terminals (Windows Terminal on WSL among them) cannot report Ctrl/Shift+Enter as
    // anything but a plain Enter, and some send Ctrl+J or Ctrl+M instead — hence TAB, the
    // binding that works everywhere.
    match key.code {
        KeyCode::Tab => Action::ChooseCommand,
        KeyCode::Char('j') | KeyCode::Char('m') if control => Action::ChooseCommand,
        KeyCode::Enter
            if key
                .modifiers
                .intersects(KeyModifiers::CONTROL | KeyModifiers::SHIFT) =>
        {
            Action::ChooseCommand
        }
        KeyCode::Enter => Action::RunDefaultCommand,
        KeyCode::Char('f') | KeyCode::Char('F') => Action::ToggleFavorite,
        KeyCode::Char('r') | KeyCode::Char('R') => Action::Rename,
        KeyCode::F(2) => Action::EditPaths,
        _ => Action::Ignore,
    }
}

pub fn repositories(tui: &mut Tui, ctx: &mut AppContext) -> Result<Flow> {
    let mut browser = Browser::new(tui, ctx)?;

    loop {
        browser.refresh_rows(&ctx.config);

        let config_hint = format!("Config path ({})", ctx.config_file_path.display());
        let screen = ListScreen {
            hints: &[
                "Press Q to exit",
                "R to rename",
                "TAB for commands",
                "F2 configure paths",
                &config_hint,
            ],
            title: title_line("Select a ", "repository", vec![Span::raw(" :")]),
            rows: &browser.view.lines,
            search_keys: Some(&browser.view.keys),
        };

        let key = match screen.show(tui, &mut browser.menu)? {
            Input::Quit => return Ok(Flow::Quit),
            Input::Handled => continue,
            Input::Other(key) => key,
        };

        match classify(&key) {
            Action::RunDefaultCommand => {
                let command = ctx.config.default_command.clone();
                if let Flow::Quit = browser.run(tui, ctx, command)? {
                    return Ok(Flow::Quit);
                }
            }
            Action::ChooseCommand => {
                let Some(repo) = browser.selected() else {
                    continue;
                };
                match commands(tui, ctx, repo)? {
                    Pick::Quit => return Ok(Flow::Quit),
                    Pick::Cancelled => {}
                    Pick::Chosen(command) => {
                        if let Flow::Quit = browser.run(tui, ctx, command)? {
                            return Ok(Flow::Quit);
                        }
                    }
                }
            }
            Action::ToggleFavorite => browser.toggle_favorite(ctx)?,
            Action::Rename => browser.rename(tui, ctx)?,
            Action::EditPaths => {
                let paths_before = ctx.config.repo_paths.clone();
                if let Flow::Quit = repo_paths(tui, ctx)? {
                    return Ok(Flow::Quit);
                }
                // Scanning is by far the slowest thing the app does, so only redo it when
                // the directories actually changed.
                if ctx.config.repo_paths != paths_before {
                    browser.rescan(tui, ctx)?;
                }
            }
            Action::Ignore => {}
        }
    }
}

/// The repository list, the cursor into it, and the rendered rows — kept together so that
/// every mutation refreshes exactly what it invalidated.
struct Browser {
    repos: Vec<RepoInfo>,
    menu: MenuState,
    view: RepoView,
}

impl Browser {
    fn new(tui: &mut Tui, ctx: &AppContext) -> Result<Self> {
        let mut browser = Self {
            repos: Vec::new(),
            menu: MenuState::new(0, true),
            view: RepoView::default(),
        };
        browser.rescan(tui, ctx)?;
        Ok(browser)
    }

    fn selected(&self) -> Option<&RepoInfo> {
        self.repos.get(self.menu.index)
    }

    /// Re-reads every repository from disk, keeping the cursor on the same one.
    fn rescan(&mut self, tui: &mut Tui, ctx: &AppContext) -> Result<()> {
        let selected = self.selected().map(|repo| repo.path.clone());

        // On a network or WSL-mounted filesystem this takes long enough that a frozen
        // screen would look like a hang.
        tui.terminal.draw(|frame| {
            let [_, _, notice] = Layout::vertical([
                Constraint::Length(1),
                Constraint::Length(1),
                Constraint::Min(1),
            ])
            .areas(frame.area());

            let text = Span::styled("Scanning repositories\u{2026}", theme::dim());
            frame.render_widget(Paragraph::new(Line::from(text)), notice);
        })?;

        self.repos = scan::scan(&ctx.config);
        self.reorder(&ctx.config, selected.as_deref());
        Ok(())
    }

    fn toggle_favorite(&mut self, ctx: &mut AppContext) -> Result<()> {
        let Some(path) = self.selected().map(|repo| repo.path.clone()) else {
            return Ok(());
        };

        ctx.config.toggle_favorite(&path);
        ctx.save()?;
        // Favorites only affect the order, so re-sort instead of rescanning.
        self.reorder(&ctx.config, Some(&path));
        Ok(())
    }

    fn rename(&mut self, tui: &mut Tui, ctx: &mut AppContext) -> Result<()> {
        let Some(repo) = self.selected() else {
            return Ok(());
        };

        let current = ctx.config.display_name(&repo.path);
        let title = title_line(
            "Enter ",
            "display name",
            vec![
                Span::raw(" for "),
                Span::styled(repo.name.clone(), theme::fg("blue")),
                Span::raw(" :"),
            ],
        );

        let default_value = current.unwrap_or(&repo.name).to_string();
        let Some(value) = text_input::prompt(tui, title, &default_value)? else {
            return Ok(());
        };

        let unchanged = current.map_or(value.trim().is_empty(), |current| current == value);
        if unchanged {
            return Ok(());
        }

        // A display name changes the label only — no rescan, no reordering.
        let path = repo.path.clone();
        ctx.config.set_display_name(&path, Some(&value));
        ctx.save()?;
        self.view = RepoView::build(&self.repos, &ctx.config);
        Ok(())
    }

    /// Runs a command on the selected repository, then refreshes: it may well have changed
    /// the repository it ran in.
    fn run(&mut self, tui: &mut Tui, ctx: &AppContext, command: ConfigCommand) -> Result<Flow> {
        let Some(repo) = self.selected() else {
            return Ok(Flow::Continue);
        };

        let path = repo.path.clone();
        tui.run_command(
            &command.process_name,
            expand_repo_path(command.working_directory.as_ref(), &path),
            expand_repo_path(command.arguments.as_ref(), &path),
        )?;
        self.rescan(tui, ctx)?;
        Ok(Flow::Continue)
    }

    /// Sorts favorites first, then by most recent activity, and restores the cursor.
    fn reorder(&mut self, config: &Config, selected: Option<&str>) {
        self.repos.sort_by(|a, b| {
            let favorite = config
                .is_favorite(&b.path)
                .cmp(&config.is_favorite(&a.path));
            favorite.then(b.last_activity.cmp(&a.last_activity))
        });

        self.menu.set_len(self.repos.len());
        if let Some(index) =
            selected.and_then(|path| self.repos.iter().position(|repo| repo.path == path))
        {
            self.menu.index = index;
        }

        self.view = RepoView::build(&self.repos, config);
    }

    /// Rebuilds the rows when the ages they show have gone stale.
    fn refresh_rows(&mut self, config: &Config) {
        if self.view.is_stale() {
            self.view = RepoView::build(&self.repos, config);
        }
        self.view.apply_search(&self.menu.search);
    }
}

/// The rendered repository rows. Building them costs a timezone lookup per row and search
/// highlighting reallocates each row, so both are cached until their input changes.
#[derive(Default)]
struct RepoView {
    segments: Vec<Vec<Segment>>,
    /// Plain text of each row, used to resolve searches.
    keys: Vec<String>,
    lines: Vec<Line<'static>>,
    search: String,
    built_at: Option<DateTime<Local>>,
}

impl RepoView {
    fn build(repos: &[RepoInfo], config: &Config) -> Self {
        let built_at = Local::now();
        let segments: Vec<Vec<Segment>> = repos
            .iter()
            .map(|repo| format::repo_segments(repo, built_at, config))
            .collect();

        let mut view = Self {
            keys: segments.iter().map(|row| format::plain_text(row)).collect(),
            segments,
            built_at: Some(built_at),
            ..Self::default()
        };
        view.render_lines();
        view
    }

    /// Ages are shown to the minute, so anything fresher renders identically.
    fn is_stale(&self) -> bool {
        self.built_at
            .is_none_or(|built_at| Local::now().signed_duration_since(built_at).num_minutes() >= 1)
    }

    fn apply_search(&mut self, search: &str) {
        if self.search != search {
            self.search = search.to_string();
            self.render_lines();
        }
    }

    fn render_lines(&mut self) {
        self.lines = self
            .segments
            .iter()
            .map(|row| format::highlight(row, &self.search, theme::SEARCH_HIGHLIGHT))
            .collect();
    }
}

// ---------------------------------------------------------------------------
// Command picker
// ---------------------------------------------------------------------------

enum Pick {
    Quit,
    Cancelled,
    Chosen(ConfigCommand),
}

fn commands(tui: &mut Tui, ctx: &AppContext, repo: &RepoInfo) -> Result<Pick> {
    let mut menu = MenuState::new(ctx.config.custom_commands.len(), true);

    let rows: Vec<Line<'static>> = ctx
        .config
        .custom_commands
        .iter()
        .map(|command| {
            let color = command.color.as_deref().unwrap_or("white");
            Line::from(Span::styled(
                format!("{:<20}", command.display_name()),
                theme::fg(color),
            ))
        })
        .collect();

    let title = title_line(
        "Select a ",
        "command",
        vec![
            Span::styled(format!(" ({})", repo.path), theme::dim()),
            Span::raw(" :"),
        ],
    );

    loop {
        let screen = ListScreen {
            hints: &["Press Q to exit", "Press ESC to go back"],
            title: title.clone(),
            rows: &rows,
            search_keys: None,
        };

        let key = match screen.show(tui, &mut menu)? {
            Input::Quit => return Ok(Pick::Quit),
            Input::Handled => continue,
            Input::Other(key) => key,
        };

        match key.code {
            KeyCode::Esc => return Ok(Pick::Cancelled),
            KeyCode::Enter => {
                return Ok(match ctx.config.custom_commands.get(menu.index) {
                    Some(command) => Pick::Chosen(command.clone()),
                    None => Pick::Cancelled,
                })
            }
            _ => {}
        }
    }
}

// ---------------------------------------------------------------------------
// Scan directories
// ---------------------------------------------------------------------------

/// Manages the directories scanned for repositories.
pub fn repo_paths(tui: &mut Tui, ctx: &mut AppContext) -> Result<Flow> {
    let mut menu = MenuState::new(0, true);

    loop {
        // The last row is a sentinel that adds a directory rather than naming one.
        let rows: Vec<Line<'static>> = ctx
            .config
            .repo_paths
            .iter()
            .map(|path| Line::from(Span::raw(path.clone())))
            .chain([Line::from(vec![
                Span::styled("+ Add", theme::fg("green")),
                Span::raw(" directory"),
            ])])
            .collect();
        let add_index = rows.len() - 1;
        menu.set_len(rows.len());

        let screen = ListScreen {
            hints: &["A to add", "D to remove", "Press ESC to go back"],
            title: title_line("", "Git", vec![Span::raw(" directories :")]),
            rows: &rows,
            search_keys: None,
        };

        let key = match screen.show(tui, &mut menu)? {
            Input::Quit => return Ok(Flow::Quit),
            Input::Handled => continue,
            Input::Other(key) => key,
        };

        match key.code {
            // The app is unusable without at least one path, so keep the user here.
            KeyCode::Esc if !ctx.config.repo_paths.is_empty() => return Ok(Flow::Continue),
            KeyCode::Char('d') | KeyCode::Char('D') if menu.index < add_index => {
                ctx.config.repo_paths.remove(menu.index);
                ctx.save()?;
            }
            KeyCode::Char('a') | KeyCode::Char('A') => add_path(tui, ctx)?,
            KeyCode::Enter if menu.index == add_index => add_path(tui, ctx)?,
            _ => {}
        }
    }
}

fn add_path(tui: &mut Tui, ctx: &mut AppContext) -> Result<()> {
    let title = title_line("Enter ", "directory path", vec![Span::raw(" :")]);
    let Some(path) = text_input::prompt(tui, title, "")? else {
        return Ok(());
    };

    let path = path.trim().to_string();
    if path.is_empty()
        || !crate::config::resolve_path(&path).is_dir()
        || ctx.config.repo_paths.contains(&path)
    {
        return Ok(());
    }

    ctx.config.repo_paths.push(path);
    ctx.save()?;
    Ok(())
}
