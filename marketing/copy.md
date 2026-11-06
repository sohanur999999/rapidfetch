# RapidFetch launch copy

Ready-to-paste copy. Replace the date and any bracketed text before posting.
Every post must disclose that you are the maker when the platform or community
expects it.

Canonical URL:

```
https://github.com/sohanur999999/rapidfetch
```

Landing page with UTM:

```
https://sohanur999999.github.io/rapidfetch/?utm_source=SOURCE&utm_medium=CHANNEL&utm_campaign=launch_v1
```

## Boilerplate

### 25 words

> RapidFetch is a free, open-source download manager for Windows 10/11 with a
> native Chrome extension. Segmented, resumable transfers. No trial, account or
> subscription.

### 50 words

> RapidFetch is a free download manager for Windows 10/11 with a native Chrome
> extension. It uses up to 32 HTTP range connections per file, resumes
> interrupted transfers, captures video and HLS, and includes queues and a
> scheduler. MIT-licensed, local-only, and free for life — no trial, account or
> subscription.

### 100 words

> RapidFetch is a free, open-source download manager for Windows 10/11 with a
> native Chrome extension. It takes over browser downloads and accelerates them
> with up to 32 segmented HTTP range connections, then resumes from a `.part`
> file and segment map if the app closes or the machine reboots. It also captures
> direct video, audio and HLS playlists from Chrome, and includes batch queues,
> scheduling, speed limits, categories and optional yt-dlp support. There is no
> trial clock, account, subscription, telemetry or activation server. The code is
> MIT-licensed and the Chrome Web Store listing is pending, so the extension
> currently installs from an official ZIP.

## X / Twitter

### Launch thread (8 posts)

**1/8**

```
IDM costs about $30. RapidFetch is $0.

A free, open-source download manager for Windows + Chrome.
32 segmented connections. Resume after a reboot. HLS capture.

No trial. No account. No subscription.

I built it because the paid-download-manager era should be over. 🧵
```

**2/8**

```
Chrome gives a file one connection. RapidFetch opens up to 32 HTTP range connections and writes the pieces into one file.

If the server does not support ranges, it falls back to a single stream instead of pretending. No fake speed dial.
```

**3/8**

```
The part I use most: pause, close the app, reboot, reopen.

The .part file and segment map are still there, so the download continues from where it stopped.

Not a demo mode. The real engine.
```

**4/8**

```
It also catches video + HLS from Chrome. Pick a format, hand it to the desktop app, and keep the file local.

Only download what you own or have permission to save. No DRM nonsense.
```

**5/8**

```
The awkward part: the Chrome Web Store listing is pending because the $5 developer fee is unpaid.

Until it is live, the extension is a one-minute manual ZIP install. I would rather say that than post a dead store link.
```

**6/8**

```
Free for life means the current core feature set: segmented downloads, resume, queue, scheduler, Chrome capture, HLS, categories, speed limits, backups.

If that ever changes, the README and landing page say so before anything is paywalled.
```

**7/8**

```
It is MIT-licensed, local-only, and has no analytics, telemetry or account.

Windows 10/11, 64-bit. The installer is unsigned for now, so SmartScreen may warn; checksums and source are on the release page.
```

**8/8**

```
Try it, break it, tell me what is missing:

https://github.com/sohanur999999/rapidfetch

If you know someone still paying for a download manager, send them this thread.
```

### Single-post variants

**Price contrast**

```
IDM costs about $30. This is $0.

Not a trial. Not a subscription. A free download manager for Windows + Chrome, with 32 segmented connections and resume.

Open source. No account. No telemetry.

https://github.com/sohanur999999/rapidfetch
```

**Resume**

```
Chrome closed. The download didn't die.

RapidFetch keeps a .part file and a segment map, so a 4 GB transfer resumes after a reboot instead of starting over.

Free for life, MIT-licensed, Windows + Chrome.

https://github.com/sohanur999999/rapidfetch
```

