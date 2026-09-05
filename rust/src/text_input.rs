use crate::theme;
use crate::tui::{self, Tui};
use anyhow::Result;
use crossterm::event::{KeyCode, KeyModifiers};
use ratatui::layout::{Constraint, Layout};
use ratatui::text::{Line, Span};
use ratatui::widgets::Paragraph;

/// Prompts for a single line of text. Returns `None` when the user cancels with ESC.
pub fn prompt(tui: &mut Tui, prompt: Line<'static>, default_value: &str) -> Result<Option<String>> {
    let mut value = default_value.to_string();
    let mut cursor = value.chars().count();

    loop {
        tui.terminal.draw(|frame| {
            let [hints, _, input] =
                Layout::vertical([Constraint::Length(1); 3]).areas(frame.area());

            frame.render_widget(
                Paragraph::new(crate::list::hints_line(&[
                    "Empty to delete",
                    "Press ESC to go back",
                ])),
                hints,
            );

            let at = byte_index(&value, cursor);
            let mut line = prompt.clone();
            line.spans.extend([
                Span::raw(" "),
                Span::raw(value[..at].to_string()),
                Span::styled("\u{2588}", theme::dim()),
                Span::raw(value[at..].to_string()),
            ]);

            frame.render_widget(Paragraph::new(line), input);
        })?;

        let Some(key) = tui::next_key()? else {
            continue;
        };
        let control = key.modifiers.contains(KeyModifiers::CONTROL);
        let length = value.chars().count();

        match key.code {
            KeyCode::Enter => return Ok(Some(value)),
            KeyCode::Esc => return Ok(None),
            KeyCode::Left => cursor = cursor.saturating_sub(1),
            KeyCode::Right => cursor = (cursor + 1).min(length),
            KeyCode::Home => cursor = 0,
            KeyCode::End => cursor = length,
            KeyCode::Delete if cursor < length => {
                value.remove(byte_index(&value, cursor));
            }
            KeyCode::Backspace if control => {
                value.clear();
                cursor = 0;
            }
            KeyCode::Backspace if cursor > 0 => {
                cursor -= 1;
                value.remove(byte_index(&value, cursor));
            }
            KeyCode::Char(c) if !control => {
                value.insert(byte_index(&value, cursor), c);
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
        .map_or(value.len(), |(at, _)| at)
}
