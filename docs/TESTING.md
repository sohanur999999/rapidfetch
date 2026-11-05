# RapidFetch test report

Run on Windows 11 with .NET SDK 10 and Node.js 22.

## Engine smoke suite

```powershell
dotnet run --project tools\EngineSmoke\EngineSmoke.csproj -c Release
```

The suite starts a local HTTP server with range, no-range and HLS endpoints and
verifies the finished files by SHA-256.

| Test | Result |
| --- | --- |
| Direct download, 8 connections, segmented | PASS |
| Pause retains progress and resume does not restart from zero | PASS |
| Restart restores segments and finishes correctly | PASS |
| Concurrency limit never runs two transfers at once | PASS |
| Server without ranges falls back to one stream | PASS |
| HLS master/variant playlists and segment joining | PASS |
| yt-dlp URL routing for YouTube, Facebook watch/videos/reels/shares and fb.watch | PASS |
| Facebook/photo/home URLs stay on the direct-file engine | PASS |
| Long dotted titles truncate to 180 characters without an exception | PASS |

Current output ends with `ALL TESTS PASSED`.

## Browser extension

All extension scripts pass `node --check`. The local harness stubs the extension
runtime and serves the real `content.js` and `popup.js`:

```powershell
node tools\extension-harness\server.js 6123
```

- Floating panel: shows 3 detected items, numbered badge, Download buttons,
  Download all, Open RapidFetch, settings broadcast and Sent/toast state. PASS.
- Popup: shows 3 supported items, filters `.mpd`, `.m4s` and googlevideo URLs,
  renders file sizes, Download/Sent buttons, and the Downloads pane with
  pause/resume/reveal/remove actions. PASS.
- `floatingButton` off hides the page panel immediately; turning it on restores
  it. PASS.
- A live Facebook reel (`https://web.facebook.com/reel/2147944719462588`) shows
  four real formats in the floating panel: 1080p, 720p, 540p and audio only.
  Choosing 1080p queued and completed a 13.7 MB MP4 through the desktop app. The
  old list of tiny player fragments no longer appears on yt-dlp pages. PASS.
- Reloading the unpacked extension while a YouTube Short was open removed the
  stale floating panel on its next request instead of showing **Extension context
  invalidated** or a broken **Retry** action. A freshly loaded page then showed
  the full format list with no error. PASS.

## Floating panels and completion popup

UI Automation drives the debug build with the main window minimized:

- A new download auto-shows `RapidFetch transfers` with the live speed graph.
- **Pause transfer** holds the transfer, **Resume transfer** continues from the
  saved segments, and **Cancel transfer** stops it. A canceled transfer does not
  open the completion popup. PASS.
- When the queue goes idle, `RapidFetch transfers` is hidden and
  `RapidFetch download complete` appears bottom-right. It reports a 400 x 203 px
  window at 125% DPI (320 x 162 DIP) with three 28 DIP (35 px) buttons:
  **Open**, **Open folder** and **Close**. The window is borderless and
  translucent: no rectangular backdrop or outline remains, the card's rounded
  corners reveal the page behind it, and the neutral glass keeps the text
  readable over both light and dark content. PASS.
- **Open folder** reveals the file and hides the popup; **Close** hides it
  without opening anything; starting a new transfer hides the completion popup
  and brings back the transfer panel. PASS.

## Browser capture dedupe

The bridge is tested directly with `source: "capture"`:

- A completed capture URL is answered with `duplicate: true`,
  `reason: "completed"` and the existing item id; the download list count does
  not change. PASS.
- A capture whose URL redirected to a different final address is still matched
  through the stored `originalUrl`. PASS.
- An in-progress capture URL is answered with `duplicate: true`,
  `reason: "active"`. PASS (same code path).
- The extension ignores `chrome.downloads.onCreated` events whose `startTime` is
  older than two minutes, remembers captured URLs for ten minutes, and tags
  automatic takeovers with `source: "capture"`. Together with the bridge check
  this stops Chrome from re-adding its download history after a restart. PASS.

## Download manager layout and sorting

UI Automation drives the debug build at 125% DPI (`test-ab-layout.ps1`):

- The **File** menu opens with New Download, Open Download Folder, Settings and
  Exit. **Tasks**, **Tools** and **Help** use the same menu-bar pattern. PASS.
- The labeled toolbar exposes New Download, Resume, Pause, Start Queue, Stop
  Queue, Queues, Stop All, Delete and Settings, each with an accessibility name
  and no clipping. PASS.
- The sidebar tree shows All downloads with its category children (Compressed,
  Documents, Music, Video, Programs, Images, General), Finished, Unfinished and
  Queues > Main. Counts match the saved download state. The compact tree rows
  keep all 12 entries visible with no vertical scrollbar at 900 DIP. PASS.
- Clicking a column header sorts both ways: **Name** toggles A-Z/Z-A, **Size**
  toggles largest/smallest, and **Status**, **Speed**, **Time Left** and
  **Date Added** toggle as well. The active sort is written to `settings.json`
  and restored after restart; the default is newest first. PASS.
- The header checkbox selects every visible row and a second toggle clears them.
  It also shows a partial state when only some rows are checked. The header and
  row boxes use the same 18x18 template and centered cell; their centers match
  within 0 px at default width and at 900 DIP. The 28x28 status tile renders its
  full 35 px (at 125% DPI) glyph without clipping. PASS.