**Mechanism**

```
One file. Up to 32 HTTP range connections.

RapidFetch splits a download into segments, retries them individually, and joins the pieces into one file. Servers without range support fall back to a single stream.

Free, open source, no account.

https://github.com/sohanur999999/rapidfetch
```

**Builder story**

```
I got tired of "your trial has expired" so I shipped a free alternative.

RapidFetch: segmented downloads, resume, HLS capture, queue, scheduler. Windows + Chrome. MIT. No telemetry.

The Chrome store fee is still unpaid, so the extension is a manual ZIP for now.

https://github.com/sohanur999999/rapidfetch
```

**Underdog / transparency (use sparingly)**

```
The Chrome Web Store wants $5 to list an extension.

Until I pay it, RapidFetch ships as a one-minute manual ZIP. The app is free for life and the source is public. The store listing will replace the ZIP when it is approved, not before.

https://github.com/sohanur999999/rapidfetch
```

### Reply bank

Use these in replies; do not post them as a wall of text.

- **"Is it really free?"** — "Yes. No trial clock, account, subscription or activation server. The README defines the promise: every current core feature is free, and if that ever changes the page will say so before anything is paywalled."
- **"How is it faster?"** — "It can use up to 32 HTTP range connections per file. That often helps on servers that support ranges, but results depend on the server and your connection. It falls back to one stream when ranges are not supported."
- **"Is it safe? SmartScreen warned me."** — "The installer is not code-signed yet, so Windows shows Unknown publisher. The source is MIT-licensed, the release page has SHA-256 checksums, and you can inspect or build it yourself. Don't bypass a warning from a source you don't trust."
- **"Why isn't it on the Chrome Web Store?"** — "The listing is pending because the developer registration fee is unpaid. The extension ZIP is the same package prepared for the store. I won't post a store link until it is live."
- **"Does it work on Mac/Linux?"** — "No. The desktop app is Windows 10/11, 64-bit. The Chrome extension is a companion and needs the desktop app."
- **"Can it download YouTube?"** — "It supports direct media and HLS, plus optional yt-dlp for pages it can resolve. Only download content you own or have permission to save, and respect the site's terms. It does not bypass DRM."
- **"Is it open source?"** — "Yes, MIT. The core engine, WPF app and MV3 extension are in the repo."
- **"Does it phone home?"** — "No analytics, telemetry or ad SDK. The extension talks to the desktop app on localhost/native messaging. Settings and history stay in %APPDATA%\\RapidFetch."
- **"Can I use it at work?"** — "The MIT license allows commercial use. Check your organization's software policy first; the installer is per-user and does not need admin rights."
- **"Why manual ZIP?"** — "Chrome's Load unpacked flow, one time. Extract the ZIP, open chrome://extensions, enable Developer mode, select the folder with manifest.json. A developer-mode reminder is normal."
- **"Will you add a paid tier?"** — "The current core feature set is free for life. If a paid tier is ever added, the free core stays and the page will say so first. No bait-and-switch."
- **"Does it support batch/scheduling?"** — "Yes: multi-URL add, queues, concurrency limits, a daily transfer cap, a time window with midnight wrap, and finish actions (exit/sleep/hibernate/shutdown)."

## LinkedIn

### Long post

```
I shipped a free download manager because the paid ones still act like it is 2008.

RapidFetch is a Windows 10/11 app with a native Chrome extension. It does the things people expect from a paid download manager — segmented transfers, resume, queues, scheduling, browser capture, HLS — without a trial clock, account or subscription.

The part I care about most is resume. Close the app, reboot, reopen. The .part file and segment map are still there, so the transfer continues. That should be table stakes. It usually is not.

It is MIT-licensed, local-only, and has no analytics or telemetry. The Chrome Web Store listing is still pending, so the extension currently installs from an official ZIP with a one-minute manual flow. I would rather be transparent about that than post a dead store link.

If you try it, I want the unglamorous feedback: install friction, SmartScreen warnings, weird servers, edge cases. That is what makes a downloader good.

https://github.com/sohanur999999/rapidfetch

#Windows #Chrome #OpenSource #FreeSoftware
```

