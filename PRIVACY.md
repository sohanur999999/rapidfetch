# RapidFetch Privacy Policy

**Effective date:** November 5, 2026

RapidFetch is a Windows download manager with an optional browser extension for
Chromium browsers. This policy explains what the desktop app and the extension
do with your information.

The short version: **RapidFetch does not collect, sell, or share your personal
data.** Downloads, history, and settings stay on your computer. There are no
accounts, analytics, advertising SDKs, or tracking scripts.

## 1. Information we do not collect

We do not collect or transmit:

- your name, email address, or account credentials;
- your browsing history;
- your download history;
- the files you download;
- your IP address for analytics or advertising;
- device fingerprints, telemetry, or crash reports.

RapidFetch has no advertising network, no analytics service, and no user
account system.

## 2. Information processed on your device

### Browser extension

The extension reads the current page to find downloadable media and links. It
may process:

- the page URL and title;
- media and download URLs found in the page or in network responses;
- file names, sizes, content types, and download status;
- your RapidFetch settings.

This information is used only to show download options and to send the selected
download to the RapidFetch desktop app on the same computer.

The extension stores settings in `chrome.storage`. Settings may sync through
your browser account if Chrome sync is enabled. Tab-specific media state is kept
in session storage and is cleared when the tab closes or navigates.

### Desktop app

The app stores settings, download history, and queue state in:

```
%APPDATA%\RapidFetch
```

Downloaded files are saved to the folders you choose. Logs in the same folder
may contain download URLs and error messages. They are not uploaded anywhere.

### Native messaging and localhost bridge

The extension talks to the desktop app through Chrome native messaging
(`com.rapidfetch.host`) or a local bridge on `127.0.0.1`. This connection stays
on your computer.

## 3. Network connections

RapidFetch connects only to:

- the download addresses you choose;
- the local RapidFetch desktop app;
- yt-dlp, ffmpeg, or similar tools' official download locations when you ask
  the app to install or update those tools;
- video sites you choose to resolve through yt-dlp.

Those sites and tools have their own privacy policies. RapidFetch does not
proxy or monitor your traffic.

If you enable **Cookies from browser** for the optional yt-dlp integration,
RapidFetch passes cookies from your chosen browser profile to the local yt-dlp
process so it can access a video you are allowed to download. The cookies are
not sent to us.

## 4. Data sharing

We do not sell, rent, or share personal data. We have no server that receives
your downloads, browsing history, or extension activity.

The only third-party processing is performed by services you already use, such
as Chrome sync for extension settings, or by the websites and tools you choose
to access.

## 5. Data retention and deletion

- **Extension:** remove the extension in `chrome://extensions` to delete its
  local and synced settings. You can also clear its data from the extension
  details page.
- **Desktop app:** use the app to remove download records, or delete
  `%APPDATA%\RapidFetch` after closing the app. Uninstalling the program keeps
  this folder so your settings and history are not lost; delete it manually if
  you want a full reset.
- **Downloaded files:** deleting a history record does not delete the file
  unless you choose **Also delete files from storage** in the remove dialog.

## 6. Permissions used by the extension

| Permission | Why it is needed |
| --- | --- |
| `downloads` | Capture browser downloads and provide pause, resume, cancel, and reveal actions. |
| `storage` | Save settings and tab-specific media state. |
| `contextMenus` | Add the **Download with RapidFetch** right-click menu. |
| `webRequest` | Detect media responses, content types, and sizes while you browse. |
| `tabs` | Read the active tab and keep media state per tab. |
| `notifications` | Show download progress and completion actions. |
| `scripting` | Read media and link URLs from the current page when you request it. |
| `nativeMessaging` | Communicate with the RapidFetch desktop app. |
| `<all_urls>` | Detect downloadable media on any site, because media can be served from any domain. |

The extension does not modify page content, inject ads, or change your search
engine or new-tab page.

## 7. Children

RapidFetch is not directed at children under 13. We do not knowingly collect
data from anyone.

## 8. Changes

If this policy changes, the new version will be published in the repository and
the effective date will be updated. Material changes will be noted in the
release notes.

## 9. Contact

For privacy questions or data requests, open an issue at:

https://github.com/sohanur999999/rapidfetch/issues

## 10. Chrome Web Store Limited Use

RapidFetch's use of information received from Chrome APIs complies with the
Chrome Web Store User Data Policy, including the Limited Use requirements. Data
is used only to provide or improve the extension's single purpose: capturing
downloads and media and handing them to the RapidFetch desktop app on the same
computer.
