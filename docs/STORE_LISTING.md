# Chrome Web Store Listing Pack

Ready-to-paste copy for the Chrome Web Store dashboard.

## Listing fields

| Field | Value |
| --- | --- |
| Item name | `RapidFetch - Free Download Manager` |
| Summary | `Download files and videos faster with segmented, resumable transfers. Free for life, no trial and no subscription.` |
| Category | Productivity |
| Primary language | English (United States) |
| Homepage URL | `https://github.com/sohanur999999/rapidfetch` |
| Support URL | `https://github.com/sohanur999999/rapidfetch/issues` |
| Privacy policy URL | `https://sohanur999999.github.io/rapidfetch/privacy.html` |
| Developer | RapidFetch |

## Detailed description

```text
RapidFetch is a free download manager for Chrome and Windows. It captures downloads from your browser, accelerates them with up to 32 segmented connections, and resumes interrupted transfers without starting over.

Install the RapidFetch desktop app, then add this extension. The extension connects to the app through a native messaging host or a local localhost bridge. Your files, download history, and settings stay on your computer.

FREE FOR LIFE
No trial. No subscription. No account. Every core download feature is available without payment.

FEATURES
• Capture downloads from Chrome and take over the browser queue
• Download direct files, video, audio, and HLS playlists
• Pause, resume, cancel, and reveal files from the popup
• See live progress, queue status, and active transfers
• Detect media on the page with the floating "Download this video" button
• Choose real formats when the optional yt-dlp engine is installed
• Batch downloads, scheduler, speed limits, categories, and site grabbing in the desktop app
• Segmented transfers with up to 32 connections per file
• Works with Chrome, Edge, Brave, Vivaldi, Opera, and Chromium

HOW IT WORKS
1. Install RapidFetch for Windows from the project page.
2. Add this extension to Chrome.
3. Click the RapidFetch toolbar icon or the floating download button.
4. Choose a format and let the desktop app finish the transfer.

The extension is a companion for the desktop app. It does not download files by itself.

PERMISSIONS
• downloads — capture and manage Chrome downloads
• storage — remember settings and per-tab media state
• contextMenus — add "Download with RapidFetch"
• webRequest — detect media and HLS responses
• tabs — show media for the active tab
• notifications — show progress and completion
• scripting — read media and link URLs when you ask
• nativeMessaging — talk to the RapidFetch app
• host permissions — detect downloadable media on any site

PRIVACY
RapidFetch does not collect, sell, or share browsing history, download history, or personal data. Page media URLs are sent only to the RapidFetch app on your computer. There are no analytics or tracking scripts. Read the full policy: PRIVACY_URL

SUPPORT
Report bugs and request features: SUPPORT_URL

Use RapidFetch only for content you own or have permission to download. Respect the terms of the sites you visit.
```

## Permission justifications

Use these in the **Privacy practices** tab.

### Single purpose

```text
RapidFetch captures downloads and media from the browser and hands them to the
RapidFetch desktop download manager installed by the user.
```

### `downloads`

```text
Captures browser downloads so the desktop manager can take over, and provides
pause, resume, cancel, and reveal controls from the extension popup.
```

### `storage`

```text
Stores user settings and tab-scoped media state locally. Settings may sync
through Chrome sync when the user has it enabled.
```

### `contextMenus`

```text
Adds a "Download with RapidFetch" right-click menu for links, media, and pages.
```

### `webRequest`

```text
Observes media responses and content types to detect downloadable audio, video,
and HLS playlists for the active tab. It does not read request bodies or modify
traffic.
```

### `tabs`

```text
Reads the active tab URL and title, routes commands to the correct tab, and
keeps media state per tab.
```

### `notifications`

```text
Shows download progress and completion notifications with pause and resume
actions.
```

### `scripting`

```text
Reads media and link URLs from the current page when the user opens the picker
or presses the capture command.
```

### `nativeMessaging`

```text
Connects to the companion RapidFetch desktop app through the native messaging
host installed by the user.
```

### Host permission `<all_urls>`

```text
Downloadable media can be served from any domain, so the extension must inspect
the current page and media responses on any site the user visits. It does not
modify page content, inject ads, or change search or new-tab settings.
```

### Remote code

```text
No. All JavaScript is packaged in the extension. The extension does not load or
execute remote code.
```

### Data usage

```text
No personal data is sold or transferred to the developer. Page URLs and media
URLs are processed locally and sent only to the RapidFetch desktop app on the
same computer. No analytics, advertising, or tracking SDKs are included.
```

## Screenshot order

Upload five 1280x800 PNGs from `docs/store-assets`:

1. `01-desktop.png` — fast segmented downloads on Windows
2. `02-browser-capture.png` — real formats in the Chrome panel
3. `03-pause-resume.png` — pause, resume, and finish interrupted transfers
4. `04-queue-scheduler.png` — queue and scheduler controls
5. `05-free-for-life.png` — free build, no trial or subscription

## Promotional assets

| Asset | File | Size |
| --- | --- | --- |
| Store icon | `extension/icons/icon128.png` | 128x128 |
| Small promo tile | `docs/store-assets/promo-440x280.png` | 440x280 |
| Marquee | `docs/store-assets/marquee-1400x560.png` | 1400x560 |
| GitHub social preview | `docs/store-assets/social-preview-1280x640.png` | 1280x640 |

## Claim check

The **Free for life** copy is accurate only while every current core feature
remains free. If a paid Pro tier is added later, change the summary and the
`FREE FOR LIFE` section to **Free core** and describe the paid features
honestly. Do not keep a lifetime claim after introducing a mandatory payment.

Do not use "Number one", "best", "Editor's Choice", or similar claims in the
listing or promotional images. They are not verifiable and can trigger a
metadata rejection.

## Submission checklist

- [ ] Verify the public URLs open.
- [ ] Publish `PRIVACY.md` at a public HTTPS URL.
- [ ] Build the extension zip with `manifest.json` at the root.
- [ ] Verify the zip loads in a clean Chrome profile.
- [ ] Upload five screenshots and the promo tile.
- [ ] Complete every permission justification.
- [ ] Select **No remote code** and **No data sale**.
- [ ] Confirm the privacy policy matches the extension behavior.
- [ ] Test the listing on mobile width.
- [ ] Submit for review.
- [ ] After approval, update README and landing-page links.
