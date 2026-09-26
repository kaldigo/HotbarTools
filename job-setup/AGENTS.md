# Job Setup working rules

This is the separate preset-application plugin, not the hotbar sync engine. Follow README.md for the agreed product scope. Provide independent hotbar and Wrath application for the current class/job or all supported classes/jobs. Keep it distributable and usable without Hotbar Bridge.

Use the curated per-job source under ../wrath and its accepted constraints. Do not distribute personal settings indiscriminately, embed machine paths, silently overwrite other jobs during current-job operations, or invent Wrath runtime APIs. Verify action placement and configuration integration against installed compatible APIs. Stage and preview bulk writes, preserve backups, and report skipped/failed work accurately.

The plugin is implemented as an initial test release. Do not claim in-game validation from a successful build alone. Wrath apply and restore must be blocked whenever Wrath is enabled, including a final pre-write check. Base classes inherit corresponding job layouts by user decision; crafting and gathering presets still need assessment.
