# RapidFetch SEO and Launch Playbook

## Reality check

No one can guarantee **#1** in Chrome Web Store search. Google's public
documentation says ranking uses the item name, description relevance,
popularity, ratings, average rating, downloads versus uninstalls, engagement,
and overall user experience. A brand-new listing with no ratings will not
outrank established download managers on day one.

The goal is to be the **most relevant and most trusted** listing for a small set
of realistic queries, then improve rank as installs and ratings grow.

**Current launch state:** the Chrome Web Store listing is pending. The live site
and README offer the extension ZIP with manual install steps. The Store-specific
copy below is ready for the day the listing can be published.

## Target keywords

### Primary

- free download manager
- download manager for Chrome
- Chrome download manager
- download manager for Windows

### Secondary

- video downloader for Chrome
- HLS downloader
- resumable download manager
- batch downloader
- download accelerator
- IDM alternative
- browser download manager
- media downloader

Use each phrase naturally. Do not repeat one keyword more than five times.
Keyword stuffing is a Chrome Web Store policy violation.

## Chrome Web Store fields

| Field | Recommended text |
| --- | --- |
| Name | `RapidFetch - Free Download Manager` |
| Summary | `Download files and videos faster with segmented, resumable transfers. Free for life, no trial and no subscription.` |
| Category | Productivity |
| First description line | `RapidFetch is a free download manager for Chrome and Windows.` |
| Second line | `It captures downloads from your browser, accelerates them with up to 32 segmented connections, and resumes interrupted transfers without starting over.` |

The first 132 characters matter most because they appear in search results.

## GitHub repository SEO

- Repository name: `rapidfetch`
- Description: `Free download manager for Windows with a Chrome extension. Segmented and resumable downloads, HLS, media capture, batch queues, and optional yt-dlp support.`
- Website: GitHub Pages landing page. Add the Chrome Web Store URL once the
  listing is live.
- Topics:

```text
download-manager
chrome-extension
windows
wpf
dotnet
video-downloader
hls
yt-dlp
download-accelerator
internet-download-manager
browser-extension
free-download-manager
resumable-downloads
media-downloader
```

- README first paragraph should contain: **free download manager**, **Windows**,
  **Chrome extension**, **segmented**, **resumable**, **HLS**.
- Publish releases with clear names: `RapidFetch 1.0.0 — Windows installer + Chrome extension`.
- Add a `CHANGELOG.md` and update it with every release.
- Add a GitHub Pages landing page at `docs/index.html` with the same keywords as
  the store listing.

## Launch sequence

### Week 1

1. Publish the GitHub repository and release.
2. Publish the landing page, privacy policy, and extension ZIP with manual
   install steps. Add the Chrome Web Store listing when it is available.
3. Test install, onboarding, capture, pause/resume, and uninstall in a clean
   Chrome profile.
4. Ask 20–50 real users to try it. Do not buy reviews.

### Week 2–4

1. Fix every install or permission complaint immediately.
2. Add an in-app review prompt after a successful download, not on first launch.
3. Publish a short YouTube demo: "Free download manager for Chrome and Windows".
4. Post in relevant communities where self-promotion is allowed. Read each
   community's rules first.
5. Submit to software directories that accept original freeware.

### Month 2–3

1. Ship an update every 2–4 weeks with real fixes.
2. Add localized listings only after the English listing converts.
3. A/B test screenshots and the summary after 1,000 impressions.
4. Add tutorials for legitimate use cases: downloading your own media, offline
   access, batch course materials, HLS streams you own.
5. Respond to every review, especially negative ones.

## Review prompt rules

Ask after a successful download:

> Enjoying RapidFetch? A rating helps other people find it.

- Never gate features behind a review.
- Never offer rewards for reviews.
- Never ask for five stars only.
- Make **Not now** easy to find.

## Conversion assets

- First screenshot must show the product working, not a logo.
- Put the strongest benefit in the first 5–7 words.
- Keep each screenshot to one idea.
- Use the same blue/cyan/green brand as the icon.
- Add the small promo tile; listings without one are shown after listings with
  one.
- Add a 30–60 second YouTube demo. It increases trust even if few people watch.

## Backlinks that help discovery

- GitHub README and releases.
- GitHub Pages landing page.
- YouTube tutorial description.
- AlternativeTo, Softpedia, MajorGeeks, and similar directories that accept
  original freeware.
- Relevant Reddit and forum posts where promotion is allowed.
- A short write-up on dev.to, Hashnode, or Medium about the segmented download
  engine.

## Metrics to track weekly

- Chrome Web Store impressions
- Listing page views
- Installs and uninstalls
- Active users
- Average rating and number of ratings
- Support issues and permission complaints
- Landing-page clicks to the extension ZIP and app downloads

## What not to do

- Do not buy installs or reviews.
- Do not use "free download" ads that install something else.
- Do not bundle unrelated software.
- Do not inject ads into third-party pages.
- Do not keyword-stuff names or descriptions.
- Do not promise "number one" or "best" in metadata.
- Do not use a lifetime-price claim if you later add a mandatory subscription.
