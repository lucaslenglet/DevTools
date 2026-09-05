use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use std::fs;
use std::path::{Path, PathBuf};

pub const CURRENT_VERSION: u32 = 1;

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", default)]
pub struct ConfigCommand {
    pub process_name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub working_directory: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub arguments: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub color: Option<String>,
}

impl ConfigCommand {
    fn new(
        process_name: &str,
        name: &str,
        color: &str,
        working_directory: Option<&str>,
        arguments: Option<&str>,
    ) -> Self {
        Self {
            process_name: process_name.to_string(),
            name: Some(name.to_string()),
            working_directory: working_directory.map(str::to_string),
            arguments: arguments.map(str::to_string),
            color: Some(color.to_string()),
        }
    }

    /// Falls back to the process name, mirroring the C# `Name` getter.
    pub fn display_name(&self) -> &str {
        match &self.name {
            Some(name) if !name.is_empty() => name,
            _ => &self.process_name,
        }
    }

    fn lazygit() -> Self {
        Self::new("lazygit", "Lazygit", "hotpink", Some("{0}"), None)
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", default)]
pub struct Config {
    pub version: u32,
    pub repo_paths: Vec<String>,
    pub favorites: BTreeSet<String>,
    pub display_names: BTreeMap<String, String>,
    pub default_command: ConfigCommand,
    pub custom_commands: Vec<ConfigCommand>,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            version: CURRENT_VERSION,
            repo_paths: Vec::new(),
            favorites: BTreeSet::new(),
            display_names: BTreeMap::new(),
            default_command: ConfigCommand::lazygit(),
            custom_commands: vec![
                ConfigCommand::new(
                    "pwsh",
                    "VS Code",
                    "lightsteelblue",
                    None,
                    Some("-Command \"code {0}\""),
                ),
                ConfigCommand::new("claude", "Claude Code", "darkorange", Some("{0}"), None),
                ConfigCommand::new("copilot", "Copilot", "silver", Some("{0}"), None),
                ConfigCommand::new("codex", "Codex", "grey63", Some("{0}"), None),
                ConfigCommand::new("vibe", "Vibe", "orange1", Some("{0}"), None),
                ConfigCommand::new("pwsh", "Powershell", "blue", Some("{0}"), None),
                ConfigCommand::lazygit(),
                ConfigCommand::new("explorer.exe", "File Explorer", "green", None, Some("{0}")),
            ],
        }
    }
}

impl Config {
    pub fn is_favorite(&self, repo_path: &str) -> bool {
        self.favorites.contains(repo_path)
    }

    pub fn toggle_favorite(&mut self, repo_path: &str) {
        if !self.favorites.remove(repo_path) {
            self.favorites.insert(repo_path.to_string());
        }
    }

    pub fn display_name(&self, repo_path: &str) -> Option<&str> {
        self.display_names.get(repo_path).map(String::as_str)
    }

    pub fn set_display_name(&mut self, repo_path: &str, display_name: Option<&str>) {
        match display_name.map(str::trim).filter(|n| !n.is_empty()) {
            Some(name) => {
                self.display_names
                    .insert(repo_path.to_string(), name.to_string());
            }
            None => {
                self.display_names.remove(repo_path);
            }
        }
    }
}

pub struct AppContext {
    pub config_file_path: PathBuf,
    pub config: Config,
}

impl AppContext {
    pub fn load() -> Result<Self> {
        let config_file_path = config_file_path()?;
        let config = load(&config_file_path)?;
        Ok(Self {
            config_file_path,
            config,
        })
    }

    pub fn save(&self) -> Result<()> {
        if let Some(directory) = self.config_file_path.parent() {
            fs::create_dir_all(directory)
                .with_context(|| format!("creating {}", directory.display()))?;
        }

        let yml = serde_yaml_ng::to_string(&self.config)?;
        fs::write(&self.config_file_path, yml)
            .with_context(|| format!("writing {}", self.config_file_path.display()))?;
        Ok(())
    }
}

fn config_file_path() -> Result<PathBuf> {
    let local_app_data =
        dirs::data_local_dir().context("could not resolve the local application data folder")?;
    Ok(local_app_data.join("DevTools").join("config.yml"))
}

fn load(path: &Path) -> Result<Config> {
    if !path.exists() {
        return Ok(Config::default());
    }

    let yml = fs::read_to_string(path).with_context(|| format!("reading {}", path.display()))?;
    let config =
        serde_yaml_ng::from_str(&yml).with_context(|| format!("parsing {}", path.display()))?;
    Ok(config)
}

/// Replaces the `{0}` placeholder with the repository path, mirroring `StringHelper.FormatIfNotNull`.
pub fn format_if_some(pattern: Option<&String>, value: &str) -> Option<String> {
    pattern.map(|p| p.replace("{0}", value))
}

#[cfg(test)]
mod tests {
    use super::*;

    const REPO: &str = r"C:\Dev\DevTools";

    #[test]
    fn serializes_with_camel_case_keys() {
        let mut config = Config::default();
        config.repo_paths.push(r"C:\Dev".to_string());

        let yml = serde_yaml_ng::to_string(&config).unwrap();

        assert!(yml.contains("repoPaths:"));
        assert!(yml.contains("defaultCommand:"));
        assert!(yml.contains("processName: lazygit"));
    }

    #[test]
    fn reads_a_config_written_by_the_dotnet_version() {
        let yml = concat!(
            "version: 1\n",
            r"repoPaths:", "\n",
            r"- C:\Dev", "\n",
            r"favorites:", "\n",
            r"- C:\Dev\DevTools", "\n",
            r"displayNames:", "\n",
            r"  C:\Dev\DevTools: tools", "\n",
            "defaultCommand:\n",
            "  processName: lazygit\n",
            "  workingDirectory: '{0}'\n",
        );

        let config: Config = serde_yaml_ng::from_str(yml).unwrap();

        assert_eq!(config.version, 1);
        assert_eq!(config.repo_paths, vec![r"C:\Dev".to_string()]);
        assert!(config.is_favorite(REPO));
        assert_eq!(config.display_name(REPO), Some("tools"));
        assert_eq!(config.default_command.process_name, "lazygit");
    }

    #[test]
    fn toggling_a_favorite_adds_then_removes_it() {
        let mut config = Config::default();
        config.toggle_favorite(REPO);
        assert!(config.is_favorite(REPO));
        config.toggle_favorite(REPO);
        assert!(!config.is_favorite(REPO));
    }

    #[test]
    fn blank_display_name_removes_the_entry() {
        let mut config = Config::default();
        config.set_display_name("repo", Some("tools"));
        config.set_display_name("repo", Some("   "));
        assert_eq!(config.display_name("repo"), None);
    }

    #[test]
    fn formats_the_path_placeholder() {
        let pattern = Some(r#"-Command "code {0}""#.to_string());
        assert_eq!(
            format_if_some(pattern.as_ref(), REPO),
            Some(r#"-Command "code C:\Dev\DevTools""#.to_string())
        );
        assert_eq!(format_if_some(None, r"C:\Dev"), None);
    }
}