### Short post

```
Most download managers sell you a trial timer, then a subscription.

RapidFetch is free for life: segmented downloads, resume, HLS capture, queues and scheduling on Windows + Chrome. MIT-licensed, local-only, no account.

The Chrome store listing is pending, so the extension is a manual ZIP for now. Feedback welcome — especially install friction.

https://github.com/sohanur999999/rapidfetch
```

## Hacker News (Show HN)

**Title (64 characters):**

```
Show HN: RapidFetch – free IDM alternative for Windows and Chrome
```

**Body:**

```
Hi HN — I built RapidFetch, a free, MIT-licensed download manager for Windows 10/11 with a native Manifest V3 Chrome extension.

The short version: it probes a URL with a one-byte Range request, and if the server returns 206 it splits the file into up to 32 byte ranges. Each segment has its own retry loop and writes into a preallocated .part file; a segment map lets the transfer resume after the app closes or the machine reboots. If the server ignores Range, it falls back to a single stream instead of pretending to accelerate.

The browser side is an MV3 extension that captures chrome.downloads, links, media and context-menu actions. It talks to the desktop app through a native messaging host, with a localhost HTTP bridge as a fallback. Media detection handles direct audio/video and HLS playlists; optional yt-dlp handles pages whose tracks need muxing. It does not bypass DRM, and DASH/separate-track YouTube streams are intentionally filtered from the built-in grabber.

Stack: .NET 10 core engine, WPF shell, MV3 extension. No account, no telemetry, no activation server. Settings and history stay in %APPDATA%\RapidFetch. The code is MIT.

Two honest caveats:
1. The Chrome Web Store listing is pending because the $5 developer fee is unpaid, so the extension currently installs from an official ZIP.
2. The installer is unsigned, so SmartScreen may warn. The release page has SHA-256 checksums and the source is public.

I am mostly interested in feedback on install friction, weird HTTP servers, resume edge cases and browser capture behavior. What would you test first?

https://github.com/sohanur999999/rapidfetch
```

**HN reply notes:**

- Do not argue about the name "IDM alternative"; explain it is descriptive.
- If someone asks for benchmarks, say you have not published one and explain the variables (server range support, CDN, connection cap, disk).
- If someone says "just use aria2", acknowledge it is a good tool; RapidFetch is a GUI/browser-integration alternative, not a replacement for every CLI workflow.
- If someone flags the CWS fee, do not beg; explain the manual ZIP and that the store link will replace it when approved.

## Reddit

Read the sidebar and recent mod posts first. If self-promotion is restricted,
use the weekly thread or ask the mods. Do not cross-post the same text.

### r/software

**Title:**

```
I built a free, open-source IDM alternative for Windows + Chrome — feedback wanted
```

**Body:**

```
I got tired of download managers that gate resume and queues behind a trial timer, so I built RapidFetch: a free, MIT-licensed download manager for Windows 10/11 with a native Chrome extension.

What it does:
- Up to 32 HTTP range connections per file, with single-stream fallback for servers that don't support ranges
- Resume from a .part file + segment map after closing the app or rebooting
- Captures downloads, links, video and HLS from Chrome
- Batch queues, scheduler, speed limits, categories, site grabber
- Optional yt-dlp for pages whose tracks need muxing
- No account, telemetry, subscription or activation server

Caveats, because they matter:
- The Chrome Web Store listing is pending (the $5 developer fee is unpaid), so the extension is a manual ZIP for now. Exact steps are in the README.
- The installer is unsigned, so SmartScreen may warn. Checksums and source are on the release page.
- Windows only. It does not bypass DRM, and it intentionally filters DASH/separate-track streams from the built-in grabber.

I'm the maker. I'm looking for install friction and edge-case feedback, not upvotes. If the mods prefer this in a weekly thread, happy to move it.

https://github.com/sohanur999999/rapidfetch
```