- **Stop Queue** pauses waiting items and changes the status bar to
  **Queue stopped**; **Start Queue** resumes them and restores **Queue running**.
  PASS.
- Typing `telegram` in the search box returns six URL-named fallback rows; every
  visible row contains the query, and selecting one opens its details. PASS.
- Selecting a row opens the details panel below the list with name, category,
  status, progress, size, source, save path, thread/segment count and quick
  actions. The X clears the selection and hides the panel. PASS.
- The Delete confirmation lists the selected count and keeps downloaded files by
  default. **Also delete files from storage** changes the action to
  **Delete files and remove** and permanently deletes the files on disk. Cancel
  leaves both the entries and files untouched. Verified with temporary local
  downloads: removing without the option deleted the record and kept the file;
  removing with the option deleted both. PASS.
- At 1220 DIP all seven columns are visible: Name keeps about 406 DIP, Size about
  136 DIP, Status 108 DIP, Speed 78 DIP and Time Left 78 DIP. Date Added remains
  visible, and no header or toolbar button leaves the window. PASS.
- At the 900 DIP minimum Date Added hides, Name keeps about 198 DIP, and Size,
  Status, Speed and Time Left keep their full widths (no compressed columns).
  The menu bar, all nine toolbar buttons, search and sidebar fit without
  clipping or a sidebar scrollbar. Opening details at 900 DIP uses a compact
  bottom panel; source and path truncate with an ellipsis but the action buttons
  stay visible. PASS.
- Completed rows hide the progress bar; active rows keep the percentage and bar.
  Failed captures with an empty file name show a URL-derived label in both the
  list and the details panel. PASS.

## Theme and visual checks

UI Automation and `PrintWindow` capture the real windows at 125% DPI. The final
screenshots are in `docs/screenshots`.

- The default dark theme uses a near-black cool base, low-saturation aurora
  light, smoked translucent cards, white specular rims, 10-18 px radii and the
  iOS blue accent. The main window, sidebar, list, search pill and details panel
  all use the same material without losing text contrast. PASS.
- The status icon tile keeps its 28x28 bounds and a 1 px tinted border at both
  default width and 900 DIP; no glyph or edge is clipped. PASS.
- The Options window renders **Appearance** and **Behaviour** headings in the
  text color (not the default black) and remains readable over the dark glass
  navigation pane. PASS.
- Add Download, Options, Scheduler, Site Grabber, Setup, the floating transfer
  panel and the completion popup were captured under the dark theme. The graph
  well in the transfer panel is a translucent inset rather than a black block,
  and the compact glass completion popup keeps Open / Open folder / Close
  readable without a border or rectangular backdrop. PASS.
- Switching between dark and light through the theme button still rebuilds the
  resource dictionary and keeps the window readable; the settings file is left
  on `theme=dark`, `accent=#0A84FF`, `sortOrder=newest`. PASS.

## Setup executable

```powershell
.\build-setup.ps1
.\RapidFetch-Setup-1.0.0.exe --silent --install-dir "$env:TEMP\RapidFetchSetupTest" `
  --no-desktop --no-shell --no-startup --no-launch
```

Verified:

- exit code 0 and installed binary is byte-identical to the setup executable
- `extension`, `host`, `docs`, `README.md` and `uninstall.ps1` are present
- native host manifest and all Chromium browser registry keys point at the
  installed executable
- Apps & Features entry contains the correct location and uninstall command
- installed app starts, answers `GET /ping`, downloads a local file through
  `POST /add`, and removes it through `POST /remove`
- `uninstall.ps1` removes files, shortcuts, host keys and the uninstall entry
  while keeping `%APPDATA%\RapidFetch`

GUI setup is exercised by `tools\setup-gui-test.ps1`, which uses UI Automation to
set the folder, disable the desktop shortcut, press Install, wait for the success
page and verify every installed file and registry entry. PASS.

The rebuilt setup executable was re-tested after the remove-downloads dialog
change. The GUI install passed in `%TEMP%\RapidFetchSetupGuiTest`, and a silent
install in `%TEMP%\RapidFetchSetupSilentTest` exited 0 with a byte-identical
executable, the expected files, native-host and Apps & Features entries. The
installed app answered `GET /ping`, downloaded and verified a local file through
`POST /add`, removed it through `POST /remove`, and kept the 63-record history
intact. `uninstall.ps1` then removed the program folder, host keys and uninstall
entry while keeping `%APPDATA%\RapidFetch`. The real per-user install was
restored afterwards. PASS.

## Release artifacts

| Artifact | Contents |
| --- | --- |
| `RapidFetch-1.0.0-win-x64.zip` | folder build, extension, host, docs, install and uninstall scripts |
| `RapidFetch-1.0.0-standalone-win-x64.exe` | one self-contained executable, embedded extension and host identity |
| `RapidFetch-Setup-1.0.0.exe` | GUI/silent per-user installer, same self-contained binary |

Each executable and zip has a matching `.sha256` file.

## Known limits

- DASH `.mpd`, fragmented `.m4s` and YouTube googlevideo streams are filtered
  because the engine does not mux separate audio and video tracks.
- The browser extension is loaded with **Load unpacked**; no Web Store package is
  published.
- Explorer shell integration and run-at-startup are off by default in the app.
  The installers register them because they are explicit setup options.
