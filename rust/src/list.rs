//! The chrome shared by every list screen: layout, hints, incremental search and
//! cursor movement. Screens supply their rows and interpret the keys left over.

use crate::menu::MenuState;
use crate::theme;
use crate::tui::{self, Tui};
use anyhow::Result;
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use ratatui::layout::{Constraint, Layout};
use ratatui::text::{Line, Span};
use ratatui::widgets::Paragraph;

/// A screen showing a title, a list and a hint bar. Drawing, navigation, incremental
/// search and the quit key are handled here; every other key goes back to the caller.
pub struct ListScreen<'a> {
    pub hints: &'a [&'a str],
    pub title: Line<'static>,
    pub rows: &'a [Line<'static>],
    /// One search key per row. `None` disables incremental search on this screen.
    pub search_keys: Option<&'a [String]>,
}

/// The outcome of one draw-and-read cycle.
pub enum Input {
    /// The quit key was pressed.
    Quit,
    /// Navigation or search consumed the key; nothing for the caller to do.
    Handled,
    /// A key the screen itself has to interpret.
    Other(KeyEvent),
}

impl ListScreen<'_> {
    pub fn show(&self, tui: &mut Tui, menu: &mut MenuState) -> Result<Input> {
        let mut page_size = 0;

        tui.terminal.draw(|frame| {
            let [hints, _, title, _, list, _, footer] = Layout::vertical([
                Constraint::Length(1),
                Constraint::Length(1),
                Constraint::Length(1),
                Constraint::Length(1),
                Constraint::Min(1),
                Constraint::Length(1),
                Constraint::Length(1),
            ])
            .areas(frame.area());

            page_size = list.height as usize;

            frame.render_widget(Paragraph::new(hints_line(self.hints)), hints);
            frame.render_widget(Paragraph::new(self.title.clone()), title);
            menu.render(frame, list, self.rows);

            if self.search_keys.is_some() {
                frame.render_widget(Paragraph::new(search_line(menu)), footer);
            }
        })?;

        let Some(key) = tui::next_key()? else {
            return Ok(Input::Handled);
        };

        // Search swallows plain characters, so it gets first refusal on every key.
        if let Some(keys) = self.search_keys {
            if handle_search_key(&key, menu, keys) {
                return Ok(Input::Handled);
            }
        }

        if matches!(key.code, KeyCode::Char('q') | KeyCode::Char('Q')) {
            return Ok(Input::Quit);
        }

        if navigate(&key, menu, page_size) {
            return Ok(Input::Handled);
        }

        Ok(Input::Other(key))
    }
}

pub fn hints_line(hints: &[&str]) -> Line<'static> {
    let mut spans = Vec::new();
    for (index, hint) in hints.iter().enumerate() {
        if index > 0 {
            spans.push(Span::styled(" | ", theme::dim()));
        }
        spans.push(Span::styled(hint.to_string(), theme::dim()));
    }
    Line::from(spans)
}

pub fn title_line(prefix: &str, highlight: &str, suffix: Vec<Span<'static>>) -> Line<'static> {
    let mut spans = vec![
        Span::raw(prefix.to_string()),
        Span::styled(highlight.to_string(), theme::fg("green")),
    ];
    spans.extend(suffix);
    Line::from(spans)
}

fn search_line(menu: &MenuState) -> Line<'static> {
    if !menu.searching {
        return Line::from(Span::styled("(Press ? to search)", theme::dim()));
    }

    Line::from(vec![
        Span::styled("Searching", theme::SEARCH_HIGHLIGHT),
        Span::styled(" (Press ESC to cancel)", theme::dim()),
        Span::raw(format!(" : {}", menu.search)),
    ])
}

/// Returns true when the key was consumed by search handling.
fn handle_search_key(key: &KeyEvent, menu: &mut MenuState, keys: &[String]) -> bool {
    if !menu.searching {
        let starts_search = key.code == KeyCode::Char('?');
        if starts_search {
            menu.start_search();
        }
        return starts_search;
    }

    match key.code {
        KeyCode::Esc => menu.cancel_search(),
        KeyCode::Backspace => {
            menu.backspace_search(key.modifiers.contains(KeyModifiers::CONTROL), keys)
        }
        KeyCode::Char(c) if !key.modifiers.contains(KeyModifiers::CONTROL) => {
            menu.push_search(c, keys)
        }
        _ => return false,
    }

    true
}

/// Returns true when the key moved the cursor.
fn navigate(key: &KeyEvent, menu: &mut MenuState, page_size: usize) -> bool {
    // Ctrl+<letter> is a binding of its own, never a movement.
    if key.modifiers.contains(KeyModifiers::CONTROL) {
        return false;
    }

    match key.code {
        KeyCode::Up | KeyCode::Char('k') => menu.move_relative(-1),
        KeyCode::Down | KeyCode::Char('j') => menu.move_relative(1),
        KeyCode::Home => menu.move_first(),
        KeyCode::End => menu.move_last(),
        KeyCode::PageUp => menu.move_page(-1, page_size),
        KeyCode::PageDown => menu.move_page(1, page_size),
        _ => return false,
    }

    true
}
