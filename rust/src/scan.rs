use crate::config::{self, Config};
use rayon::prelude::*;
use std::fs;
use std::path::{Path, PathBuf};
use std::time::SystemTime;

/// Files whose timestamps mark the last activity in a repository. `logs/HEAD` is the
/// reflog: git touches it on every commit, checkout, merge, pull and reset, which makes it
/// a one-stat replacement for walking every file under `refs/heads`.
const ACTIVITY_FILES: [&str; 5] = ["logs/HEAD", "FETCH_HEAD", "HEAD", "index", "ORIG_HEAD"];

#[derive(Debug, Clone)]
pub struct RepoInfo {
    /// Absolute path as text: it is the key of the `favorites` and `displayNames` config
    /// maps, the argument passed to launched commands, and what the UI displays.
    pub path: String,
    pub name: String,
    /// Set when the repository was found one level below a scan root.
    pub parent_folder: Option<String>,
    pub last_activity: Option<SystemTime>,
    pub branch: String,
    pub ahead: usize,
    pub behind: usize,
    pub has_tracking: bool,
}

/// A directory that looks like a repository, before its git metadata is read.
struct Candidate {
    path: PathBuf,
    parent_folder: Option<String>,
}

/// Branch and upstream state, defaulting to "unknown" when the repository cannot be read.
#[derive(Default)]
struct GitStatus {
    branch: String,
    ahead: usize,
    behind: usize,
    has_tracking: bool,
}

pub fn scan(config: &Config) -> Vec<RepoInfo> {
    // Both phases are IO bound and independent per directory, and on WSL or a network
    // share the syscalls dominate everything else, so both run in parallel.
    config
        .repo_paths
        .par_iter()
        .flat_map(|root| find_candidates(&config::resolve_path(root)))
        .map(repo_info)
        .collect()
}

/// Repositories directly inside `root`, plus those one level deeper.
fn find_candidates(root: &Path) -> Vec<Candidate> {
    sub_directories(root)
        .into_par_iter()
        .flat_map(|dir| {
            if is_git_repository(&dir) {
                return vec![Candidate {
                    path: dir,
                    parent_folder: None,
                }];
            }

            let parent = file_name(&dir);
            sub_directories(&dir)
                .into_iter()
                .filter(|sub_dir| is_git_repository(sub_dir))
                .map(|sub_dir| Candidate {
                    path: sub_dir,
                    parent_folder: Some(parent.clone()),
                })
                .collect()
        })
        .collect()
}

fn sub_directories(path: &Path) -> Vec<PathBuf> {
    let Ok(entries) = fs::read_dir(path) else {
        return Vec::new();
    };

    entries
        .filter_map(Result::ok)
        .filter(|entry| entry.file_type().is_ok_and(|kind| kind.is_dir()))
        .map(|entry| entry.path())
        .collect()
}

fn is_git_repository(path: &Path) -> bool {
    path.join(".git").exists()
}

fn file_name(path: &Path) -> String {
    path.file_name()
        .unwrap_or(path.as_os_str())
        .to_string_lossy()
        .into_owned()
}

fn repo_info(candidate: Candidate) -> RepoInfo {
    let Candidate {
        path,
        parent_folder,
    } = candidate;

    let GitStatus {
        branch,
        ahead,
        behind,
        has_tracking,
    } = git_status(&path).unwrap_or_default();

    RepoInfo {
        name: file_name(&path),
        last_activity: last_activity_time(&path),
        path: path.to_string_lossy().into_owned(),
        parent_folder,
        branch,
        ahead,
        behind,
        has_tracking,
    }
}

fn last_activity_time(repo_dir: &Path) -> Option<SystemTime> {
    let git_dir = repo_dir.join(".git");
    if !git_dir.is_dir() {
        // A `.git` file means a worktree or submodule; fall back to the directory itself.
        return modified(repo_dir);
    }

    ACTIVITY_FILES
        .iter()
        .filter_map(|file| modified(&git_dir.join(file)))
        .max()
        .or_else(|| modified(repo_dir))
}

fn modified(path: &Path) -> Option<SystemTime> {
    fs::metadata(path)
        .and_then(|metadata| metadata.modified())
        .ok()
}

fn git_status(path: &Path) -> Option<GitStatus> {
    // `isolated` skips the system and user config files, which are otherwise re-read for
    // every single repository; the repository's own config is still loaded.
    let repo = gix::open_opts(path, gix::open::Options::isolated()).ok()?;
    let head = repo.head().ok()?;

    // A branch without commits yet ("unborn") still has a name; a detached HEAD has none,
    // so it falls back to the short commit id.
    let branch = match head.referent_name() {
        Some(name) => name.shorten().to_string(),
        None => format!("({})", head.id()?.shorten_or_id()),
    };

    let mut status = GitStatus {
        branch,
        ..GitStatus::default()
    };

    // An upstream that was never fetched still leaves the branch name usable — only the
    // tracking counters are lost.
    if let Some(mut head) = head.try_into_referent() {
        if let Some((ahead, behind)) = tracking_counts(&repo, &mut head) {
            status.ahead = ahead;
            status.behind = behind;
            status.has_tracking = true;
        }
    }

    Some(status)
}

fn tracking_counts(
    repo: &gix::Repository,
    head: &mut gix::Reference<'_>,
) -> Option<(usize, usize)> {
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

    Some((
        count_commits(repo, local_id, upstream_id),
        count_commits(repo, upstream_id, local_id),
    ))
}

/// Counts commits reachable from `tip` but not from `hidden`.
fn count_commits(repo: &gix::Repository, tip: gix::ObjectId, hidden: gix::ObjectId) -> usize {
    repo.rev_walk(Some(tip))
        .with_hidden(Some(hidden))
        .all()
        .map(|walk| walk.filter_map(Result::ok).count())
        .unwrap_or(0)
}

#[cfg(test)]
#[path = "scan_tests.rs"]
mod tests;
