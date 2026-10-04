# Hotbar Bridge working rules

Build a slot-map-based two-way synchronizer for regular and cross hotbars, with individually shared slots and an in-game mapping editor. Preset loading belongs exclusively to the separate Job Setup plugin. Follow the confirmed requirements in README.md and the user's working Gunbreaker export, rather than inferring mappings from the earlier Wrath plans.

The capture has been reviewed and the user has authorized implementing sync. Do not execute game actions; writes are limited to configured assignments following explicit in-game activation. Native pointers must be read on the framework thread after checking that the player and hotbar module are ready. Capture assigned command type/ID, never treat Wrath's transient displayed icon as the assigned action.

No character name, account identifier, content ID, macro text or network upload is needed for this work. Preserve type/ID for macros and unknown slot types without inventing semantics. Keep export snapshots immutable and versioned. State when a field is cached, unavailable or unverified. The plugin is separate from Wrath and must not change Wrath settings as a side effect.

Read-only capture does not mean native reads are immune to game/API changes. Build against installed compatible Dalamud references, document the version, and do not claim runtime validation based on compilation alone.

The Gunbreaker capture defines editable shipped defaults, not permanent mappings. Preserve user configuration during updates. Bridge must function independently of Wrath and Job Setup. Treat job switches, initial load and external bulk layout application distinctly from observed slot edits. Never silently resolve ambiguous two-sided edits.

The first Gunbreaker export was reviewed on 2026-09-26. The user explicitly says the cross layout is not set up: regular hotbars are authoritative and the stated controller map determines cross destinations. Existing cross contents must not be treated as binding layout requirements or as conflicts needing a user decision.

A locally hashed character key may partition runtime sync state and backups to avoid cross-character leakage; it is never part of captures or repository content.

Utility defaults use cross hotbar 8, preserving regular hotbar 10 plus hotbar 9 slots 1–4. Migrate only the complete old default group and pause sync for reviewed regular-to-cross initialization; preserve custom mappings. Offer explicit old-page cleanup with backup, clearing only unmapped cross hotbar 2 duplicates confirmed against both source and new destination. Keep current/all scope and native shared storage semantics explicit.
