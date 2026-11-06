# RapidFetch viral launch kit

This folder is the working launch kit for RapidFetch. It is written to be used,
not admired: copy, hooks, scripts, a 30-day calendar, outreach templates, and a
rendered vertical teaser are all here.

## What is in this kit

| File | Use it for |
| --- | --- |
| [`strategy.md`](strategy.md) | Positioning, audiences, channel plan, 30-day calendar, metrics, risks |
| [`copy.md`](copy.md) | Ready-to-paste posts for X, LinkedIn, Hacker News, Reddit, Product Hunt, YouTube and directories |
| [`video-scripts.md`](video-scripts.md) | Four short-form scripts, shot lists, captions and production rules |
| [`technical-article.md`](technical-article.md) | A full draft for dev.to, Hashnode or a GitHub discussion |
| [`outreach.md`](outreach.md) | Directory and creator outreach templates with a tracker |
| [`assets/rapidfetch-teaser-1080x1920.mp4`](assets/rapidfetch-teaser-1080x1920.mp4) | 14.7-second vertical teaser with burned-in copy and UI sound effects |
| [`assets/rapidfetch-teaser-1080x1920.srt`](assets/rapidfetch-teaser-1080x1920.srt) | Captions for the teaser |
| [`assets/poster-hook.jpg`](assets/poster-hook.jpg) | Cover image from the teaser hook |
| [`assets/poster-end.jpg`](assets/poster-end.jpg) | Cover image from the end card |
| [`../docs/press.html`](../docs/press.html) | Public press kit: facts, approved copy, assets and claim rules |
| [`../docs/assets/rapidfetch-press-kit.zip`](../docs/assets/rapidfetch-press-kit.zip) | Complete ZIP for outreach: teaser, captions, posters, icon, social preview and five screenshots |

The editable HyperFrames project for the teaser lives in
[`video-teaser/`](video-teaser/). Render it again with:

```powershell
cd marketing\video-teaser
npx hyperframes check
npx hyperframes render --quality delivery -o ..\assets\rapidfetch-teaser-1080x1920.mp4
```

## The campaign in one paragraph

RapidFetch is a free, MIT-licensed download manager for Windows with a native
Chrome extension. It does the things people expect from a paid download manager
— segmented transfers, resume, queues, scheduling and browser capture — without
a trial clock, account or subscription. The launch story is not "we are faster
than IDM"; it is "the paid-download-manager era does not have to cost $30, and
here is the real UI to prove it." Lead with the price contrast, prove it with the
product, and be transparent about the unsigned installer and the pending Chrome
Web Store listing.

## Ground rules

- **Free for life** means what the README says: every current core feature is in
  the free build, with no trial, account or subscription. Do not add a paid tier
  and keep the claim.
- **No unverified speed claims.** "Up to 32 range connections" is a product fact.
  "Faster than IDM" is not; do not say it without a published benchmark.
- **No DRM, piracy or "download any video" claims.** The supported path is direct
  files, HLS, and optional yt-dlp for content the user has the right to save.
- **Windows 10/11 only.** Do not imply macOS, Linux, Android or iOS support.
- **The Chrome Web Store listing is not live.** Use the official extension ZIP
  and the manual install steps. Never add a dead store link.
- **Disclose that you are the maker** in every community post. Read each
  community's self-promotion rules before posting. Do not spam, buy reviews or
  fake traction.
- **Do not use third-party logos or copyrighted site screenshots** in promos.
  The teaser uses a public-domain Big Buck Bunny demo page and the real product.

## Publishing status

Nothing in this kit has been posted to a third-party platform. The BroBuddy
article is live. The site changes are live at:

- Landing page: <https://sohanur999999.github.io/rapidfetch/>
- Press kit: <https://sohanur999999.github.io/rapidfetch/press.html>
- Teaser MP4: <https://sohanur999999.github.io/rapidfetch/assets/rapidfetch-teaser-1080x1920.mp4>
- Press kit ZIP: <https://sohanur999999.github.io/rapidfetch/assets/rapidfetch-press-kit.zip>

The landing and press pages score 100/100/100 in Lighthouse (accessibility,
best practices, SEO). The GitHub release notes were corrected to remove a dead
Chrome Web Store link; the listing is still pending and the extension remains a
manual ZIP install.

Posting to X, LinkedIn, Product Hunt, Hacker News, Reddit, YouTube or any
directory needs the account owner's approval and access. Do not post on the
maker's behalf without both.
