# Building a free IDM alternative with .NET and a Manifest V3 extension

*Draft for dev.to, Hashnode, a GitHub discussion, or a personal blog. Replace
`[CANONICAL URL]` with the published location before cross-posting. Add the
canonical link when republishing elsewhere.*

## Why another download manager

Browser downloads are deliberately boring: one connection, one progress bar, one
"failed — network changed" at 90%. For a 20 MB installer that is fine. For a 4 GB
archive on a flaky connection, it is a small tragedy.

The commercial download managers solved this years ago, then wrapped the useful
parts in a trial clock, a registration key, a browser extension with a store
listing, and an upgrade nag. I wanted the useful parts without the rest. So I
built RapidFetch: a Windows 10/11 download manager with a native Manifest V3
Chrome extension, MIT-licensed, no account, no telemetry, and free for life.

This article is about the engineering, not the launch. The interesting problems
were HTTP range probing, restart-safe resume, segment scheduling under a
connection cap, and making a browser extension talk to a desktop app without a
server.

## The shape of the system

```
Chrome (MV3 extension)
  ├─ chrome.downloads interception
  ├─ context menus, link capture
  ├─ media/HLS detection
  └─ native messaging host (localhost HTTP fallback)
          │
          ▼
RapidFetch.Core (.NET 10)
  ├─ HttpProbe        range support + metadata
  ├─ DownloadEngine   segments, retry, resume, rate limits
  ├─ Scheduler        windows, daily caps, finish actions
  └─ storage          .part file + segment map
          │
          ▼
RapidFetch.App (WPF)
  └─ queue, categories, settings, tray, site grabber
```

The core library has no UI dependency. The WPF shell is one consumer; the
extension never touches the file system directly. That separation made the
engine testable without launching a window.

## 1. Probe before promising acceleration

Not every server supports range requests. Some return `200` and ignore the
`Range` header; some return `206` for one range but fail on many; some advertise
`Accept-Ranges: bytes` and then return the whole file.

RapidFetch starts with a one-byte range request:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, url);
request.Headers.Range = new RangeHeaderValue(0, 0);

using var response = await client.SendAsync(
    request, HttpCompletionOption.ResponseHeadersRead, ct);

bool acceptsRanges =
    response.StatusCode == HttpStatusCode.PartialContent &&
    response.Content.Headers.ContentRange?.Length is > 0;
