# RapidFetch viral strategy

## The honest goal

"Viral" is not a button. For a free Windows utility, virality is a short spike of
attention followed by a long tail of search, directory listings, word of mouth
and updates. The practical target for the first 30 days is:

- **1,000–3,000 landing-page visits** from launch posts and short-form video.
- **150–400 Windows installs** and **100–300 extension ZIP downloads**, measured
  as GitHub release-asset downloads and landing-page outbound clicks.
- **50–150 GitHub stars** if one technical post lands; fewer is normal.
- **20–50 substantive comments or issues** that can become the next release.

These are planning numbers, not promises. A post that reaches 100,000 people but
produces ten installs is not a win; a 300-person community that produces 60
installs and five bug reports is.

## The one-line pitch

**RapidFetch is a free, open-source download manager for Windows with a native
Chrome extension — segmented, resumable transfers without the trial clock.**

Supporting proof, in order:

1. **Price contrast:** free for life; no trial, account or subscription.
2. **Visible mechanism:** up to 32 HTTP range connections per file, with
   single-stream fallback when a server does not support ranges.
3. **Real relief:** pause, close, reboot, resume from the `.part` file.
4. **Browser-native:** captures downloads, links, media and HLS from Chrome.
5. **Honest trust signals:** MIT license, local-only data, no telemetry, public
   SHA-256 checksums.

Do not lead with a feature list. Lead with a situation the viewer recognises:
paying for a download manager, losing a 4 GB download when Chrome closes, or
watching a video that the browser will not save.

## What makes this shareable

A product spreads when the share itself is useful. RapidFetch has five shareable
artifacts:

1. **The price contrast.** "IDM costs about $30; this is $0" is a one-line story.
   Keep it factual and do not use the IDM logo.
2. **The 32-connection demo.** A live speed panel with multiple segmented
   connections is visually different from a browser download bar.
3. **The resume proof.** Closing the app and resuming the same file is a strong
   before/after clip. It is also easy to verify.
4. **The underdog transparency.** The Chrome Web Store fee is unpaid, so the
   extension ships as a signed-in manual ZIP. This is a relatable builder story,
   but use it in replies and the FAQ, not as the main headline.
5. **The free-for-life promise.** A public, written promise with a clear boundary
   ("while every current core feature stays free") is more credible than
   "free forever" with an asterisk.

## Audiences and what they care about

| Audience | Where they are | What they care about | Lead with |
| --- | --- | --- | --- |
| Windows power users tired of trialware | r/software, r/windows, X, YouTube | No nag, no subscription, a real queue | Price contrast + real UI |
| Chrome users with large downloads | r/chrome_extensions, Chrome communities | Browser capture, resume, speed | "Chrome closed; the download didn't die" |
| Legitimate video/offline users | r/DataHoarder, video forums, YouTube | HLS capture, formats, batch | HLS + format picker, with the rights caveat |
| Open-source and privacy crowd | r/opensource, r/freesoftware, Hacker News, Lobsters | MIT, local-only, no telemetry, inspectable code | Source + no account + no analytics |
| Indie builders | X, LinkedIn, Indie Hackers, Product Hunt | The build story, distribution, honesty | "I shipped a free IDM alternative" |
| Software directory browsers | AlternativeTo, Softpedia, MajorGeeks | A clear freeware listing, screenshots, license | Listing pack + checksums |

## Channel plan

Priority is based on effort, fit and whether the account already exists. Do not
open every channel at once.

### Tier 1 — owned, no permission needed

| Channel | Role | Cadence |
| --- | --- | --- |
| GitHub repo, release and README | The trust anchor; every post links here | Update at launch and every release |
| Landing page (`docs/index.html`) | Converts attention into installs | Add share strip, press kit, SmartScreen FAQ |
| BroBuddy article | Long-form SEO and a shareable explainer | Already live; add internal links when the site changes |
| X / Twitter | Launch thread, demo clips, build-in-public replies | 1 thread + 3–5 short posts/week |
| LinkedIn | Builder story and a link to the article | 1 post at launch, 1 every two weeks |
| YouTube / Shorts | Searchable demo and Shorts cut | 1 long demo + 3 Shorts in month one |
| dev.to / Hashnode | Technical article, backlinks, HN-friendly proof | 1 article at launch, 1 per month |

### Tier 2 — communities, rules first

| Channel | Role | Rules / caveats |
| --- | --- | --- |
| Hacker News (Show HN) | Technical spike and serious feedback | Must be the maker; no marketing language; answer every technical question |
| Product Hunt | Launch-day spike and a permanent listing | Needs a maker account, gallery, first comment; ask for feedback, not upvotes |
| r/software | Broad freeware audience | Read the sidebar; many subs restrict self-promotion to certain days |
| r/windows | Windows users | Use a help/feedback framing, not an ad |
| r/chrome_extensions | Extension users | Mention the manual ZIP and the pending store listing |
| r/opensource / r/freesoftware | License and source audience | Lead with MIT and the repo, not the download |
| r/DataHoarder | Batch/queue/scheduler users | Strict no-piracy culture; focus on legal files and HLS you own |
| AlternativeTo, Softpedia, MajorGeeks, FossHub | Evergreen discovery | Submit the original freeware; expect review delays |