### r/windows

**Title:**

```
Free download manager for Windows 10/11 — would love install feedback
```

**Body:**

```
I built a free download manager for Windows 10/11 and a Chrome extension to go with it. It is called RapidFetch.

The reason: I wanted segmented downloads, resume and a queue without a trial timer or an account. It uses up to 32 HTTP range connections per file, resumes from a .part file after a reboot, and captures downloads/video/HLS from Chrome. There is a scheduler, speed limits, categories and a site grabber.

It is free for life and MIT-licensed. No telemetry, no account, no activation server.

Two things to know before you try it:
1. The Chrome Web Store listing is pending, so the extension installs from a ZIP via chrome://extensions → Developer mode → Load unpacked.
2. The installer is unsigned, so SmartScreen may show "Unknown publisher." The release page has SHA-256 checksums and the source is public.

I'm the maker. I'd appreciate feedback on the installer, SmartScreen, and whether the UI makes sense on a 1080p or 4K display.

https://github.com/sohanur999999/rapidfetch
```

### r/chrome_extensions

**Title:**

```
Free Chrome download/media extension (manual ZIP while the Web Store listing is pending)
```

**Body:**

```
I built a Manifest V3 extension that captures Chrome downloads, links, media and context-menu actions and hands them to a free Windows download manager. The desktop app does the segmented/resumable transfer; the extension is the browser bridge.

Why the manual ZIP: the Chrome Web Store listing is still pending because the developer registration fee is unpaid. I would rather ship the official ZIP than a dead store link. It is the same package prepared for the store.

Install:
1. Install the Windows app first (it registers the native messaging host).
2. Download the extension ZIP from the release page.
3. Extract it to a permanent folder.
4. Open chrome://extensions, enable Developer mode, click Load unpacked, select the folder with manifest.json.
5. Pin it and test a download.

Permissions: downloads, storage, contextMenus, webRequest, tabs, notifications, scripting, nativeMessaging and host permissions for media detection. The extension does not inject ads, change search/new-tab, or send browsing data to a server. It talks to the desktop app on the same machine.

I'm the maker. Feedback on the install flow and permission prompts is especially useful.

https://github.com/sohanur999999/rapidfetch
```

### r/opensource

**Title:**

```
RapidFetch: MIT-licensed download manager for Windows with a Chrome extension
```

**Body:**

```
I open-sourced RapidFetch, a download manager for Windows 10/11 with an MV3 Chrome extension. MIT license, no telemetry, no account.

The engine is .NET 10: a Range probe, up to 32 byte-range segments, per-segment retry, a preallocated .part file and a segment map for restart-safe resume. Servers that ignore Range fall back to a single stream. The WPF shell handles queues, scheduling, categories, speed limits and a site grabber. The extension uses native messaging with a localhost HTTP bridge fallback.

It also captures direct media and HLS; optional yt-dlp handles pages whose audio/video tracks need muxing. It does not bypass DRM, and DASH/separate-track streams are filtered from the built-in grabber by design.

Caveat: the Chrome Web Store listing is pending, so the extension is a manual ZIP for now. The installer is unsigned; checksums and source are public.

I'm the maker. I would value code review, edge cases and install feedback more than stars.

https://github.com/sohanur999999/rapidfetch
```

### r/freesoftware

**Title:**

```
RapidFetch — free for life download manager for Windows + Chrome (MIT)
```

**Body:**

