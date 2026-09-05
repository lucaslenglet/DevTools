use crate::theme;
use crate::tui::{self, Tui};
use anyhow::Result;
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use ratatui::layout::{Constraint, Layout};
use ratatui::text::{Line, Span};
use ratatui::widgets::Paragraph;

/// Prompts for a single line of text. Returns `None` when the user cancels with ESC.
pub fn prompt(
    tui: &mut Tui,
    prompt: Vec<Span<'static>>,
    default_value: &str,
) -> Result<Option<String>> {
    let mut value = default_value.to_string();
    let mut cursor = value.chars().count();

    loop {
        tui.terminal.draw(|frame| {
            let [hints, _, input] = Layout::vertical([
                Constraint::Length(1),
                Constraint::Length(1),
                Constraint::Length(1),
            ])
            .areas(frame.area());

            frame.render_widget(
                Paragraph::new(Line::from(vec![
                    Span::styled("Empty to delete", theme::dim()),
                    Span::styled(" | ", theme::dim()),
                    Span::styled("Press ESC to go back", theme::dim()),
                ])),
                hints,
            );

            let byte_cursor = byte_index(&value, cursor);
            let mut line = prompt.clone();
            line.push(Span::raw(" "));
            line.push(Span::raw(value[..byte_cursor].to_string()));
            line.push(Span::styled("\u{2588}", theme::dim()));
            line.push(Span::raw(value[byte_cursor..].to_string()));

            frame.render_widget(Paragraph::new(Line::from(line)), input);
        })?;

        let Some(key) = tui::next_key()? else {
            continue;
        };

        match key {
            KeyEvent {
                code: KeyCode::Enter,
                ..
            } => return Ok(Some(value)),
            KeyEvent {
                code: KeyCode::Esc, ..
            } => return Ok(None),
            KeyEvent {
                code: KeyCode::Left,
                ..
            } => cursor = cursor.saturating_sub(1),
            KeyEvent {
                code: KeyCode::Right,
                ..
            } => cursor = (cursor + 1).min(value.chars().count()),
            KeyEvent {
                code: KeyCode::Home,
                ..
            } => cursor = 0,
            KeyEvent {
                code: KeyCode::End, ..
            } => cursor = value.chars().count(),
            KeyEvent {
                code: KeyCode::Delete,
                ..
            } => {
                if cursor < value.chars().count() {
                    let at = byte_index(&value, cursor);
                    value.remove(at);
                }
            }
            KeyEvent {
                code: KeyCode::Backspace,
                modifiers,
                ..
            } => {
                if modifiers.contains(KeyModifiers::CONTROL) {
                    value.clear();
                    cursor = 0;
                } else if cursor > 0 {
                    let at = byte_index(&value, cursor - 1);
                    value.remove(at);
                    cursor -= 1;
                }
            }
            KeyEvent {
                code: KeyCode::Char(c),
                modifiers,
                ..
            } if !modifiers.contains(KeyModifiers::CONTROL) => {
                let at = byte_index(&value, cursor);
                value.insert(at, c);
                cursor += 1;
            }
            _ => {}
        }
    }
}

fn byte_index(value: &str, char_index: usize) -> usize {
    value
        .char_indices()
        .nth(char_index)
        .map(|(i, _)| i)
        .unwrap_or(value.len())
}
