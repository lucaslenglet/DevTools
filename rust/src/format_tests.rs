use super::*;
use crate::config::Config;
use std::path::PathBuf;

fn repo(name: &str, parent_folder: Option<&str>) -> RepoInfo {
    RepoInfo {
        path: PathBuf::from(format!(r"C:\Dev\{name}")),
        name: name.to_string(),
        parent_folder: parent_folder.map(str::to_string),
        last_activity: None,
        branch: "main".to_string(),
        ahead: 0,
        behind: 0,
        has_tracking: true,
    }
}

fn row(repo: &RepoInfo, config: &Config) -> String {
    plain_text(&repo_segments(repo, Local::now(), config))
}

/// Column the pattern starts at, measured the way the terminal lays the row out.
fn column_of(row: &str, pattern: &str) -> Option<usize> {
    row.find(pattern).map(|at| count(&row[..at]))
}

#[test]
fn nested_repositories_use_the_same_column_widths_as_flat_ones() {
    let config = Config::default();

    let flat = row(&repo("DevTools", None), &config);
    let nested = row(&repo("vars", Some("HelloGleam")), &config);

    assert_eq!(count(&flat), count(&nested));
    // The branch is the last column, so a matching start offset means every column lines up.
    assert_eq!(column_of(&flat, "main"), column_of(&nested, "main"));
}

#[test]
fn a_renamed_repository_keeps_the_columns_aligned() {
    let mut config = Config::default();
    let repo = repo("DevTools", None);
    let plain = row(&repo, &config);

    config.set_display_name(&repo.path.to_string_lossy(), Some("tools"));
    let renamed = row(&repo, &config);

    assert_eq!(count(&plain), count(&renamed));
    assert_eq!(column_of(&plain, "main"), column_of(&renamed, "main"));
}

#[test]
fn overlong_names_are_truncated_instead_of_shifting_the_columns() {
    let config = Config::default();

    let flat = row(&repo("DevTools", None), &config);
    let long = row(&repo(&"x".repeat(80), None), &config);
    let long_nested = row(&repo(&"y".repeat(40), Some(&"z".repeat(40))), &config);

    assert_eq!(count(&flat), count(&long));
    assert_eq!(count(&flat), count(&long_nested));
    assert!(long.contains('\u{2026}'));
    assert!(long_nested.contains('\u{2026}'));
}

#[test]
fn the_parent_folder_gives_up_room_before_the_repository_name() {
    let config = Config::default();
    let nested = row(&repo("short-name", Some(&"p".repeat(80))), &config);

    assert!(nested.contains("short-name"));
    assert!(nested.contains("\u{2026} > "));
}

#[test]
fn a_favorite_keeps_the_columns_aligned() {
    let mut config = Config::default();
    let repo = repo("DevTools", None);
    let plain = row(&repo, &config);

    config.toggle_favorite(&repo.path.to_string_lossy());
    let favorite = row(&repo, &config);

    assert_eq!(count(&plain), count(&favorite));
    assert_eq!(column_of(&plain, "main"), column_of(&favorite, "main"));
}

#[test]
fn wide_glyph_names_are_measured_in_columns_not_chars() {
    let config = Config::default();

    let plain = row(&repo("DevTools", None), &config);
    let wide = row(&repo("東京プロジェクト", None), &config);

    assert_eq!(count(&plain), count(&wide));
    assert_eq!(column_of(&plain, "main"), column_of(&wide, "main"));
}