```
Freeware with a clear promise: every current core feature is free, with no trial, account or subscription. RapidFetch is a Windows 10/11 download manager with a Chrome extension.

- Segmented downloads (up to 32 range connections per file)
- Resume after closing the app or rebooting
- Chrome download/link/media/HLS capture
- Queues, scheduler, speed limits, categories
- Optional yt-dlp support
- MIT license, local-only data, no telemetry

The Chrome Web Store listing is pending, so the extension is a manual ZIP for now. The installer is unsigned, so SmartScreen may warn; checksums and source are on the release page.

I'm the maker. Feedback welcome.

https://github.com/sohanur999999/rapidfetch
```

### r/DataHoarder

Only post if the rules allow it. Do not mention piracy, paid content or DRM.
Focus on legal archives, queues and HLS you own.

**Title:**

```
Free download manager with queue, scheduler and HLS capture for legal archives
```

**Body:**

```
I built RapidFetch, a free Windows download manager with a Chrome extension. It is aimed at large legal archives: batch URLs, queues, a scheduler, categories, speed limits and resume after a reboot.

It handles direct files and HLS playlists, and optional yt-dlp for pages you have the right to save. It does not bypass DRM, and it intentionally filters DASH/separate-track streams from the built-in grabber.

MIT-licensed, no account, no telemetry. The Chrome store listing is pending, so the extension is a manual ZIP for now; the installer is unsigned and checksums are on the release page.

I'm the maker. If you hoard public-domain films, course material you own, or your own recordings, I'd like feedback on queue/scheduler behavior and HLS edge cases.

https://github.com/sohanur999999/rapidfetch
```

## Product Hunt

**Name:** RapidFetch

**Tagline (43 characters):**

```
Free IDM alternative for Windows and Chrome
```

**Description (under 260 characters):**

```
RapidFetch is a free download manager for Windows with a native Chrome extension. Segmented transfers, resume, HLS capture, queues and scheduling. Free for life — no trial, account or subscription. MIT-licensed and local-only.
```

**Topics:** Productivity, Developer Tools, Open Source, Windows, Chrome Extensions

**Maker comment:**

```
Hi Product Hunt — I built RapidFetch because I wanted a download manager with segmented transfers, resume and a queue without a trial timer or an account.

It is a Windows 10/11 app plus a native Chrome extension. Up to 32 HTTP range connections per file, restart-safe resume from a .part file, Chrome download/media/HLS capture, queues, scheduler, speed limits and categories. MIT-licensed, no telemetry, no activation server.

Two honest caveats: the Chrome Web Store listing is pending (the $5 developer fee is unpaid), so the extension is a one-minute manual ZIP for now. The installer is unsigned, so SmartScreen may warn; checksums and source are public.

I would love feedback on install friction and the first-run experience. What would make you switch from your current download manager?
```

**First comment:**

```
What it is: a free, open-source download manager for Windows 10/11 with a Chrome extension.
What it is not: a DRM bypass, a Mac/Linux app, or a Chrome Web Store listing (yet).
How to try it: install the Windows app, then load the official extension ZIP from chrome://extensions → Developer mode → Load unpacked.
Free-for-life boundary: every current core feature is free. If that ever changes, the README and landing page will say so before anything is paywalled.
```

**Gallery captions:**

1. "32 segmented connections per file — real UI, real progress."
2. "Pause, close, reboot, resume. The .part file and segment map survive."
3. "Capture video and HLS from Chrome, then pick a format."
4. "Queue it, schedule it, cap it, forget it."
5. "Free for life. No trial. No account. No subscription."

## TikTok / Reels / Shorts

The rendered teaser is silent except for UI sound effects. Add a trending
platform track in the app at upload; do not add copyrighted music to the master
file.

### Caption A — price hook

```
POV: your download manager costs $30 in 2026.

RapidFetch is free for life. Windows + Chrome. 32 segmented connections. Resume after a reboot. No trial, no account, no subscription.

#windows #chrome #freesoftware #downloadmanager #techtok
```

### Caption B — resume hook

