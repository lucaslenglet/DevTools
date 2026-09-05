use crate::config::{format_if_some, AppContext, Config, ConfigCommand};
use crate::format::{self, Segment};
use crate::menu::MenuState;
use crate::scan::{self, RepoInfo};
use crate::text_input;
use crate::theme;
use crate::tui::{self, Tui};
use anyhow::Result;
use chrono::{DateTime, Local};
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use ratatui::layout::{Constraint, Layout, Rect};
use ratatui::style::Style;
use ratatui::text::{Line, Span};
use ratatui::widgets::Paragraph;
use ratatui::Frame;

pub enum Flow {
    /// Keep the app running (the caller decides whether to stay on the screen).
    Continue,
    /// Quit the whole application.
    Quit,
}

const ADD_PATH_SENTINEL: &str = "+ Add directory";

/// Repository browser — the root screen.
pub fn repositories(tui: &mut Tui, ctx: &mut AppContext) -> Result<Flow> {
    let mut menu = MenuState::new(0, true);
    let mut repos = rescan(tui, ctx, &mut menu)?;
    let mut view = RepoView::build(&repos, &ctx.config);

    loop {
        if view.is_stale() {
            view = RepoView::build(&repos, &ctx.config);
        }

        let hints = Line::from(join_hints(&[
            "Press Q to exit",
            "R to rename",
            "TAB for commands",
            "F2 configure paths",
            &format!("Config path ({})", ctx.config_file_path.display()),
        ]));
        let title = Line::from(vec![
            Span::raw("Select a "),
            Span::styled("repository", Style::default().fg(theme::parse_color("green"))),
            Span::raw(" :"),
        ]);

        let spans = view.spans(&menu.search);

        let mut page_size = 0usize;
        tui.terminal.draw(|frame| {
            page_size = draw_list_screen(frame, hints.clone(), title.clone(), spans, &mut menu);
        })?;

        let Some(key) = tui::next_key()? else {
            continue;
        };

        if handle_search_key(&key, &mut menu, &view.keys) {
            continue;
        }

        // Many terminals (Windows Terminal on WSL among them) cannot report Ctrl/Shift+Enter
        // as anything but a plain Enter, and some send Ctrl+J or Ctrl+M instead. TAB is the
        // binding that works everywhere.
        let control = key.modifiers.contains(KeyModifiers::CONTROL);
        let opens_command_list = key.code == KeyCode::Tab
            || (key.code == KeyCode::Enter
                && key
                    .modifiers
                    .intersects(KeyModifiers::CONTROL | KeyModifiers::SHIFT))
            || (control && matches!(key.code, KeyCode::Char('j') | KeyCode::Char('m')));
        let runs_a_command = opens_command_list || key.code == KeyCode::Enter;

        match key.code {
            _ if runs_a_command => {
                let Some(repo) = repos.get(menu.index).cloned() else {
                    continue;
                };

                let command = if opens_command_list {
                    match commands(tui, ctx, &repo)? {
                        (Flow::Quit, _) => return Ok(Flow::Quit),
                        (Flow::Continue, command) => command,
                    }
                } else {
                    Some(ctx.config.default_command.clone())
                };

                if let Some(command) = command {
                    let path = repo.path.to_string_lossy().into_owned();
                    tui.run_command(
                        &command.process_name,
                        format_if_some(command.working_directory.as_ref(), &path),
                        format_if_some(command.arguments.as_ref(), &path),
                    )?;
                    // The command may well have changed the repository, so refresh here.
                    repos = rescan(tui, ctx, &mut menu)?;
                    select_path(&repos, &mut menu, &path);
                    view = RepoView::build(&repos, &ctx.config);
                }
            }
            KeyCode::Char('q') | KeyCode::Char('Q') => return Ok(Flow::Quit),
            KeyCode::F(2) => {
                let paths_before = ctx.config.repo_paths.clone();

                if let Flow::Quit = repo_paths(tui, ctx)? {
                    return Ok(Flow::Quit);
                }

                // Scanning is by far the slowest thing the app does, so only redo it when
                // the directories actually changed.
                if ctx.config.repo_paths != paths_before {
                    repos = rescan(tui, ctx, &mut menu)?;
                    view = RepoView::build(&repos, &ctx.config);
                }
            }
            KeyCode::Char('f') | KeyCode::Char('F') => {
                if let Some(repo) = repos.get(menu.index) {
                    let path = repo.path.to_string_lossy().into_owned();
                    ctx.config.toggle_favorite(&path);
                    ctx.save()?;
                    // Favorites only affect the order, so re-sort instead of rescanning.
                    sort_repos(&mut repos, &ctx.config);
                    select_path(&repos, &mut menu, &path);
                    view = RepoView::build(&repos, &ctx.config);
                }
            }
            KeyCode::Char('r') | KeyCode::Char('R') => {
                if let Some(repo) = repos.get(menu.index).cloned() {
                    // A display name changes the label only — no rescan, no reordering.
                    rename_repo(tui, ctx, &repo)?;
                    view = RepoView::build(&repos, &ctx.config);
                }
            }
            _ => handle_navigation_key(&key, &mut menu, page_size),
        }
    }
}

