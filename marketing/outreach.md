# RapidFetch outreach and directory tracker

Use this as a work queue, not a blast list. Every entry needs a real fit, a
personalised message, and a human on the other end. No paid reviews, no fake
accounts, no automated mass submissions.

## Before any outreach

- [ ] The landing page has the current version, installer checksum and manual
      extension steps.
- [ ] `docs/press.html` is live with facts and assets.
- [ ] The GitHub release has the installer, extension ZIP and `SHA256SUMS.txt`.
- [ ] You can answer the three hard questions: SmartScreen, Chrome Web Store,
      and "is it faster than IDM?"
- [ ] You have approval to use the sending account and a real reply address.

## Tier 1 — high-fit directories

| Directory | Submit at | Cost | Notes | Status |
| --- | --- | --- | --- | --- |
| AlternativeTo | <https://alternativeto.net/manage/add-software/> | Free | Create an account; list as an alternative to Internet Download Manager. Use the long blurb. Expect edits/review. | Not submitted |
| Product Hunt | <https://www.producthunt.com/posts/new> | Free | Needs a maker account and gallery. Launch as a product, not a link drop. First comment required. | Not scheduled |
| MajorGeeks | <https://www.majorgeeks.com/content/page/submit_software.html> | Free | Long-running Windows freeware directory. Include version, license, size, screenshots. | Not submitted |
| SnapFiles | <https://www.snapfiles.com/submit/> | Free | Windows freeware; expects a direct download URL. | Not submitted |
| Softpedia | <https://www.softpedia.com/mysoftware/> | Free | Account required; review can take weeks. | Not submitted |
| SourceForge | <https://sourceforge.net/create/> | Free | Optional mirror/project page; keep GitHub as the canonical source. | Not submitted |
| FossHub | <https://www.fosshub.com/> (developer contact) | Free | Open-source/freeware focus; ask first, do not assume acceptance. | Not submitted |

## Tier 2 — package managers and developer distribution

These are higher-effort and higher-trust than a directory listing. Do them after
the first patch release, when the installer URL is stable.

| Channel | What to do | Why it matters | Status |
| --- | --- | --- | --- |
| winget | Add a manifest to `microsoft/winget-pkgs` with the installer URL and SHA-256. Test with `winget install`. | Windows users search `winget` first. | Not started |
| Chocolatey | Package the installer, host on the community repo, test `choco install rapidfetch -y`. | Sysadmin and power-user distribution. | Not started |
| Scoop | Add a manifest to a bucket or submit to `extras`. | Developer crowd; easy updates. | Not started |
| Homebrew Cask | Only if a macOS build exists. It does not; skip. | Avoid implying macOS support. | N/A |

**Winget note:** an unsigned installer is allowed, but SmartScreen still applies.
Do not use a URL that changes between versions; pin the versioned release asset
and update the manifest each release.

## Tier 3 — communities (rules first)

| Community | Allowed format | Best angle | Do not |
| --- | --- | --- | --- |
| Hacker News / Show HN | Show HN post, maker present | Engineering: range probing, resume, MV3 bridge | Post marketing copy; argue with critics |
| r/software | Check sidebar/weekly thread | Free IDM alternative, install feedback | Cross-post identical text |
| r/windows | Check self-promotion rules | Install/UX feedback on Windows 10/11 | Lead with the download link |
| r/chrome_extensions | Check rules | MV3 extension + manual ZIP caveat | Hide the pending store listing |
| r/opensource | Check rules | MIT, architecture, code review | Ask for stars |
| r/freesoftware | Check rules | Free-for-life boundary, no telemetry | Spam the same blurb |
| r/DataHoarder | Strict; often no promo | Legal archives, queues, HLS you own | Mention piracy or DRM |
| Lobsters | Invite-only; only if you have an account | Technical article | Create a sock puppet |
| Indie Hackers | Product launch/feedback | Build story and distribution lessons | Fake traction |

**Rule of thumb:** one community per day, answer every comment for the first four
hours, and stop the moment a moderator asks you to. A removed post is worse than
no post.

## Tier 4 — creators and newsletters

| Target type | Examples (research current coverage first) | Pitch angle | Status |
| --- | --- | --- | --- |
| Windows utility YouTube channels | Channels that cover freeware, debloating, PowerToys, winget | "Free IDM alternative, real UI, no trial" | Build list of 20 |
| Chrome extension channels/sites | Newsletters that round up MV3 tools | "Browser capture without a subscription" | Build list of 10 |
| Open-source newsletters | Weekly OSS roundups | MIT license, .NET + MV3 architecture | Build list of 10 |
| Freeware blogs | Long-running Windows freeware sites | Directory-style listing + screenshots | Build list of 10 |
| University IT / lab managers | Local or forum contacts | Batch queue, scheduler, no account | 5 personalised emails |

Do not send a mass BCC. Personalise the first sentence with a specific video,
post or problem the recipient covered. If there is no fit, say so and move on.