```
Chrome closed. The download didn't.

RapidFetch keeps a .part file and a segment map, so the transfer resumes after a reboot instead of starting over. Free for life, open source.

#windows #chrome #techtok #freesoftware
```

### Caption C — mechanism hook

```
One file. Up to 32 connections.

RapidFetch splits a download into HTTP range segments, retries them individually, and joins the pieces. Servers without range support fall back to one stream. No fake speed dial.

#windows #chrome #downloadmanager #opensource
```

### Caption D — video/HLS hook

```
Video + HLS from Chrome, handed to a desktop downloader.

Pick a format, keep the file local. Only download content you own or have permission to save. No DRM.

#chrome #videodownloader #hls #windows #freesoftware
```

## YouTube

**Title:**

```
RapidFetch: Free IDM Alternative for Windows & Chrome (2026)
```

**Description:**

```
RapidFetch is a free, open-source download manager for Windows 10/11 with a native Chrome extension. It uses up to 32 HTTP range connections per file, resumes interrupted transfers from a .part file, captures video and HLS from Chrome, and includes queues, a scheduler, speed limits and categories.

Free for life — no trial, no account, no subscription. MIT-licensed, no telemetry.

Download and source:
https://github.com/sohanur999999/rapidfetch

Chrome extension: the Chrome Web Store listing is pending, so use the official ZIP and the manual install steps in the README. The installer is unsigned for now; SHA-256 checksums are on the release page.

Chapters:
0:00 The problem with paid download managers
0:25 Install the Windows app
0:50 Load the Chrome extension
1:20 Segmented downloads and resume
2:00 Video and HLS capture
2:30 Queue and scheduler
3:00 Free-for-life boundary and caveats

Only download content you own or have permission to save. RapidFetch does not bypass DRM and is not affiliated with Tonec/Internet Download Manager.

#RapidFetch #DownloadManager #Windows #ChromeExtension #FreeSoftware #OpenSource #IDMAlternative
```

**Thumbnail text:**

```
FREE IDM
ALTERNATIVE
32 connections · Windows + Chrome
```

**Pinned comment:**

```
The Chrome Web Store listing is still pending, so the extension is a manual ZIP for now. Exact steps: install the Windows app → download the extension ZIP → chrome://extensions → Developer mode → Load unpacked → select the folder with manifest.json. Questions welcome.
```

## Directory listings

### Short blurb (50 words)

```
RapidFetch is a free, open-source download manager for Windows 10/11 with a native Chrome extension. It uses up to 32 HTTP range connections per file, resumes interrupted transfers, captures video and HLS, and includes queues, a scheduler and speed limits. Free for life; no trial, account or subscription.
```

### Long blurb (120 words)

```
RapidFetch is a free download manager for Windows 10/11 with a native Manifest V3 Chrome extension. It captures browser downloads and accelerates them with up to 32 HTTP range connections per file, then resumes from a .part file and segment map after the app closes or the machine reboots. Servers without range support fall back to a single stream. The app also captures direct video, audio and HLS playlists from Chrome, and includes batch queues, a scheduler with midnight wrap, daily transfer caps, speed limits, categories, a site grabber and optional yt-dlp support. There is no trial clock, account, subscription, telemetry or activation server. The code is MIT-licensed. The Chrome Web Store listing is pending, so the extension currently installs from an official ZIP.
```

### Listing fields

| Field | Value |
| --- | --- |
| Name | RapidFetch |
| Category | Utilities / Download Managers |
| License | MIT |
| Price | Free |
| Platforms | Windows 10/11, 64-bit; Chrome/Chromium extension |
| Version | 1.0.0 |
| Homepage | https://sohanur999999.github.io/rapidfetch/ |
| Source | https://github.com/sohanur999999/rapidfetch |
| Download | https://github.com/sohanur999999/rapidfetch/releases/latest |
| Screenshots | https://sohanur999999.github.io/rapidfetch/store-assets/ |
| Tags | download manager, Chrome extension, segmented downloads, resumable downloads, HLS, IDM alternative, freeware, open source |

