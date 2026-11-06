# RapidFetch short-form video scripts

Four scripts for TikTok, Instagram Reels, YouTube Shorts and X video. Each is a
single idea, 15–30 seconds, built from the real product. The rendered
[`assets/rapidfetch-teaser-1080x1920.mp4`](assets/rapidfetch-teaser-1080x1920.mp4)
is the first one, ready to post.

## Production rules

- **Real UI only.** Every frame that claims to be RapidFetch is a screenshot or
  recording of RapidFetch. No invented buttons, speeds or dialogs.
- **One idea per video.** Do not turn a 20-second short into a feature list.
- **Frame 0 is the hook.** A bold line or the result, already visible. No black
  frame, no logo intro.
- **Silent-first.** Burn the key words into the picture; most viewers watch
  muted. Add captions to the upload as well.
- **Sound:** keep the UI sound effects in the master. Add trending platform
  audio in the app at upload, at low volume under the voice/SFX. Do not embed
  copyrighted music in the exported file.
- **Safe area:** on 1080×1920, keep all text inside 168–1330 px. Nothing under
  the right-hand action rail. No text in the top 168 px.
- **Caveats in the caption or pinned comment**, not as a wall of text in the
  video: Chrome Web Store pending, unsigned installer, Windows only.
- **No competitor logos, no YouTube/Netflix/Spotify UI, no DRM claims.** Use the
  public-domain Big Buck Bunny demo page and your own files.
- **Disclose the maker** in the caption: "I built this."

## Shared shot list

Assets are already in the repo:

| Shot | Source | Use |
| --- | --- | --- |
| Main window with live segments | `docs/screenshots/main.png` | Hook, speed proof |
| Transfer panel | `docs/screenshots/transfer-clean.png` | Pause/resume proof |
| Media picker | `docs/screenshots/media-bar-demo.png` | Video/HLS proof |
| Scheduler | `docs/screenshots/scheduler.png` | Queue/schedule proof |
| Add download dialog | `docs/screenshots/add-download.png` | Batch/batch URLs |
| End card | `marketing/assets/poster-end.jpg` | CTA |

Re-render the master teaser:

```powershell
cd marketing\video-teaser
npx hyperframes check
npx hyperframes render --quality delivery -o ..\assets\rapidfetch-teaser-1080x1920.mp4
```

---

## Script 1 — "Still paying?" (the rendered teaser)

**Length:** 14.7 s · **Format:** 1080×1920 · **File:**
`assets/rapidfetch-teaser-1080x1920.mp4`

**One idea:** A download manager should not cost $30.

| Time | Picture | Text on screen | Sound |
| --- | --- | --- | --- |
| 0.0–3.2 | Real main window slides up under the hook | STILL PAYING FOR A DOWNLOAD MANAGER? · FREE FOR LIFE | Whoosh on entry |
| 3.2–6.4 | Transfer panel, pause/resume visible | PAUSE. CLOSE. RESUME. · Nothing restarts. Not even after a reboot. | Whoosh cut |
| 6.4–9.6 | Media picker with MP4/WEBM/M4A/HLS chips | VIDEO + HLS. FROM CHROME. | Whoosh cut |
| 9.6–14.7 | Bright end card, icon, wordmark, CTA | FREE IDM ALTERNATIVE · RapidFetch · Free for life. · Windows + Chrome · github.com/sohanur999999/rapidfetch | Chime + click |

**Caption:**

```text
POV: your download manager costs $30 in 2026.

RapidFetch is free for life. Windows + Chrome. 32 segmented connections. Resume after a reboot. No trial, no account, no subscription.

I built it. The Chrome Web Store listing is still pending, so the extension is a one-minute ZIP install for now. Installer is unsigned; checksums are on the release page.

#windows #chrome #freesoftware #downloadmanager #techtok
```

**Pinned comment:**

```text
Free for life means every current core feature: segmented downloads, resume, queues, scheduler, Chrome capture, HLS. No paid tier. Source + checksums: github.com/sohanur999999/rapidfetch
```

---

## Script 2 — "Chrome closed. The download didn't."

**Length:** 20 s · **Format:** 1080×1920 · **Source:** new screen recording

**One idea:** Resume is the feature that saves a 4 GB download.

| Time | Picture | Text on screen | Sound |
| --- | --- | --- | --- |
| 0–2 | Screen recording: a large file at ~60%, then click Pause | CHROME CLOSED. | Click |
| 2–5 | Close the app window; desktop visible | THE DOWNLOAD DIDN'T. | Whoosh |
| 5–8 | Reopen RapidFetch; the item is still paused with the same progress | STILL 60%. | Pop |
| 8–12 | Click Resume; speed and segments come alive | RESUME. NO RESTART. | Click + subtle riser |
| 12–16 | Progress reaches 100%; completion state | ONE FILE. ONE .PART FILE. | Chime |
| 16–20 | End card with URL | RapidFetch — Free for life · Windows + Chrome | Chime tail |

