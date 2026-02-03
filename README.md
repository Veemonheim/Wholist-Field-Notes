# Wholist Field Notes

A fork of Wholist that focuses on *session-based* nearby-player logging, optional marking, and copy-to-clipboard exports for **manual** reporting / note-taking workflows.

> **Status:** Work in progress. This repository began as a fork of Wholist and is being reshaped into “Field Notes” (session cache + marking + export templates + auto-prune). Features mentioned below should be treated as WIP.

---

## What this is

Wholist Field Notes is designed to help you:
- **Scan nearby players during a user-started session**
- **Keep a session log** even as players load/unload while you move around
- **Optionally mark** entries for follow-up
- **Export** a formatted list (or full template block) to your clipboard for *manual* use elsewhere (e.g., a support ticket form you fill out yourself)

This plugin **does not** submit tickets, contact anyone in-game, or perform automated actions. It exists to speed up the boring parts: collecting, deduping, and formatting names.

---

## Guardrails

This fork bakes in guardrails to reduce misuse:
- **User-initiated scanning only** (no silent background scanning on login)
- **No automated reporting** (clipboard export only)
- **No in-game interaction automation** (no tells/emotes/follow/etc.)
- **Local-only storage** (your marked history lives on your machine)
- **Auto-prune** removes entries not seen for **180 days** by default (configurable)

---

## Features

### Session scan
- Press **Start Scan** to begin a session.
- While scanning, the plugin **remembers everyone seen in that session**, even if they later unload or move out of range.
- Each entry tracks:
  - First seen (this session)
  - Last seen (this session)
  - Visible now / not currently loaded (optional UI indicator)

### Marked history
You can mark entries you care about. Marked entries store:
- **First marked** timestamp
- **Last seen** timestamp
- **Times seen** counter
- Optional “exported already” flag (to reduce duplicate reporting)

### Exports
- Copy **names only** 
- Copy **names + world**
- Copy a full **report template block** with placeholders replaced

### Lodestone helper links
Because the client does not provide a Lodestone character ID, the plugin provides a **one-click Lodestone search** link for a character’s name/world (instead of a direct `/lodestone/character/<id>/` URL).

---

## Data & privacy

- Stored locally via Dalamud’s plugin configuration system.
- No external network requests are required for core functionality.
- Auto-prune removes entries not seen for **180 days** by default.

If you want to wipe everything:
- Use the **Reset / Clear History** button in the UI (when implemented), or delete the plugin config file used by Dalamud for this plugin.

---

## Template tokens

The report template supports placeholders (final list may evolve):
- `{{names}}` — selected/filtered names (one per line)
- `{{timestamp_utc}}` — export time (UTC)
- `{{location}}` — location text (if available)
- `{{world}}` — your world/current context (if available)

---

## License & attribution

- This project is a **fork** of Wholist and remains licensed under **AGPL-3.0**.
- If you distribute builds of this plugin, you must make the corresponding source available under the same license terms.

See `LICENSE` for details, and the upstream repository for original authorship and history.

---

## Contributing / feedback

PRs and issue reports are welcome, especially for:
- UX improvements to session scan + marking
- Export formats / template tokens
- Edge cases involving world-visit / name collisions
- Performance improvements in crowded areas

If you’re giving feedback as a tester:
- Please include your Dalamud API level, game region (NA/EU/JP), and a short description of where you tested (e.g., “busy city plaza”).