/// Manages the directories scanned for repositories.
pub fn repo_paths(tui: &mut Tui, ctx: &mut AppContext) -> Result<Flow> {
    let mut menu = MenuState::new(ctx.config.repo_paths.len() + 1, true);

    loop {
        let mut entries: Vec<String> = ctx.config.repo_paths.clone();
        entries.push(ADD_PATH_SENTINEL.to_string());
        menu.set_len(entries.len());

        let last = entries.len() - 1;
        let spans: Vec<Vec<Span<'static>>> = entries
            .iter()
            .enumerate()
            .map(|(index, entry)| {
                if index == last {
                    vec![
                        Span::styled("+ Add", Style::default().fg(theme::parse_color("green"))),
                        Span::raw(" directory"),
                    ]
                } else {
                    vec![Span::raw(entry.clone())]
                }
            })
            .collect();

        let hints = Line::from(join_hints(&["A to add", "D to remove", "Press ESC to go back"]));
        let title = Line::from(vec![
            Span::styled("Git", Style::default().fg(theme::parse_color("green"))),
            Span::raw(" directories :"),
        ]);

        let mut page_size = 0usize;
        tui.terminal.draw(|frame| {
            page_size = draw_list_screen(frame, hints.clone(), title.clone(), spans, &mut menu);
        })?;

        let Some(key) = tui::next_key()? else {
            continue;
        };

        match key.code {
            KeyCode::Char('q') | KeyCode::Char('Q') => return Ok(Flow::Quit),
            KeyCode::Esc => {
                // The app is unusable without at least one path, so keep the user here.
                if ctx.config.repo_paths.is_empty() {
                    continue;
                }
                return Ok(Flow::Continue);
            }
            KeyCode::Char('d') | KeyCode::Char('D') => {
                if menu.index < ctx.config.repo_paths.len() {
                    ctx.config.repo_paths.remove(menu.index);
                    ctx.save()?;
                }
            }
            KeyCode::Char('a') | KeyCode::Char('A') => add_path(tui, ctx)?,
            KeyCode::Enter if menu.index == last => add_path(tui, ctx)?,
            _ => handle_navigation_key(&key, &mut menu, page_size),
        }
    }
}

