use crate::config::Config;
use crate::scan::RepoInfo;
use crate::theme;
use chrono::{DateTime, Local};
use ratatui::style::{Color, Style};
use ratatui::text::{Line, Span};
use std::time::SystemTime;
use unicode_width::{UnicodeWidthChar, UnicodeWidthStr};

/// One styled chunk of a repository row, before search highlighting is applied.
pub struct Segment {
    pub text: String,
    pub style: Style,
}

impl Segment {
    fn new(text: impl Into<String>, style: Style) -> Self {
        Self {
            text: text.into(),
            style,
        }
    }
}

pub fn repo_segments(repo: &RepoInfo, now: DateTime<Local>, config: &Config) -> Vec<Segment> {
    let custom_display_name = config.display_name(&repo.path);

    let favorite_icon = if config.is_favorite(&repo.path) {
        "\u{1F525} "
    } else {
        "   "
    };
    let renamed_icon = if custom_display_name.is_some() {
        // A text-presentation pencil: emoji pencils report one column to `unicode-width`
        // but are drawn on two by most terminals, which shifts every later column.
        "\u{270E} "
    } else {
        "  "
    };

    let time_text = format_time_ago(repo.last_activity, now);
    let (remote_status, remote_color) = remote_status(repo);

    let mut segments = vec![Segment::new(favorite_icon, Style::default())];
    segments.extend(display_name_segments(repo, custom_display_name));
    segments.push(Segment::new(renamed_icon, Style::default()));
    segments.push(Segment::new(pad(&time_text, 15), theme::dim()));
    segments.push(Segment::new(
        pad(&remote_status, 10),
        Style::default().fg(remote_color),
    ));
    segments.push(Segment::new(
        fit(&repo.branch, 50),
        Style::default().fg(branch_color(&repo.branch)),
    ));

    segments
}

/// Visible width of the name column. Every variant below fills exactly this many
/// columns so the following columns stay aligned.
const NAME_WIDTH: usize = 60;

const SEPARATOR: &str = " > ";

fn display_name_segments(repo: &RepoInfo, custom_display_name: Option<&str>) -> Vec<Segment> {
    if let Some(name) = custom_display_name {
        return vec![Segment::new(fit(name, NAME_WIDTH), Style::default())];
    }

    let Some(parent) = &repo.parent_folder else {
        return vec![Segment::new(fit(&repo.name, NAME_WIDTH), Style::default())];
    };

    // `parent > name` must still occupy NAME_WIDTH columns in total. The parent is
    // only context, so it gives up room first when the pair is too long.
    let available = NAME_WIDTH.saturating_sub(SEPARATOR.chars().count());
    let name = truncate(&repo.name, available);
    let parent = truncate(parent, available.saturating_sub(count(&name)));
    let padding = available.saturating_sub(count(&parent) + count(&name));

    vec![
        Segment::new(parent, theme::dim()),
        Segment::new(SEPARATOR, Style::default()),
        Segment::new(format!("{name}{}", " ".repeat(padding)), Style::default()),
    ]
}

/// Display width in terminal columns — the same measure ratatui uses to lay out spans,
/// which differs from the char count for wide glyphs and combining marks.
fn count(text: &str) -> usize {
    UnicodeWidthStr::width(text)
}

/// Truncates to `width` columns, marking the cut with an ellipsis.
fn truncate(text: &str, width: usize) -> String {
    if count(text) <= width {
        return text.to_string();
    }
    if width == 0 {
        return String::new();
    }

    let mut kept = String::new();
    let mut used = 0;

    for c in text.chars() {
        let char_width = UnicodeWidthChar::width(c).unwrap_or(0);
        if used + char_width > width - 1 {
            break;
        }
        kept.push(c);
        used += char_width;
    }

    // A wide glyph at the cut can leave one column spare; `pad` fills it.
    format!("{kept}\u{2026}")
}

/// Truncates or pads so the result is exactly `width` columns wide.
fn fit(text: &str, width: usize) -> String {
    pad(&truncate(text, width), width)
}

fn format_time_ago(last_activity: Option<SystemTime>, now: DateTime<Local>) -> String {
    let Some(last_activity) = last_activity else {
        return String::new();
    };

    let last: DateTime<Local> = last_activity.into();
    let elapsed = now.signed_duration_since(last);

    if elapsed.num_minutes() < 1 {
        format!("{}s ago", elapsed.num_seconds().max(0))
    } else if elapsed.num_hours() < 1 {
        format!("{}m ago", elapsed.num_minutes())
    } else if elapsed.num_days() < 1 {
        format!("{}h ago", elapsed.num_hours())
    } else if elapsed.num_days() < 7 {
        format!("{}d ago", elapsed.num_days())
    } else {
        last.format("%Y/%m/%d").to_string()
    }
}

fn remote_status(repo: &RepoInfo) -> (String, Color) {
    if !repo.has_tracking {
        return (String::new(), Color::DarkGray);
    }

    match (repo.ahead, repo.behind) {
        (0, 0) => ("\u{2713}".to_string(), theme::parse_color("green")),
        (0, behind) => (format!("\u{2193}{behind}"), theme::parse_color("red")),
        (ahead, 0) => (format!("\u{2191}{ahead}"), theme::parse_color("yellow")),
        (ahead, behind) => (
            format!("\u{2191}{ahead}\u{2193}{behind}"),
            theme::parse_color("orange1"),
        ),
    }
}

fn branch_color(branch: &str) -> Color {
    let branch = branch.to_lowercase();
    let name = match branch.as_str() {
        "main" | "master" => "steelblue",
        "develop" => "lightseagreen",
        _ if branch.starts_with("feature/") || branch.starts_with("feat/") => "yellow",
        _ if branch.starts_with("bugfix/") || branch.starts_with("fix/") => "red",
        _ if branch.starts_with("hotfix/") => "magenta",
        _ if branch.starts_with("release/") => "cyan",
        _ => "white",
    };
    theme::parse_color(name)
}

fn pad(text: &str, width: usize) -> String {
    format!("{text}{}", " ".repeat(width.saturating_sub(count(text))))
}

/// Renders a row, giving every case-insensitive occurrence of `needle` the search style.
pub fn highlight(segments: &[Segment], needle: &str, style: Style) -> Line<'static> {
    let plain = |segment: &Segment| Span::styled(segment.text.clone(), segment.style);

    if needle.is_empty() {
        return Line::from(segments.iter().map(plain).collect::<Vec<_>>());
    }

    let needle = needle.to_lowercase();
    let mut spans = Vec::new();

    for segment in segments {
        // Lowercasing can change byte lengths, so only ASCII text is byte-index safe.
        if !needle.is_ascii() || !segment.text.is_ascii() {
            spans.push(plain(segment));
            continue;
        }

        let mut cursor = 0;
        for (start, matched) in segment.text.to_lowercase().match_indices(&needle) {
            let end = start + matched.len();
            if start > cursor {
                spans.push(Span::styled(
                    segment.text[cursor..start].to_string(),
                    segment.style,
                ));
            }
            spans.push(Span::styled(segment.text[start..end].to_string(), style));
            cursor = end;
        }

        if cursor < segment.text.len() {
            spans.push(Span::styled(
                segment.text[cursor..].to_string(),
                segment.style,
            ));
        }
    }

    Line::from(spans)
}

/// Plain text of a row, used as the search key.
pub fn plain_text(segments: &[Segment]) -> String {
    segments.iter().map(|s| s.text.as_str()).collect()
}

#[cfg(test)]
#[path = "format_tests.rs"]
mod tests;
