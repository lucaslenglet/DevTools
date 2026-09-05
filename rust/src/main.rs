mod config;
mod format;
mod menu;
mod scan;
mod screens;
mod text_input;
mod theme;
mod tui;

use anyhow::Result;
use config::{AppContext, CURRENT_VERSION};
use screens::Flow;

fn main() -> Result<()> {
    let mut ctx = AppContext::load()?;

    if ctx.config.version != CURRENT_VERSION {
        eprintln!(
            "Config version doesn't match tool version. ({} != {CURRENT_VERSION})",
            ctx.config.version
        );
        eprintln!("{}", ctx.config_file_path.display());
        std::process::exit(1);
    }

    let mut tui = tui::Tui::new()?;
    let result = run(&mut tui, &mut ctx);
    tui.restore()?;
    result
}

fn run(tui: &mut tui::Tui, ctx: &mut AppContext) -> Result<()> {
    if ctx.config.repo_paths.is_empty() {
        if let Flow::Quit = screens::repo_paths(tui, ctx)? {
            return Ok(());
        }
    }

    match screens::repositories(tui, ctx)? {
        Flow::Quit | Flow::Continue => Ok(()),
    }
}

#[cfg(test)]
#[path = "scan_tests.rs"]
mod scan_tests;