/// Command picker for a repository. Returns the chosen command, if any.
fn commands(
    tui: &mut Tui,
    ctx: &AppContext,
    repo: &RepoInfo,
) -> Result<(Flow, Option<ConfigCommand>)> {
    let mut menu = MenuState::new(ctx.config.custom_commands.len(), true);

    loop {
        let spans: Vec<Vec<Span<'static>>> = ctx
            .config
            .custom_commands
            .iter()
            .map(|command| {
                let color = command
                    .color
                    .as_deref()
                    .map(theme::parse_color)
                    .unwrap_or(ratatui::style::Color::White);
                vec![Span::styled(
                    format!("{:<20}", command.display_name()),
                    Style::default().fg(color),
                )]
            })
            .collect();

        let hints = Line::from(join_hints(&["Press Q to exit", "Press ESC to go back"]));
        let title = Line::from(vec![
            Span::raw("Select a "),
            Span::styled("command", Style::default().fg(theme::parse_color("green"))),
            Span::styled(
                format!(" ({})", repo.path.display()),
                theme::dim(),
            ),
            Span::raw(" :"),
        ]);

        let mut page_size = 0usize;
        tui.terminal.draw(|frame| {
            page_size = draw_list_screen(frame, hints.clone(), title.clone(), spans, &mut menu);
        })?;

        let Some(key) = tui::next_key()? else {
            continue;
        };

        match key.code {
            KeyCode::Char('q') | KeyCode::Char('Q') => return Ok((Flow::Quit, None)),
            KeyCode::Esc => return Ok((Flow::Continue, None)),
            KeyCode::Enter => {
                return Ok((
                    Flow::Continue,
                    ctx.config.custom_commands.get(menu.index).cloned(),
                ))
            }
            _ => handle_navigation_key(&key, &mut menu, page_size),
        }
    }
}

