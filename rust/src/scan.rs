use crate::config::Config;
use rayon::prelude::*;
use std::fs;
use std::path::{Path, PathBuf};
use std::time::SystemTime;

const GIT_ACTIVITY_FILES: [&str; 4] = ["FETCH_HEAD", "HEAD", "index", "ORIG_HEAD"];

#[derive(Debug, Clone)]
pub struct RepoInfo {
    pub path: PathBuf,
    pub name: String,
    /// Set when the repository was found one level below a scan root.
    pub parent_folder: Option<String>,
    pub last_activity: Option<SystemTime>,
    pub branch: String,
    pub ahead: usize,
    pub behind: usize,
    pub has_tracking: bool,
}

pub fn scan(config: &Config) -> Vec<RepoInfo> {
    let candidates: Vec<(PathBuf, Option<String>)> = config
        .repo_paths
        .iter()
        .flat_map(|root| find_git_directories(Path::new(root)))
        .collect();

    let mut repos: Vec<RepoInfo> = candidates
        .into_par_iter()
        .map(|(dir, parent_folder)| repo_info(dir, parent_folder))
        .collect();

    repos.sort_by_key(|repo| std::cmp::Reverse(repo.last_activity));
    repos
}

fn find_git_directories(root: &Path) -> Vec<(PathBuf, Option<String>)> {
    let mut found = Vec::new();

    for dir in sub_directories(root) {
        if is_git_repository(&dir) {
            found.push((dir, None));
            continue;
        }

        let parent_name = file_name(&dir);
        for sub_dir in sub_directories(&dir) {
            if is_git_repository(&sub_dir) {
                found.push((sub_dir, Some(parent_name.clone())));
            }
        }
    }

    found
}

fn sub_directories(path: &Path) -> Vec<PathBuf> {
    let Ok(entries) = fs::read_dir(path) else {
        return Vec::new();
    };

    entries
        .filter_map(Result::ok)
        .filter(|entry| entry.file_type().map(|t| t.is_dir()).unwrap_or(false))
        .map(|entry| entry.path())
        .collect()
}

fn is_git_repository(path: &Path) -> bool {
    path.join(".git").exists()
}

fn file_name(path: &Path) -> String {
    path.file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| path.to_string_lossy().into_owned())
}

fn repo_info(path: PathBuf, parent_folder: Option<String>) -> RepoInfo {
    let name = file_name(&path);
    let last_activity = last_activity_time(&path);
    let (branch, ahead, behind, has_tracking) = git_info(&path).unwrap_or_default();

    RepoInfo {
        path,
        name,
        parent_folder,
        last_activity,
        branch,
        ahead,
        behind,
        has_tracking,
    }
}

fn last_activity_time(repo_dir: &Path) -> Option<SystemTime> {
    let git_dir = repo_dir.join(".git");
    if !git_dir.is_dir() {
        return modified(repo_dir);
    }

    let mut times: Vec<SystemTime> = GIT_ACTIVITY_FILES
        .iter()
        .filter_map(|file| modified(&git_dir.join(file)))
        .collect();

    collect_modified_recursive(&git_dir.join("refs").join("heads"), &mut times);

    times.into_iter().max().or_else(|| modified(repo_dir))
}

fn collect_modified_recursive(path: &Path, times: &mut Vec<SystemTime>) {
    let Ok(entries) = fs::read_dir(path) else {
        return;
    };

    for entry in entries.filter_map(Result::ok) {
        let entry_path = entry.path();
        match entry.file_type() {
            Ok(t) if t.is_dir() => collect_modified_recursive(&entry_path, times),
            Ok(_) => times.extend(modified(&entry_path)),
            Err(_) => {}
        }
    }
}

fn modified(path: &Path) -> Option<SystemTime> {
    fs::metadata(path).and_then(|m| m.modified()).ok()
}

/// Returns `(branch, ahead, behind, has_tracking)`. Errors degrade to an empty branch name.
fn git_info(path: &Path) -> Option<(String, usize, usize, bool)> {
    let repo = gix::open(path).ok()?;

    let head = repo.head().ok()?;

    // A branch without commits yet ("unborn") still has a name; a detached HEAD has none,
    // so it falls back to the short commit id.
    let branch = match head.referent_name() {
        Some(name) => name.shorten().to_string(),
        None => format!("({})", head.id()?.shorten_or_id()),
    };

    let Some(mut head) = head.try_into_referent() else {
        return Some((branch, 0, 0, false));
    };

    // An upstream that was never fetched still leaves the branch name usable —
    // only the tracking counters are lost.
    let (ahead, behind, has_tracking) = tracking_info(&repo, &mut head).unwrap_or((0, 0, false));

    Some((branch, ahead, behind, has_tracking))
}

fn tracking_info(
    repo: &gix::Repository,
    head: &mut gix::Reference<'_>,
) -> Option<(usize, usize, bool)> {
    let tracking_name = head
        .clone()
        .remote_tracking_ref_name(gix::remote::Direction::Fetch)?
        .ok()?;

    let upstream_id = repo
        .find_reference(tracking_name.as_bstr())
        .ok()?
        .peel_to_id()
        .ok()?
        .detach();

    let local_id = head.peel_to_id().ok()?.detach();

    let ahead = count_commits(repo, local_id, upstream_id);
    let behind = count_commits(repo, upstream_id, local_id);

    Some((ahead, behind, true))
}

/// Counts commits reachable from `tip` but not from `hidden`.
fn count_commits(repo: &gix::Repository, tip: gix::ObjectId, hidden: gix::ObjectId) -> usize {
    repo.rev_walk(Some(tip))
        .with_hidden(Some(hidden))
        .all()
        .map(|walk| walk.filter_map(Result::ok).count())
        .unwrap_or(0)
}
