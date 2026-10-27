# Changelog

## 1.0.1

- Fixes Check for Updates failing to open with a StartCoroutine
  NullReferenceException after Silverpine destroys the bootstrap runtime host.
- Creates the Unity request runner on demand, checks native-object validity
  before reuse, and recreates it after scene cleanup.
- Keeps cached results in managed process-lifetime state. Interrupted requests
  are aborted/disposed and can be retried without leaving the menu busy.
- Adds lifecycle regression checks including delayed cleanup of an old request.

## 1.0.0

- Optional online addon with Check for Updates in both shared mod menus.
- One public catalog request per check; local version comparison for plugins
  and preloader patcher assemblies, with obsolete replacements and release links.
- Installed/available filters, search, dependency details, unknown identities,
  newer local builds, and explicit stale-result handling after failed checks.