fn add_path(tui: &mut Tui, ctx: &mut AppContext) -> Result<()> {
    let prompt = vec![
        Span::raw("Enter "),
        Span::styled(
            "directory path",
            Style::default().fg(theme::parse_color("green")),
        ),
        Span::raw(" :"),
    ];

    let Some(path) = text_input::prompt(tui, prompt, "")? else {
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

fn rename_repo(tui: &mut Tui, ctx: &mut AppContext, repo: &RepoInfo) -> Result<()> {
    let path = repo.path.to_string_lossy().into_owned();
    let current = ctx.config.display_name(&path).map(str::to_string);

    let prompt = vec![
        Span::raw("Enter "),
        Span::styled(
            "display name",
            Style::default().fg(theme::parse_color("green")),
        ),
        Span::raw(" for "),
        Span::styled(
            repo.name.clone(),
            Style::default().fg(theme::parse_color("blue")),
        ),
        Span::raw(" :"),
    ];

    let default_value = current.clone().unwrap_or_else(|| repo.name.clone());
    let Some(value) = text_input::prompt(tui, prompt, &default_value)? else {
        return Ok(());
    };

    let unchanged = match &current {
        None => value.trim().is_empty(),
        Some(current) => current == &value,
    };
    if unchanged {
        return Ok(());
    }

    ctx.config.set_display_name(&path, Some(value.as_str()));
    ctx.save()?;
    Ok(())
}

/// The rendered repository rows. Formatting every row costs a timezone lookup per row, so
/// the result is kept until the data changes or the displayed ages go stale.
struct RepoView {
    rows: Vec<Vec<Segment>>,
    keys: Vec<String>,
    built_at: DateTime<Local>,
}

impl RepoView {
    fn build(repos: &[RepoInfo], config: &Config) -> Self {
        let built_at = Local::now();
        let rows: Vec<Vec<Segment>> = repos
            .iter()
            .map(|repo| format::repo_segments(repo, built_at, config))
            .collect();
        let keys = rows.iter().map(|row| format::plain_text(row)).collect();

        Self {
            rows,
            keys,
            built_at,
        }
    }

    /// Ages are shown to the minute, so anything fresher than that renders identically.
    fn is_stale(&self) -> bool {
        Local::now()
            .signed_duration_since(self.built_at)
            .num_minutes()
            >= 1
    }

    fn spans(&self, search: &str) -> Vec<Vec<Span<'static>>> {
        self.rows
            .iter()
            .map(|row| format::highlight(row, search, theme::SEARCH_HIGHLIGHT))
            .collect()
    }
}

/// Scans the configured directories, showing a notice first: on a network or WSL-mounted
/// filesystem this takes long enough that a frozen screen would look like a hang.
fn rescan(tui: &mut Tui, ctx: &AppContext, menu: &mut MenuState) -> Result<Vec<RepoInfo>> {
    tui.terminal.draw(|frame| {
        let [_, _, notice] = Layout::vertical([
            Constraint::Length(1),
            Constraint::Length(1),
            Constraint::Min(1),
        ])
        .areas(frame.area());

        frame.render_widget(
            Paragraph::new(Line::from(Span::styled(
                "Scanning repositories\u{2026}",
                theme::dim(),
            ))),
            notice,
        );
    })?;

    let mut repos = scan::scan(&ctx.config);
    sort_repos(&mut repos, &ctx.config);
    menu.set_len(repos.len());
    Ok(repos)
}

/// Favorites first, then most recent activity.
fn sort_repos(repos: &mut [RepoInfo], config: &Config) {
    repos.sort_by(|a, b| {
        let a_favorite = config.is_favorite(&a.path.to_string_lossy());
        let b_favorite = config.is_favorite(&b.path.to_string_lossy());
        b_favorite
            .cmp(&a_favorite)
            .then(b.last_activity.cmp(&a.last_activity))
    });
}

/// Keeps the cursor on the same repository after the list is reordered or rebuilt.
fn select_path(repos: &[RepoInfo], menu: &mut MenuState, path: &str) {
    if let Some(index) = repos.iter().position(|repo| repo.path.as_os_str() == path) {
        menu.index = index;
    }
}

/// Draws hints, title, list and search footer. Returns the list height (used as page size).
fn draw_list_screen(
    frame: &mut Frame,
    hints: Line<'static>,
    title: Line<'static>,
    rows: Vec<Vec<Span<'static>>>,
    menu: &mut MenuState,
) -> usize {
    let [hints_area, _, title_area, _, list_area, footer_area] = Layout::vertical([
        Constraint::Length(1),
        Constraint::Length(1),
        Constraint::Length(1),
        Constraint::Length(1),
        Constraint::Min(1),
        Constraint::Length(2),
    ])
    .areas(frame.area());

    frame.render_widget(Paragraph::new(hints), hints_area);
    frame.render_widget(Paragraph::new(title), title_area);
    menu.render(frame, list_area, rows);
    draw_footer(frame, footer_area, menu);

    list_area.height as usize
}

fn draw_footer(frame: &mut Frame, area: Rect, menu: &MenuState) {
    let line = if menu.searching {
        Line::from(vec![
            Span::styled("Searching", theme::SEARCH_HIGHLIGHT),
            Span::styled(" (Press ESC to cancel)", theme::dim()),
            Span::raw(format!(" : {}", menu.search)),
        ])
    } else {
        Line::from(Span::styled("(Press ? to search)", theme::dim()))
    };

    let [_, footer] = Layout::vertical([Constraint::Length(1), Constraint::Length(1)]).areas(area);
    frame.render_widget(Paragraph::new(line), footer);
}

/// Returns true when the key was consumed by search handling.
fn handle_search_key(key: &KeyEvent, menu: &mut MenuState, keys: &[String]) -> bool {
    if !menu.searching {
        if let KeyCode::Char('?') = key.code {
            menu.start_search();
            return true;
        }
        return false;
    }

    match key.code {
        KeyCode::Esc => {
            menu.cancel_search();
            true
        }
        KeyCode::Backspace => {
            menu.backspace_search(key.modifiers.contains(KeyModifiers::CONTROL), keys);
            true
        }
        KeyCode::Char(c) if !key.modifiers.contains(KeyModifiers::CONTROL) => {
            menu.push_search(c, keys);
            true
        }
        _ => false,
    }
}

fn handle_navigation_key(key: &KeyEvent, menu: &mut MenuState, page_size: usize) {
    match key.code {
        KeyCode::Up | KeyCode::Char('k') => menu.move_relative(-1),
        KeyCode::Down | KeyCode::Char('j') => menu.move_relative(1),
        KeyCode::Home => menu.move_first(),
        KeyCode::End => menu.move_last(),
        KeyCode::PageUp => menu.move_page(-1, page_size),
        KeyCode::PageDown => menu.move_page(1, page_size),
        _ => {}
    }
}

fn join_hints(hints: &[&str]) -> Vec<Span<'static>> {
    let mut spans = Vec::new();
    for (index, hint) in hints.iter().enumerate() {
        if index > 0 {
            spans.push(Span::styled(" | ", theme::dim()));
        }
        spans.push(Span::styled(hint.to_string(), theme::dim()));
    }
    spans
}