Do not post the same text to five subreddits in one hour. Write a different post
for each, participate in the comments, and stop if a moderator says stop.

### Tier 3 — outreach, low volume

- Freeware newsletters and "open-source finds" curators.
- Windows and Chrome YouTube channels that cover utilities.
- University IT / lab managers who need a free download queue.
- Local maker communities and developer Discord servers where self-promotion is
  explicitly allowed.

Personalise every message. A mass email is worse than no email.

## The launch sequence

### Phase 0 — trust and conversion (now, 2–3 days)

- [ ] Add the share strip and "Free IDM alternative" badge to the landing page.
- [ ] Add the press kit page with facts, assets and claim rules.
- [ ] Add the SmartScreen/unsigned-installer FAQ and link the SHA-256 files.
- [ ] Add the "Why is it free?" and "Is it faster?" answers to the FAQ.
- [ ] Publish the teaser to the site and use it as the social preview.
- [ ] Pin the launch thread on X; add the teaser to the GitHub release notes.
- [ ] Test the install flow in a clean Windows VM and a clean Chrome profile.
- [ ] Ask 20 real people to try it and report install friction. Do not ask for
  reviews or stars.

### Phase 1 — soft launch (days 1–3)

- [ ] GitHub release notes with the teaser, checksums and manual-install steps.
- [ ] X launch thread with the teaser; quote-post it 48 hours later with a reply
  to the best question.
- [ ] LinkedIn builder post.
- [ ] Submit to AlternativeTo and two directories that accept original freeware.
- [ ] Publish the technical article on dev.to; add the canonical URL.
- [ ] Send 10 personalised creator/newsletter pitches.

### Phase 2 — spike (days 4–10)

Stagger the big three so each gets a full day of replies:

- [ ] **Day 4:** Product Hunt launch. First comment explains the free-for-life
  boundary and the manual install. Ask for feedback, not upvotes.
- [ ] **Day 6:** Show HN. Title and body from `copy.md`. Be present for 4–6 hours;
  answer the hard questions about speed, safety and the pending store listing.
- [ ] **Day 8:** Reddit, one community at a time, only where the rules allow it.
  Start with r/software or r/opensource; do not cross-post the same text.
- [ ] **Day 9:** YouTube demo + Shorts cut.
- [ ] **Day 10:** Recap post: "What 300 people told me about a free IDM
  alternative" with the real feedback and the fixes shipped.

### Phase 3 — sustain (days 11–30)

- 3 short-form videos per week, each one idea: resume, HLS, scheduler, batch,
  free-for-life, privacy, manual install.
- 2 helpful community comments per day where linking is allowed; answer questions
  without dropping the link every time.
- 1 technical or tutorial post per week.
- Ship a small update every 2 weeks; put the changelog in the release notes.
- Follow up with every directory and creator once, after 5–7 days.

## 30-day calendar

Day 1 is the first public launch post, not today. Adjust the dates once the
accounts and approval are confirmed.