**Recording notes:**

1. Use a large legal test file (for example a public-domain film or a Linux ISO).
2. Let it reach at least 50% before pausing so the progress bar is unambiguous.
3. Close the app completely, not just the window, then reopen.
4. Show the file size and percentage before and after; do not cut around the
   resume click.
5. Record at 1080×1920 or 1440×2560; crop to the app window, not the whole
   desktop, if the desktop has personal files.

**Caption:**

```text
Chrome closed. The download didn't.

RapidFetch keeps a .part file and a segment map, so the transfer resumes after a reboot instead of starting over. Free for life, open source, Windows + Chrome.

I built it. Chrome Web Store listing is pending; extension is a manual ZIP for now.

#windows #chrome #techtok #freesoftware #downloadmanager
```

---

## Script 3 — "One file. 32 connections."

**Length:** 18 s · **Format:** 1080×1920 · **Source:** screen recording + motion

**One idea:** Show the mechanism instead of claiming a speed number.

| Time | Picture | Text on screen | Sound |
| --- | --- | --- | --- |
| 0–2 | Single browser download bar, crawling | ONE CONNECTION. | Low thud |
| 2–5 | Cut to RapidFetch transfer panel with multiple segments | UP TO 32. | Whoosh |
| 5–9 | Animate 8–12 segment bars filling in parallel (use the real panel, highlight existing rows) | EACH PIECE RETRIES ON ITS OWN. | Ticks |
| 9–13 | Show the single-stream fallback in settings or a server without ranges | NO RANGES? ONE STREAM. NO FAKE SPEED. | Click |
| 13–18 | End card | RapidFetch — Free for life · github.com/sohanur999999/rapidfetch | Chime |

**Do not say:** "faster than IDM" or "guaranteed 32× faster." Say: "up to 32
range connections when the server supports them; results vary."

**Caption:**

```text
One file. Up to 32 HTTP range connections.

RapidFetch splits a download into segments, retries them individually, and joins the pieces. Servers without range support fall back to one stream. No fake speed dial.

Free for life, MIT-licensed, Windows + Chrome. I built it.

#windows #chrome #opensource #downloadmanager #techtok
```

---

## Script 4 — "Free for life, not free for a trial."

**Length:** 15 s · **Format:** 1080×1920 · **Source:** static UI + motion

**One idea:** The price contrast, with the boundary stated plainly.

| Time | Picture | Text on screen | Sound |
| --- | --- | --- | --- |
| 0–3 | Split screen: a generic "trial expired" style dialog (do not use a real competitor's UI; build a neutral mock or use text only) vs RapidFetch running | TRIAL EXPIRED. | Error thud |
| 3–6 | RapidFetch main window, all features visible | VS. FREE FOR LIFE. | Whoosh |
| 6–9 | Quick cuts: queue, scheduler, HLS picker, categories | EVERY CORE FEATURE. | Clicks |
| 9–12 | Scroll the free-for-life section of the landing page | NO TRIAL. NO ACCOUNT. NO SUBSCRIPTION. | Pop |
| 12–15 | End card | $0 · Windows + Chrome · github.com/sohanur999999/rapidfetch | Chime |

**Important:** For the "trial expired" side, use a neutral text card, not a real
competitor's dialog or logo. Do not name a competitor in the video.

**Caption:**

```text
"Your trial has expired" is a business model, not a feature.

RapidFetch is free for life: segmented downloads, resume, Chrome capture, HLS, queues, scheduler. No trial clock, account or subscription.

I built it. Windows 10/11 + Chrome. The extension is a manual ZIP until the Web Store listing is approved.

#freesoftware #windows #chrome #downloadmanager #buildinpublic
```

---

## Thumbnails and covers

Use `assets/poster-hook.jpg` for the "Still paying?" video and
`assets/poster-end.jpg` for the end-card video. For the others, export a frame at
the strongest moment and add one short line:

- Script 2: "CHROME CLOSED. IT RESUMED."
- Script 3: "UP TO 32 CONNECTIONS."
- Script 4: "$0. FREE FOR LIFE."

Keep the thumbnail text under six words and readable at 120 px wide.

## Caption block for every upload

```text
RapidFetch — free download manager for Windows 10/11 + Chrome.
Segmented, resumable transfers. Free for life; no trial, account or subscription.
Windows only. The Chrome Web Store listing is pending; the extension is a manual ZIP for now. The installer is unsigned; SHA-256 checksums and source are on GitHub.
Only download content you own or have permission to save. Not affiliated with Tonec/Internet Download Manager.
https://github.com/sohanur999999/rapidfetch
```
