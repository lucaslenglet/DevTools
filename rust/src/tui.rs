use anyhow::Result;
use crossterm::event::{self, Event, KeyEvent, KeyEventKind};
use crossterm::terminal::{
    disable_raw_mode, enable_raw_mode, EnterAlternateScreen, LeaveAlternateScreen,
};
use crossterm::{execute, ExecutableCommand};
use ratatui::backend::CrosstermBackend;
use ratatui::Terminal;
use std::io::{self, Stdout};
use std::path::Path;
use std::process::Command;

pub struct Tui {
    pub terminal: Terminal<CrosstermBackend<Stdout>>,
}

impl Tui {
    pub fn new() -> Result<Self> {
        enable_raw_mode()?;
        let mut stdout = io::stdout();
        execute!(stdout, EnterAlternateScreen, crossterm::cursor::Hide)?;
        let terminal = Terminal::new(CrosstermBackend::new(stdout))?;
        Ok(Self { terminal })
    }

    pub fn restore(&mut self) -> Result<()> {
        disable_raw_mode()?;
        execute!(
            self.terminal.backend_mut(),
            LeaveAlternateScreen,
            crossterm::cursor::Show
        )?;
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
        if let Some(dir) = working_directory.as_deref() {
            if Path::new(dir).is_dir() {
                command.current_dir(dir);
            }
        }
        for argument in split_arguments(arguments.as_deref().unwrap_or_default()) {
            command.arg(argument);
        }

        let status = command.status();

        enable_raw_mode()?;
        io::stdout().execute(EnterAlternateScreen)?;
        io::stdout().execute(crossterm::cursor::Hide)?;
        self.terminal.clear()?;

        if let Err(error) = status {
            // Surfaced on the next screen rather than crashing the app.
            eprintln!("Failed to start '{program}': {error}");
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