| Day | Primary action | Channel | Asset | Success signal |
| --- | --- | --- | --- | --- |
| 1 | Launch thread + pinned post | X | Teaser + 6-post thread | 1,000 impressions, 20 link clicks |
| 1 | Builder story | LinkedIn | 150-word post + teaser | 10 meaningful comments |
| 1 | Release notes + teaser | GitHub | Release body | 25 release views |
| 2 | Submit 3 directories | AlternativeTo, etc. | Listing pack | 2 accepted listings |
| 2 | Send 10 creator pitches | Email/DM | Outreach template | 2 replies |
| 3 | Publish technical article | dev.to | `technical-article.md` | 100 reads |
| 4 | Product Hunt launch | Product Hunt | Gallery + first comment | 30 upvotes, 10 comments |
| 5 | Reply and recap PH questions | X / PH | Reply bank | 5 answered threads |
| 6 | Show HN | Hacker News | HN body | 10 comments, front-page try |
| 7 | Answer HN + fix top issue | GitHub | Patch/release | 1 shipped fix |
| 8 | Reddit post #1 | r/software or r/opensource | Sub-specific copy | 20 upvotes, 10 comments |
| 9 | YouTube demo + Shorts | YouTube | 60s demo | 100 views |
| 10 | Recap what users said | X / LinkedIn | Recap post | 20 clicks |
| 11 | Short #1: resume proof | TikTok/Reels/Shorts | New 20s cut | 500 views |
| 12 | Reddit post #2 (if allowed) | r/chrome_extensions | Sub-specific copy | 15 comments |
| 13 | Short #2: HLS/format picker | TikTok/Reels/Shorts | New 20s cut | 500 views |
| 14 | Newsletter follow-ups | Email | Follow-up template | 1 feature |
| 15 | Technical post #2 | dev.to / GitHub | Range-request deep dive | 50 reads |
| 16 | Short #3: scheduler | TikTok/Reels/Shorts | New 20s cut | 500 views |
| 17 | Community help day | Reddit / Discord | No link, just answers | 5 helpful replies |
| 18 | Ship 1.0.1 fixes | GitHub | Changelog | 10 downloads |
| 19 | Creator follow-up #2 | Email | Follow-up | 1 reply |
| 20 | X thread: "What I learned" | X | Thread + chart | 2,000 impressions |
| 21 | Short #4: free-for-life | TikTok/Reels/Shorts | New 15s cut | 500 views |
| 22 | Directory follow-ups | Email | Follow-up | 1 listing live |
| 23 | Reddit AMA-style feedback | r/software (if allowed) | Q&A post | 10 questions |
| 24 | Publish a tutorial | Blog / dev.to | "Download a legal HLS course offline" | 100 reads |
| 25 | Short #5: privacy/local-only | TikTok/Reels/Shorts | New 15s cut | 500 views |
| 26 | Update press kit with real numbers | `docs/press.html` | Facts + quotes | Page live |
| 27 | Outreach to 5 Windows creators | Email | Personalised pitch | 1 reply |
| 28 | Ship a visible improvement | GitHub | Release | 10 downloads |
| 29 | Recap month + thank-you post | X / LinkedIn | Thread | 20 clicks |
| 30 | Review metrics, pick the next bet | Internal | Dashboard | Decision |

## Metrics

Track weekly in a simple sheet. Use UTM links on every owned post:

```
https://sohanur999999.github.io/rapidfetch/?utm_source=x&utm_medium=social&utm_campaign=launch_v1
https://sohanur999999.github.io/rapidfetch/?utm_source=producthunt&utm_medium=launch&utm_campaign=launch_v1
https://sohanur999999.github.io/rapidfetch/?utm_source=reddit&utm_medium=community&utm_campaign=launch_v1
```

| Metric | Where | Why |
| --- | --- | --- |
| Landing-page sessions and referrers | GitHub Pages / analytics or server logs | Which channel sends traffic |
| Outbound clicks to installer and extension ZIP | Landing-page event or link shortener | Which channel converts |
| GitHub release-asset downloads | GitHub release page / API | Install intent |
| GitHub stars, forks, watchers | GitHub | Developer interest |
| Extension ZIP downloads | Release assets | Chrome intent |
| Comments, issues and sentiment | Each channel + GitHub | Product feedback and risk |
| Search Console impressions/clicks | Google Search Console | Long-tail search |
| Chrome Web Store impressions (later) | CWS dashboard | Store discovery |

Do not optimise for impressions. The decision metric is **qualified installs**
(installer or extension ZIP download) per channel.

## What "good" looks like in the first 30 days

- At least one channel produces 20+ installs.
- At least five users describe the same friction point; fix it.
- The landing page converts above 2% of sessions into a download click.
- The launch creates a permanent tail: a directory listing, a YouTube video, a
  technical article and a Product Hunt page that keep sending traffic.
- No moderation removal, no "this is spam" thread, no fake-review accusation.

## Risks and responses

| Risk | Response |
| --- | --- |
| Chrome Web Store still pending | Say so first. Use the manual ZIP with exact steps. Never post a dead link. |
| Windows SmartScreen "Unknown publisher" | Explain it is unsigned, link the SHA-256 files and the source. Never tell users to disable antivirus. |
| "Is this a virus?" | Link the MIT source, the release checksums and the privacy policy. Offer to answer questions. |
| "Free for life is a lie" | Quote the README boundary: free while every current core feature stays free; if that changes, the page says so first. |
| "Faster than IDM?" | Do not claim it. Say segmented transfers can improve throughput when the server supports ranges; results vary. |
| Reddit removal | Read the rules before posting; ask mods; use weekly promo threads; do not repost after removal. |
| HN flagging | Use Show HN, write like an engineer, disclose the maker, answer criticism directly. |
| Trademark complaint | Use "IDM alternative" descriptively, never the IDM logo or a fake affiliation. Keep the disclaimer. |
| Copyright complaint | Use public-domain demo files; do not show YouTube/Netflix/Spotify content; state the rights caveat. |
| Burnout | The calendar is a menu, not a debt. Two good posts a week beat seven bad ones. |

## The one thing to remember

If a viewer remembers only one line, make it:

> **A download manager should not cost $30 and nag you. RapidFetch is free for
> life, and the source is on GitHub.**
