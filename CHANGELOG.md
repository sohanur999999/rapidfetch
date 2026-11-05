# Changelog

All notable changes to RapidFetch are documented here.

## [1.0.0] - 2026-11-05

### Added

- Segmented HTTP downloads with up to 32 connections per file, pause/resume,
  automatic retry, and `.part` recovery.
- Download queue, concurrency limit, scheduler, daily transfer cap, and
  completion actions.
- Chromium browser extension with automatic download capture, context menus,
  media detection, a floating **Download this video** panel, and queue controls.
- Native messaging host and localhost bridge with a fixed extension ID.
- Direct media and HLS detection, live transfer panel, completion popup, tray
  integration, categories, search, CSV export, link import, and backup/restore.
- Optional yt-dlp integration for supported video pages, including format
  selection, playlists, browser cookies, and ffmpeg merging.
- Facebook watch pages, reels, page videos, share links, `fb.watch`, and
  `video.php` routing through yt-dlp.
- Long-title filename truncation and stale-extension-context recovery.
- Self-contained Windows installer with a GUI, silent mode, per-user install,
  shortcuts, uninstall entry, and browser native-host registration.
- Free build: no trial, subscription, account, or activation server.

### Fixed

- Facebook player fragments are no longer offered as complete downloads.
- Long Facebook captions no longer stop at **Connecting** because of an invalid
  filename length.
- Reloading the extension no longer leaves a dead **Extension context
  invalidated** overlay with a broken **Retry** button.
