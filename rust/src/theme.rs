use ratatui::style::{Color, Modifier, Style};

pub const HIGHLIGHT: Style = Style::new().fg(Color::Black).bg(Color::White);
pub const SEARCH_HIGHLIGHT: Style = Style::new().fg(Color::Black).bg(Color::Indexed(51));

pub fn dim() -> Style {
    Style::new().add_modifier(Modifier::DIM)
}

/// Resolves a Spectre.Console color name (as stored in `config.yml`), `#rrggbb`, or a
/// 256-color index. Unknown values fall back to white so a bad config never panics.
pub fn parse_color(name: &str) -> Color {
    let name = name.trim().to_ascii_lowercase();

    if let Some(hex) = name.strip_prefix('#') {
        if hex.len() == 6 {
            if let Ok(rgb) = u32::from_str_radix(hex, 16) {
                return Color::Rgb((rgb >> 16) as u8, (rgb >> 8) as u8, rgb as u8);
            }
        }
    }

    if let Ok(index) = name.parse::<u8>() {
        return Color::Indexed(index);
    }

    match name.as_str() {
        "black" => Color::Indexed(0),
        "maroon" => Color::Indexed(1),
        "green" => Color::Indexed(2),
        "olive" => Color::Indexed(3),
        "navy" => Color::Indexed(4),
        "purple" => Color::Indexed(5),
        "teal" => Color::Indexed(6),
        "silver" => Color::Indexed(7),
        "grey" | "gray" => Color::Indexed(8),
        "red" => Color::Indexed(9),
        "lime" => Color::Indexed(10),
        "yellow" => Color::Indexed(11),
        "blue" => Color::Indexed(12),
        "fuchsia" | "magenta" => Color::Indexed(13),
        "aqua" | "cyan" => Color::Indexed(14),
        "white" => Color::Indexed(15),
        "grey63" => Color::Indexed(139),
        "steelblue" => Color::Indexed(67),
        "lightseagreen" => Color::Indexed(37),
        "lightsteelblue" => Color::Indexed(147),
        "darkorange" => Color::Indexed(208),
        "orange1" => Color::Indexed(214),
        "hotpink" => Color::Indexed(205),
        "cyan1" => Color::Indexed(51),
        "magenta1" => Color::Indexed(201),
        "deepskyblue1" => Color::Indexed(39),
        "springgreen1" => Color::Indexed(48),
        _ => Color::White,
    }
}