### AlternativeTo

```
RapidFetch is a free, open-source alternative to Internet Download Manager for Windows 10/11. It offers segmented downloads with up to 32 HTTP range connections per file, restart-safe resume, a native Chrome extension, video/HLS capture, queues, a scheduler and speed limits. No trial, account or subscription. MIT-licensed. The Chrome Web Store listing is pending, so the extension currently installs from an official ZIP.
```

### Softpedia / MajorGeeks

```
RapidFetch is a free download manager for Windows 10/11 with a native Chrome extension. It accelerates files with up to 32 segmented HTTP range connections, resumes interrupted transfers, captures video and HLS from the browser, and includes queues, scheduling and speed limits. The project is MIT-licensed and free for life, with no account, trial or subscription. The Chrome Web Store listing is pending; the extension installs from an official ZIP.
```

## Outreach templates

### Creator / YouTuber pitch

Subject: `Free IDM alternative for your Windows/Chrome audience`

```
Hi [Name],

I watch [specific video or series] — [one sentence that proves it].

I built RapidFetch, a free, MIT-licensed download manager for Windows 10/11 with a native Chrome extension. It does segmented downloads (up to 32 range connections), resume after a reboot, HLS capture, queues and scheduling. No trial, account or subscription.

I am not asking for a review or payment. If it fits a future video, the honest angle is "a free IDM alternative with no trial timer" — including the caveats: the Chrome Web Store listing is still pending, the extension is a manual ZIP for now, and the installer is unsigned.

Press kit and assets: [link]
Source: https://github.com/sohanur999999/rapidfetch

If it is not a fit, no problem. Thanks for the work.

[Your name]
```

### Newsletter / curator pitch

Subject: `Free, open-source download manager for a future roundup`

```
Hi [Name],

For a future freeware/open-source roundup: RapidFetch is a free download manager for Windows 10/11 with a Chrome extension. Segmented transfers, resume, HLS capture, queues and scheduling. MIT-licensed, no telemetry, no account.

It is not on the Chrome Web Store yet (the listing is pending), so the extension is a manual ZIP. The Windows installer is unsigned and checksummed.

Details and screenshots: [press kit link]
Source: https://github.com/sohanur999999/rapidfetch

Happy to answer questions or send a specific asset. No payment or affiliate deal expected.

[Your name]
```

### Directory submission

```
Title: RapidFetch — Free Download Manager for Windows + Chrome
Category: Utilities / Download Managers
License: MIT
Price: Free
Platforms: Windows 10/11 (64-bit), Chrome/Chromium extension
Version: 1.0.0
Homepage: https://sohanur999999.github.io/rapidfetch/
Download: https://github.com/sohanur999999/rapidfetch/releases/latest
Description: [long blurb]
Notes: The Chrome Web Store listing is pending; the extension is distributed as an official ZIP. The installer is unsigned; SHA-256 checksums are published.
```

### Follow-up (after 5–7 days)

```
Hi [Name],

Just floating this once in case it got buried. RapidFetch is the free, open-source Windows download manager I mentioned: [one-line description].

No pressure either way. If it is not a fit, I will leave it here.

[Your name]
```

## Hashtags

Use 3–5, not a wall:

```
#Windows #Chrome #ChromeExtension #FreeSoftware #OpenSource #DownloadManager #IDMAlternative #BuildInPublic #TechTok
```

## What not to post

- "Download any video from any site." It is false and invites the wrong audience.
- "Faster than IDM" without a published benchmark.
- "Available on the Chrome Web Store" before the listing is live.
- "Mac/Linux/Android/iOS support." It does not exist.
- Fake user counts, fake reviews, or "limited-time" urgency.
- Competitor logos, YouTube/Netflix/Spotify screenshots, or copyrighted stills.
- The same post pasted into five communities in one hour.
