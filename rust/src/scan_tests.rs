use crate::config::Config;
use crate::scan;
use std::path::{Path, PathBuf};
use std::process::Command;

/// Creates `<root>/<name>` as a git repository with one empty commit on `branch`.
fn init_repo(root: &Path, name: &str, branch: &str) -> PathBuf {
    let path = root.join(name);
    std::fs::create_dir_all(&path).unwrap();

    git(&path, &["init", "-q", "-b", branch]);
    git(&path, &["config", "user.email", "test@example.com"]);
    git(&path, &["config", "user.name", "test"]);
    git(&path, &["commit", "-q", "--allow-empty", "-m", "init"]);

    path
}

fn git(dir: &Path, args: &[&str]) {
    let status = Command::new("git")
        .current_dir(dir)
        .args(args)
        .status()
        .expect("git must be installed to run this test");
    assert!(status.success(), "git {args:?} failed");
}

fn temp_root(name: &str) -> PathBuf {
    let root = std::env::temp_dir().join(format!("devtools-scan-{name}"));
    let _ = std::fs::remove_dir_all(&root);
    std::fs::create_dir_all(&root).unwrap();
    root
}

#[test]
fn finds_repositories_at_depth_one_and_two() {
    let root = temp_root("depth");
    init_repo(&root, "flat", "main");
    init_repo(&root.join("group"), "nested", "feature/x");

    let config = Config {
        repo_paths: vec![root.to_string_lossy().into_owned()],
        ..Config::default()
    };

    let repos = scan::scan(&config);

    let flat = repos.iter().find(|r| r.name == "flat").expect("flat repo");
    assert_eq!(flat.branch, "main");
    assert_eq!(flat.parent_folder, None);
    assert!(!flat.has_tracking);

    let nested = repos
        .iter()
        .find(|r| r.name == "nested")
        .expect("nested repo");
    assert_eq!(nested.branch, "feature/x");
    assert_eq!(nested.parent_folder.as_deref(), Some("group"));

    let _ = std::fs::remove_dir_all(&root);
}

#[test]
fn shows_the_branch_of_a_repository_without_commits() {
    let root = temp_root("unborn");
    let path = root.join("fresh");
    std::fs::create_dir_all(&path).unwrap();
    git(&path, &["init", "-q", "-b", "master"]);

    let config = Config {
        repo_paths: vec![root.to_string_lossy().into_owned()],
        ..Config::default()
    };

    let repos = scan::scan(&config);
    let fresh = repos.iter().find(|r| r.name == "fresh").expect("fresh");

    assert_eq!(fresh.branch, "master");
    assert!(!fresh.has_tracking);

    let _ = std::fs::remove_dir_all(&root);
}

#[test]
fn reports_ahead_and_behind_against_the_tracking_branch() {
    let root = temp_root("tracking");
    let origin = init_repo(&root, "origin", "main");

    let clone = root.join("clone");
    let status = Command::new("git")
        .current_dir(&root)
        .args(["clone", "-q", &origin.to_string_lossy(), "clone"])
        .status()
        .unwrap();
    assert!(status.success());

    git(&clone, &["config", "user.email", "test@example.com"]);
    git(&clone, &["config", "user.name", "test"]);
    git(&clone, &["commit", "-q", "--allow-empty", "-m", "local"]);
    git(&clone, &["commit", "-q", "--allow-empty", "-m", "local 2"]);

    let config = Config {
        repo_paths: vec![root.to_string_lossy().into_owned()],
        ..Config::default()
    };

    let repos = scan::scan(&config);
    let cloned = repos.iter().find(|r| r.name == "clone").expect("clone");

    assert!(cloned.has_tracking);
    assert_eq!(cloned.ahead, 2);
    assert_eq!(cloned.behind, 0);

    let _ = std::fs::remove_dir_all(&root);
}
