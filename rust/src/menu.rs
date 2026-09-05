use crate::theme;
use ratatui::layout::Rect;
use ratatui::text::{Line, Span};
use ratatui::widgets::{List, ListItem, ListState};
use ratatui::Frame;

/// Cursor + incremental-search state shared by every list screen.
pub struct MenuState {
    pub index: usize,
    pub search: String,
    pub searching: bool,
    len: usize,
    wrap: bool,
    list_state: ListState,
}

impl MenuState {
    pub fn new(len: usize, wrap: bool) -> Self {
        Self {
            index: 0,
            search: String::new(),
            searching: false,
            len,
            wrap,
            list_state: ListState::default(),
        }
    }

    pub fn set_len(&mut self, len: usize) {
        self.len = len;
        if self.index >= len {
            self.index = len.saturating_sub(1);
        }
    }

    pub fn move_relative(&mut self, delta: isize) {
        if self.len == 0 {
            return;
        }

        let len = self.len as isize;
        let next = self.index as isize + delta;

        self.index = if self.wrap {
            ((next % len) + len) % len
        } else {
            next.clamp(0, len - 1)
        } as usize;
    }

    /// Page moves are clamped even when wrap-around is enabled, matching the C# behaviour.
    pub fn move_page(&mut self, pages: isize, page_size: usize) {
        if self.len == 0 {
            return;
        }

        let next = self.index as isize + pages * page_size as isize;
        self.index = next.clamp(0, self.len as isize - 1) as usize;
    }

    pub fn move_first(&mut self) {
        self.index = 0;
    }

    pub fn move_last(&mut self) {
        self.index = self.len.saturating_sub(1);
    }

    pub fn start_search(&mut self) {
        self.searching = true;
    }

    pub fn cancel_search(&mut self) {
        self.searching = false;
        self.search.clear();
    }

    pub fn push_search(&mut self, c: char, keys: &[String]) {
        self.search.push(c);
        self.jump_to_match(keys);
    }

    pub fn backspace_search(&mut self, clear_all: bool, keys: &[String]) {
        if clear_all {
            self.search.clear();
        } else {
            self.search.pop();
        }
        self.jump_to_match(keys);
    }

    fn jump_to_match(&mut self, keys: &[String]) {
        if self.search.is_empty() {
            return;
        }

        let needle = self.search.to_lowercase();
        if let Some(index) = keys.iter().position(|k| k.to_lowercase().contains(&needle)) {
            self.index = index;
        }
    }

    pub fn render(&mut self, frame: &mut Frame, area: Rect, rows: &[Line<'static>]) {
        let items: Vec<ListItem> = rows
            .iter()
            .enumerate()
            .map(|(index, row)| {
                let marker = if index == self.index { "> " } else { "  " };
                let mut line = Line::from(vec![Span::raw(marker)]);
                line.spans.extend(row.spans.iter().cloned());
                ListItem::new(line)
            })
            .collect();

        let list = List::new(items)
            .highlight_style(theme::HIGHLIGHT)
            .scroll_padding(area.height as usize / 2);

        self.list_state.select(if self.len == 0 {
            None
        } else {
            Some(self.index)
        });

        frame.render_stateful_widget(list, area, &mut self.list_state);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn keys() -> Vec<String> {
        vec!["alpha".into(), "beta".into(), "gamma".into()]
    }

    #[test]
    fn wraps_around_both_ends() {
        let mut menu = MenuState::new(3, true);
        menu.move_relative(-1);
        assert_eq!(menu.index, 2);
        menu.move_relative(1);
        assert_eq!(menu.index, 0);
    }

    #[test]
    fn clamps_without_wrap() {
        let mut menu = MenuState::new(3, false);
        menu.move_relative(-1);
        assert_eq!(menu.index, 0);
        menu.move_relative(10);
        assert_eq!(menu.index, 2);
    }

    #[test]
    fn search_jumps_to_first_case_insensitive_match() {
        let mut menu = MenuState::new(3, true);
        menu.start_search();
        menu.push_search('G', &keys());
        assert_eq!(menu.index, 2);
        assert_eq!(menu.search, "G");
    }

    #[test]
    fn backspace_clears_search_text() {
        let mut menu = MenuState::new(3, true);
        menu.start_search();
        menu.push_search('g', &keys());
        menu.backspace_search(false, &keys());
        assert!(menu.search.is_empty());
    }

    #[test]
    fn shrinking_the_list_keeps_the_cursor_in_range() {
        let mut menu = MenuState::new(5, true);
        menu.move_last();
        menu.set_len(2);
        assert_eq!(menu.index, 1);
    }
}
