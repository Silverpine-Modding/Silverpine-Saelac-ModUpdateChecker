# Mod Update Checker

Optional BepInEx addon for Silverpine 1.7.3, version **1.0.0**.
Created by **Saelac and ChatGPT**.

[Download](https://github.com/Silverpine-Modding/Silverpine-Saelac-ModUpdateChecker/releases/latest)

Adds **Check for Updates** to the main menu's **Modding Tools** and the
inventory's **Mods** tab. Shows installed and latest published versions,
update availability, newer local builds, obsolete replacements, and release
pages. Search the list, filter updates/obsolete entries, or show available
mods that are not installed. Select a mod name for component and dependency
details.

## Optional online addon

This is a separate plugin. **Modding Tools does not depend on it, bundle it,
or contact GitHub to check updates.** Leave this DLL out if you do not want
the update-checking feature.

There are no startup checks, timers that fetch updates, telemetry, credentials,
or uploads. Opening **Check for Updates** downloads the catalog on first use;
subsequent requests require **Check now**. Reopening uses the last successful
in-memory result. Each check makes one HTTPS GET to:

https://raw.githubusercontent.com/Silverpine-Modding/.github/main/versions.json

Installed mod names, versions, file paths and game/save data remain local.
GitHub receives the normal connection metadata (such as your IP address).
The request has a 25-second timeout, a 2 MiB response limit, and a 30-second
recheck cooldown. Redirects are disabled; no authentication is needed.
Release/replacement buttons open GitHub in your browser only when clicked.

The plugin **checks and links**; it does not download or replace mod DLLs.
Close Silverpine before installing an update. Keep only one copy of each DLL.

## Installation

1. Install BepInEx 5 and [Modding Tools 1.11.0 or newer](https://github.com/Silverpine-Modding/Silverpine-Saelac-Modding-Tools/releases/latest),
   including its shared Newtonsoft.Json.dll.
2. Extract the release ZIP into the game directory. It contains
   BepInEx/plugins/ModUpdateChecker/ModUpdateChecker.dll and this README.
3. Start Silverpine and choose **Modding Tools → Check for Updates**.

To remove the checker, close the game and remove its ModUpdateChecker folder.
The framework and other mods continue to work.

## What is checked

- Loaded BepInEx plugin GUIDs and their runtime versions.
- Installed DLL assembly identities/versions under BepInEx/patchers, including
  subfolders. These describe disk contents, not proof a patcher ran; restart
  after changing files.
- The current and legacy Modding Tools GUIDs share a single repository row.
- Latest **stable published releases**, using actual DLL metadata rather than
  source-tree version numbers or alphabetical tag comparison.
- Both obsolete item fixes point to Salt Extra Debug and explain removal.
- Missing catalog identities say **Not in catalog**, never **Up to date**.
- A newer local build is identified without offering a downgrade.
- Latest-release hard dependency requirements appear in the detail pane.

Disabled/unloaded plugin DLLs are not scanned. Patcher support libraries may
appear as unknown assembly entries. Standalone applications such as SILC
Studio are excluded. A failed check retains clearly labeled previous results;
the plugin does not persist a disk cache. The catalog refreshes on GitHub
every six hours and can be refreshed manually by a maintainer, so a just-published
release may take time to appear (including GitHub's raw-content cache).

## Build and checks

From a clone, install the framework and pass the game directory:

    dotnet build ModUpdateChecker.csproj -c Release -p:SilverpineGameDir="C:\Games\Silverpine"
    dotnet run --project Tests/Tests.csproj -c Release -p:SilverpineGameDir="C:\Games\Silverpine"

ModdingToolsPath can override the framework reference. Under the game's
Plugin Development folder, the adjacent framework Release build is preferred.
Tests cover comparison, aliases, dependencies, obsolete/unknown/duplicate
identities, and malformed/untrusted catalog data. Pass a local versions.json
path after -- to validate the real catalog too.

In-game smoke checks: open from the main menu, close with Escape, enter a
save and open from Mods, scroll/search, inspect an obsolete/unknown entry,
disconnect and retry, and confirm the previous result is marked outdated.
Native UI interaction still requires an in-game check.