## Outreach sequence

### Day 0 — prepare

1. Publish the press kit and verify every link.
2. Create the UTM links for each channel.
3. Write the one-paragraph pitch and the three caveats.
4. Prepare a 1200×630 image, a 1080×1920 poster and a 15-second clip.

### Day 1 — directories

Submit AlternativeTo, MajorGeeks and SnapFiles. Use the same factual listing
data, but adapt the description to each site's field limits. Record the
submission date and any confirmation email.

### Day 2 — creators

Send 10 personalised pitches. Do not attach a ZIP; link the press kit and the
release page. Offer a specific asset ("I can send a 20-second clip of the resume
flow") instead of a generic press release.

### Day 5–7 — follow up once

One short follow-up per target. If there is no reply, stop. Do not add them to a
newsletter or send a third message.

### Day 7–10 — communities

Stagger Show HN, Product Hunt and Reddit. Keep the technical article ready for
the questions that repeat. Log every comment that reveals a product problem.

### Day 14–30 — maintain

Update the directory listings when the version changes. Reply to new reviews and
issues. Add a "Recommended on …" line to the press kit only after a listing is
actually live.

## Tracker

Copy this table into a spreadsheet. One row per target, not per message.

| Target | Type | URL | Contact | Sent | Follow-up | Reply | Listing live | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AlternativeTo | Directory | | | | | | | |
| MajorGeeks | Directory | | | | | | | |
| SnapFiles | Directory | | | | | | | |
| Softpedia | Directory | | | | | | | |
| Product Hunt | Launch | | | | | | | |
| Hacker News | Community | | | | | | | |
| r/software | Community | | | | | | | |
| r/chrome_extensions | Community | | | | | | | |
| Creator 1 | Creator | | | | | | | |
| Newsletter 1 | Newsletter | | | | | | | |

## Message templates

### Short directory submission

```text
Title: RapidFetch — Free Download Manager for Windows + Chrome
Category: Utilities / Download Managers
License: MIT
Price: Free
Platforms: Windows 10/11 (64-bit); Chrome/Chromium extension
Version: 1.0.0
Homepage: https://sohanur999999.github.io/rapidfetch/
Download: https://github.com/sohanur999999/rapidfetch/releases/latest
Description: RapidFetch is a free, open-source download manager for Windows 10/11 with a native Chrome extension. It accelerates files with up to 32 HTTP range connections, resumes interrupted transfers after a reboot, captures video and HLS from the browser, and includes queues, scheduling and speed limits. No trial, account or subscription.
Notes: The Chrome Web Store listing is pending; the extension is distributed as an official ZIP. The installer is unsigned; SHA-256 checksums are published.
```

### Creator pitch

```text
Subject: Free IDM alternative for a future Windows/Chrome video

Hi [Name],

I watched [specific video] — [one sentence proving you watched it, and why the
topic fits].

I built RapidFetch, a free, MIT-licensed download manager for Windows 10/11 with
a native Chrome extension. Segmented transfers (up to 32 range connections),
restart-safe resume, HLS capture, queues and scheduling. No trial, account or
subscription.

I am not asking for a review or payment. If it fits a future video, the honest
angle is "a free IDM alternative without the trial clock," including the
caveats: the Chrome Web Store listing is still pending, the extension is a manual
ZIP for now, and the installer is unsigned.

Press kit: [link]
Source: https://github.com/sohanur999999/rapidfetch

If it is not a fit, no problem — thanks for the work.

[Your name]
```

### Follow-up (one time, 5–7 days later)

```text
Hi [Name],

Just floating this once in case it got buried. RapidFetch is the free, open-source
Windows download manager I mentioned: [one-line description].

No pressure either way. If it is not a fit, I will leave it here.

[Your name]
```

### Reply to a directory editor asking for more

```text
Thanks for checking. Here is the missing information:

- Version: 1.0.0
- Installer size: [size]
- License: MIT (source: https://github.com/sohanur999999/rapidfetch)
- Screenshots: https://sohanur999999.github.io/rapidfetch/store-assets/
- Direct download: [versioned release asset URL]
- SHA-256: [checksum]

The Chrome Web Store listing is pending, so the extension is a separate ZIP.
The installer is unsigned; the checksums and source are public. Happy to answer
anything else.
```

## Red flags to avoid

- Paying for a "guaranteed listing" or a review.
- Submitting the same text with five different product names.
- Creating a new account to repost after a removal.
- Asking friends to upvote a Product Hunt launch or star the repo.
- Claiming "featured on …" before a listing is live.
- Using a competitor's logo in a comparison image.
- Promising "faster than IDM" without a published benchmark.
- Linking the Chrome Web Store before the listing is approved.

## What to do with replies

Every reply is product research. Log the exact words, tag them (install friction,
SmartScreen, speed, missing feature, trust, platform), and fix the top repeated
issue before the next outreach wave. A directory listing that sends 200 visitors
to a confusing install flow is not a win.
