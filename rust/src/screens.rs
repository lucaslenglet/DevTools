use crate::config::{format_if_some, AppContext, ConfigCommand};
use crate::format::{self, Segment};
use crate::menu::MenuState;
use crate::scan::{self, RepoInfo};
use crate::text_input;
use crate::theme;
use crate::tui::{self, Tui};
use anyhow::Result;
use chrono::Local;
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
    let mut repos = fetch_repos(ctx);
    let mut menu = MenuState::new(repos.len(), true);

    loop {
        let now = Local::now();
        let rows: Vec<Vec<Segment>> = repos
            .iter()
            .map(|repo| format::repo_segments(repo, now, &ctx.config))
            .collect();
        let keys: Vec<String> = rows.iter().map(|r| format::plain_text(r)).collect();

        let hints = Line::from(join_hints(&[
            "Press Q to exit",
            "R to rename",
            "F2 configure paths",
            &format!("Config path ({})", ctx.config_file_path.display()),
        ]));
        let title = Line::from(vec![
            Span::raw("Select a "),
            Span::styled("repository", Style::default().fg(theme::parse_color("green"))),
            Span::raw(" :"),
        ]);

        let spans: Vec<Vec<Span<'static>>> = rows
            .into_iter()
            .map(|segments| format::highlight(segments, &menu.search, theme::SEARCH_HIGHLIGHT))
            .collect();

        let mut page_size = 0usize;
        tui.terminal.draw(|frame| {
            page_size = draw_list_screen(frame, hints.clone(), title.clone(), spans, &mut menu);
        })?;

        let Some(key) = tui::next_key()? else {
            continue;
        };

        if handle_search_key(&key, &mut menu, &keys) {
            continue;
        }

        match key.code {
            KeyCode::Char('q') | KeyCode::Char('Q') => return Ok(Flow::Quit),
            KeyCode::F(2) => {
                if let Flow::Quit = repo_paths(tui, ctx)? {
                    return Ok(Flow::Quit);
                }
                repos = fetch_repos(ctx);
                menu.set_len(repos.len());
            }
            KeyCode::Char('f') | KeyCode::Char('F') => {
                if let Some(repo) = repos.get(menu.index) {
                    let path = repo.path.to_string_lossy().into_owned();
                    ctx.config.toggle_favorite(&path);
                    ctx.save()?;
                    repos = fetch_repos(ctx);
                    menu.set_len(repos.len());
                }
            }
            KeyCode::Char('r') | KeyCode::Char('R') => {
                if let Some(repo) = repos.get(menu.index) {
                    rename_repo(tui, ctx, repo)?;
                    repos = fetch_repos(ctx);
                    menu.set_len(repos.len());
                }
            }
            KeyCode::Enter => {
                let Some(repo) = repos.get(menu.index).cloned() else {
                    continue;
                };

                let command = if key
                    .modifiers
                    .intersects(KeyModifiers::CONTROL | KeyModifiers::SHIFT)
                {
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
                    repos = fetch_repos(ctx);
                    menu.set_len(repos.len());
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
        || !std::path::Path::new(&path).is_dir()
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

fn fetch_repos(ctx: &AppContext) -> Vec<RepoInfo> {
    let mut repos = scan::scan(&ctx.config);
    repos.sort_by(|a, b| {
        let a_favorite = ctx.config.is_favorite(&a.path.to_string_lossy());
        let b_favorite = ctx.config.is_favorite(&b.path.to_string_lossy());
        b_favorite
            .cmp(&a_favorite)
            .then(b.last_activity.cmp(&a.last_activity))
    });
    repos
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