```

Only when the server returns `206 Partial Content` with a usable total length
does the engine split the file. Otherwise it downloads a single stream. The UI
says which mode it is in; it does not pretend a non-range server is being
accelerated.

Two details that matter in practice:

- **Total length can be missing.** A chunked response may not expose
  `Content-Length`. Without a total, segment boundaries cannot be computed, so
  the engine stays on one stream.
- **The probe is not a guarantee.** A server can behave differently for
  subsequent requests. Segment requests handle `416` and unexpected `200`
  responses by falling back or retrying rather than corrupting the output.

## 2. Segments, a preallocated file, and a map

The naive segmented downloader opens N HTTP connections and appends whatever
arrives to one file. That works until two segments finish out of order, or the
process dies mid-write.

RapidFetch preallocates the destination `.part` file to the total length, then
gives each segment a byte range and an offset in that file. Each worker seeks to
its own offset and writes only its range. No segment can overwrite another.

A segment map records the state of every range:

```json
{
  "total": 4294967296,
  "connections": 16,
  "segments": [
    { "start": 0, "end": 268435455, "position": 268435455, "done": true },
    { "start": 268435456, "end": 536870911, "position": 402653184, "done": false }
  ]
}
```

`position` is the next byte to request, not the last byte written. On resume the
engine reopens the `.part` file, reads the map, and issues a new range request
from each unfinished segment's `position`. The bytes already on disk are never
re-downloaded.

This is also why "pause" is cheap: pause stops the workers and flushes the map;
it does not discard the file or restart the transfer.

### Retries belong to the segment, not the file

A single failed request should not fail a 4 GB download. Each segment has its own
retry loop with exponential backoff and a per-attempt timeout. If one segment
keeps failing, the engine can reduce the connection count and retry the remaining
ranges instead of throwing away everything.

Rate limiting is applied at the read loop, not by sleeping between requests:
each worker reads a chunk, asks a shared limiter for permission, and writes. That
keeps the cap honest across 32 concurrent streams.

## 3. The browser bridge

The extension is intentionally thin. It detects a download and hands a URL plus
metadata to the desktop app; the app owns the transfer. That keeps the extension
reviewable and means a browser crash does not kill a download.

Manifest V3 removed background pages, so the extension uses a service worker and
`chrome.downloads.onDeterminingFilename` / `chrome.downloads.onCreated` to
intercept. Native messaging is the primary transport; a localhost HTTP bridge is
the fallback when the native host is not registered. Both are local-only.

Media detection is the part users notice. The extension watches responses for
direct `video/*` and `audio/*` content and for HLS playlists
(`application/vnd.apple.mpegurl`, `.m3u8`). HLS is a playlist of segments, not a
file, so the app resolves the variant playlist and downloads the segments in
order (or with limited concurrency), then joins them.

What the built-in grabber deliberately does **not** do:

- It does not bypass DRM.
- It filters DASH and separate audio/video tracks from the built-in path, because
  muxing them correctly needs a different pipeline.
- It offers optional yt-dlp integration for pages where the user has the right to
  save the media and the tracks need muxing.

That last point is a product decision as much as a technical one: a download
manager should be clear about what it can and cannot do.

## 4. Scheduling and the unglamorous parts

A download queue is mostly state management:

- concurrency limit across active downloads;
- a daily transfer cap that resets at local midnight;
- a time window that can wrap past midnight (22:00–06:00);
- finish actions: exit, sleep, hibernate, shut down;
- categories and a destination rule;
- a site grabber for same-origin links;
- speed limits that apply per download and globally.

None of this is novel. It is the difference between a demo and something a person
leaves running overnight.

## 5. What I would do differently

**Probe more aggressively.** A one-byte range request is cheap, but some CDNs
behave differently for `HEAD`, `GET` with `Range: bytes=0-0`, and a full `GET`.
A small compatibility matrix would have saved debugging time.

**Model the segment map as append-only.** Rewriting the whole JSON map on every
checkpoint is simple but wasteful at 32 connections. A append-only log plus a
periodic compaction would be more robust and cheaper.

**Separate "paused" from "interrupted" in the UI.** They share the same engine
state but mean different things to a user. The current build shows the same
status for both; a clearer distinction is on the list.

**Build the installer signing into the release from day one.** An unsigned
installer triggers SmartScreen, and no amount of documentation makes that
warning pleasant. Code signing is a real cost for a free project, but it should
be planned, not discovered at launch.

## The free-for-life boundary

RapidFetch has no trial, account, subscription, activation server or telemetry.
Every current core feature is in the free build. The promise is written in the
README: if that ever changes, the page and release notes will say so before
anything is paywalled. I think that is a better long-term contract than "free
forever" with an asterisk.

The Chrome Web Store listing is still pending because the developer registration
fee is unpaid. Until it is live, the extension installs from the official ZIP via
`chrome://extensions` → Developer mode → Load unpacked. The installer is
unsigned, so Windows may show "Unknown publisher"; SHA-256 checksums and the full
source are on the release page.

## Try it and break it

- Source: <https://github.com/sohanur999999/rapidfetch>
- Latest release: <https://github.com/sohanur999999/rapidfetch/releases/latest>
- Landing page: <https://sohanur999999.github.io/rapidfetch/>

If you work on HTTP clients, segmented transfers or MV3 extensions, I would
value feedback on the edge cases: servers that lie about range support, proxies
that buffer, resume after a hard power loss, and HLS playlists with unusual
discontinuities. Those are the places where a download manager earns its keep.

---

*RapidFetch is MIT-licensed. It is not affiliated with Tonec or Internet Download
Manager. Only download content you own or have permission to save.*
