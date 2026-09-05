use anyhow::Result;
use crossterm::event::{
    self, Event, KeyEvent, KeyEventKind, KeyboardEnhancementFlags, PopKeyboardEnhancementFlags,
    PushKeyboardEnhancementFlags,
};
use crossterm::terminal::{
    disable_raw_mode, enable_raw_mode, supports_keyboard_enhancement, EnterAlternateScreen,
    LeaveAlternateScreen,
};
use crossterm::{cursor, execute};
use ratatui::backend::CrosstermBackend;
use ratatui::Terminal;
use std::io::{self, Stdout};
use std::path::Path;
use std::process::Command;

pub struct Tui {
    pub terminal: Terminal<CrosstermBackend<Stdout>>,
    /// Whether the terminal accepted the keyboard enhancement flags, which is what makes
    /// `Ctrl+Enter` and `Shift+Enter` distinguishable from a plain `Enter`. Queried once:
    /// the answer cannot change while the process runs, and the query is a blocking
    /// round-trip to the terminal.
    enhanced_keys: bool,
}

impl Tui {
    pub fn new() -> Result<Self> {
        enable_raw_mode()?;
        let enhanced_keys = matches!(supports_keyboard_enhancement(), Ok(true));
        let mut tui = Self {
            terminal: Terminal::new(CrosstermBackend::new(io::stdout()))?,
            enhanced_keys,
        };
        tui.enter()?;
        Ok(tui)
    }

    pub fn restore(&mut self) -> Result<()> {
        if self.enhanced_keys {
            execute!(io::stdout(), PopKeyboardEnhancementFlags)?;
        }
        disable_raw_mode()?;
        execute!(io::stdout(), LeaveAlternateScreen, cursor::Show)?;
        Ok(())
    }

    /// Hands the terminal back to a child process, then takes it over again.
    pub fn run_command(
        &mut self,
        program: &str,
        working_directory: Option<String>,
        arguments: Option<String>,
    ) -> Result<()> {
        self.restore()?;

        let mut command = Command::new(program);
        if let Some(directory) = working_directory.filter(|dir| Path::new(dir).is_dir()) {
            command.current_dir(directory);
        }
        command.args(split_arguments(arguments.as_deref().unwrap_or_default()));

        let status = command.status();

        enable_raw_mode()?;
        self.enter()?;
        self.terminal.clear()?;

        if let Err(error) = status {
            // Surfaced on the next screen rather than crashing the app.
            eprintln!("Failed to start '{program}': {error}");
        }

        Ok(())
    }

    fn enter(&mut self) -> Result<()> {
        execute!(io::stdout(), EnterAlternateScreen, cursor::Hide)?;
        if self.enhanced_keys {
            execute!(
                io::stdout(),
                PushKeyboardEnhancementFlags(KeyboardEnhancementFlags::DISAMBIGUATE_ESCAPE_CODES)
            )?;
        }
        Ok(())
    }
}

/// Reads the next key press, skipping key-release and repeat events (Windows sends both).
pub fn next_key() -> Result<Option<KeyEvent>> {
    match event::read()? {
        Event::Key(key) if key.kind == KeyEventKind::Press => Ok(Some(key)),
        _ => Ok(None),
    }
}

/// Minimal command-line splitting on spaces, honouring double quotes.
fn split_arguments(input: &str) -> Vec<String> {
    let mut arguments = Vec::new();
    let mut current = String::new();
    let mut in_quotes = false;

    for c in input.chars() {
        match c {
            '"' => in_quotes = !in_quotes,
            c if c.is_whitespace() && !in_quotes => {
                if !current.is_empty() {
                    arguments.push(std::mem::take(&mut current));
                }
            }
            c => current.push(c),
        }
    }

    if !current.is_empty() {
        arguments.push(current);
    }

    arguments
}
