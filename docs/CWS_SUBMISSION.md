# Chrome Web Store Submission Guide

Everything is built and published on GitHub. This guide covers the only remaining
step: creating the Chrome Web Store developer account and submitting the
extension.

## Status

| Item | Status |
| --- | --- |
| GitHub repository | Live: https://github.com/sohanur999999/rapidfetch |
| GitHub Pages landing page | Live: https://sohanur999999.github.io/rapidfetch/ |
| Privacy policy URL | Live: https://sohanur999999.github.io/rapidfetch/privacy.html |
| GitHub release `v1.0.0` | Live with EXE, ZIP, extension ZIP and checksums |
| Chrome Web Store developer account | **Not created yet** |
| Chrome Web Store listing | **Not submitted** |
| Public Chrome install path | Manual extension ZIP (Store listing pending) |

## 1. Create the developer account

The Chrome Web Store charges a one-time **US$5** registration fee. Google
requires a payment method; the fee is not recurring.

1. Open https://chrome.google.com/webstore/devconsole in the browser where you
   are signed in to the Google account that should own the extension.
2. Sign in and accept the Chrome Web Store Developer Agreement.
3. Pay the one-time registration fee.
4. Complete the publisher profile:
   - **Publisher name:** `RapidFetch`
   - **Contact email:** an email you check; review and takedown notices go here.
   - Verify the email if Google asks.
5. Enable 2-Step Verification on the Google account if it is not already on.
   Google requires it for publisher accounts.

You cannot upload an item until the fee is paid and the publisher profile is
complete.

## 2. Create the item

1. In the developer dashboard, click **New item**.
2. Upload:

   ```
   dist\RapidFetch-Extension-1.0.0.zip
   ```

   The ZIP has `manifest.json` at its root and contains 16 files. The version is
   `1.0.0`.
3. Wait for the manifest check. The expected name is
   `RapidFetch - Free Download Manager` and the expected extension ID is
   `lmbdeehijjekhepgoioomfgobpkgmlcb`.

If the dashboard reports a manifest warning, stop and fix it before continuing.
Do not upload the folder `extension\` directly; upload the ZIP.

## 3. Store listing fields

Use the full copy in `docs/STORE_LISTING.md`. The key fields are:

| Field | Value |
| --- | --- |
| Item name | `RapidFetch - Free Download Manager` |
| Summary | `Download files and videos faster with segmented, resumable transfers. Free for life, no trial and no subscription.` |
| Category | Productivity |
| Language | English (United States) |
| Homepage URL | `https://github.com/sohanur999999/rapidfetch` |
| Support URL | `https://github.com/sohanur999999/rapidfetch/issues` |
| Privacy policy URL | `https://sohanur999999.github.io/rapidfetch/privacy.html` |
| Developer | `RapidFetch` |

### Description

Paste the **Detailed description** block from `docs/STORE_LISTING.md`. It starts
with:

> RapidFetch is a free download manager for Chrome and Windows. It captures
> downloads from your browser, accelerates them with up to 32 segmented
> connections, and resumes interrupted transfers without starting over.

### Graphic assets

| Asset | File | Size |
| --- | --- | --- |
| Store icon | `extension/icons/icon128.png` | 128x128 |
| Screenshot 1 | `docs/store-assets/01-desktop.png` | 1280x800 |
| Screenshot 2 | `docs/store-assets/02-browser-capture.png` | 1280x800 |
| Screenshot 3 | `docs/store-assets/03-pause-resume.png` | 1280x800 |
| Screenshot 4 | `docs/store-assets/04-queue-scheduler.png` | 1280x800 |
| Screenshot 5 | `docs/store-assets/05-free-for-life.png` | 1280x800 |
| Small promo tile | `docs/store-assets/promo-440x280.png` | 440x280 |
| Marquee | `docs/store-assets/marquee-1400x560.png` | 1400x560 |

Upload all five screenshots in that order. The store icon is required; the promo
tile and marquee are optional but improve discovery.

## 4. Privacy practices tab

### Single purpose

```text
RapidFetch captures downloads and media from the browser and hands them to the
RapidFetch desktop download manager installed by the user.
```

### Permission justifications

Copy each block from `docs/STORE_LISTING.md` under **Permission justifications**:

- `downloads`
- `storage`
- `contextMenus`
- `webRequest`
- `tabs`
- `notifications`
- `scripting`
- `nativeMessaging`
- Host permission `<all_urls>`

### Remote code

Select **No**. All JavaScript is packaged in the extension. The extension does not
load or execute remote code.

### Data usage

RapidFetch does not sell, rent, or transfer personal data to the developer. Page
URLs, media URLs, and download metadata are processed locally and sent only to
the RapidFetch desktop app on the same computer. There are no analytics,
advertising, or tracking SDKs.

If the form asks which data the extension handles, be accurate:

- **Website content** — read locally to find downloadable media and links.
- **Browsing activity** — the active tab URL is used to route commands and keep
  per-tab state.
- **Downloads** — managed through `chrome.downloads`.

None of this is sent to a developer server, sold, or used for advertising. State
that in the justification field so the local processing is not mistaken for
off-device collection.

### Privacy policy

Use:

```text
https://sohanur999999.github.io/rapidfetch/privacy.html
```

## 5. Distribution

- Visibility: **Public**
- Regions: all regions
- Pricing: **Free**
- Mature content: **No**
- Ads: **No** (v1.0.0 has no ads)

## 6. Submit for review

1. Click **Submit for review**.
2. Confirm the submission.
3. Watch the contact email for reviewer questions. Reply quickly; most delays are
   caused by unanswered permission or native-app questions.
4. Review usually takes one to three business days, but can take longer for broad
   host permissions and native messaging.

The listing URL after approval will be:

```text
https://chromewebstore.google.com/detail/lmbdeehijjekhepgoioomfgobpkgmlcb
```

That URL is already used by the README, landing page, release notes and
`docs/STORE_LISTING.md`.

## 7. After approval

- Open the live listing and verify the name, screenshots, and privacy policy link.
- Install from the store in a clean Chrome profile.
- Confirm the native host still connects (extension ID must remain
  `lmbdeehijjekhepgoioomfgobpkgmlcb`).
- Run one real download and one pause/resume cycle.
- Add a `v1.0.1` or `v1.1.0` release only for real fixes; frequent meaningful
  updates help ranking.
- Do not buy installs or reviews. Do not keyword-stuff the listing.

## Common rejection causes to avoid

- Claiming the extension works without the desktop app. It does not; the listing
  says so.
- Missing permission justifications for `webRequest`, `nativeMessaging`, or
  `<all_urls>`.
- A privacy policy that does not match the manifest permissions.
- Screenshots that show a different product or a fake UI.
- Keyword spam in the name or description.
- Adding ads, analytics, or remote code after approval without updating the
  privacy policy and listing.

## What is deliberately not in v1.0.0

- No ads or sponsor cards. They can be added in a later version with clear
  labeling and a way to dismiss them.
- No paid tier. The **Free for life** claim is accurate for every current core
  feature.
- No analytics, telemetry, accounts, or activation servers.
