/* RapidFetch browser bridge — MV3 service worker.
 * Talks to the desktop app through the native messaging host
 * (com.rapidfetch.host) and falls back to the localhost bridge.
 */

const HOST_NAME = "com.rapidfetch.host";
const DEFAULT_PORT = 55123;
const DEFAULT_SETTINGS = {
  port: DEFAULT_PORT,
  captureMode: "auto",       // auto | ask | off
  captureMedia: true,
  notify: true,
  connections: 8,
  badge: true,
  floatingButton: true,
  skipHosts: ""
};

const MEDIA_RE = /\.(mp4|m4v|mkv|webm|mov|avi|flv|ts|m4s|m3u8|mpd|mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i;
const FILE_RE = /\.(zip|rar|7z|tar|gz|bz2|xz|iso|exe|msi|msix|appx|apk|dmg|deb|rpm|jar|pdf|docx?|xlsx?|pptx?|epub|txt|csv|mp3|flac|wav|aac|ogg|m4a|opus|wma|mp4|mkv|avi|mov|webm|flv|ts|m4v|wmv|jpg|jpeg|png|gif|bmp|webp|svg|tiff|ico)(?:[?#]|$)/i;

// Chrome can replay download events for files that existed before this service
// worker session started (most noticeably right after loading the extension).
// Anything older than this is treated as history, not a new download.
const RECENT_DOWNLOAD_MS = 2 * 60 * 1000;

// URLs handed to the desktop app in the last few minutes. Chrome occasionally
// re-announces a restored download with a fresh start time, so the timestamp
// guard alone is not always enough.
const CAPTURE_HISTORY_KEY = "captureHistory";
const CAPTURE_HISTORY_MS = 10 * 60 * 1000;

/* ------------------------------------------------------------------ settings */

async function getSettings() {
  const stored = await chrome.storage.sync.get(DEFAULT_SETTINGS);
  return { ...DEFAULT_SETTINGS, ...stored };
}

function skipHostList(settings) {
  return String(settings.skipHosts || "")
    .split(/[\s,]+/)
    .map((s) => s.trim().toLowerCase())
    .filter(Boolean);
}

function isSkipped(url, settings) {
  try {
    const host = new URL(url).hostname.toLowerCase();
    return skipHostList(settings).some((h) => host === h || host.endsWith("." + h));
  } catch {
    return false;
  }
}

/* ------------------------------------------------------- native messaging */

let nativePort = null;
let nativePending = [];
let nativeState = "unknown";
let nativeCheckedAt = 0;

function getNativePort() {
  if (nativePort) return nativePort;

  const port = chrome.runtime.connectNative(HOST_NAME);
  nativePort = port;

  port.onMessage.addListener((message) => {
    const waiter = nativePending.shift();
    nativeState = "online";
    nativeCheckedAt = Date.now();
    if (waiter) {
      clearTimeout(waiter.timer);
      waiter.resolve(message);
    }
  });

  port.onDisconnect.addListener(() => {
    const error = chrome.runtime.lastError?.message || "Native host disconnected";
    const waiters = nativePending;
    nativePending = [];
    nativePort = null;
    nativeState = "offline";
    nativeCheckedAt = Date.now();
    for (const waiter of waiters) {
      clearTimeout(waiter.timer);
      waiter.reject(new Error(error));
    }
  });

  return port;
}

function callNative(payload, timeoutMs = 25000) {
  return new Promise((resolve, reject) => {
    let port;
    try {
      port = getNativePort();
    } catch (error) {
      reject(error);
      return;
    }

    const waiter = {
      resolve,
      reject,
      timer: setTimeout(() => {
        const index = nativePending.indexOf(waiter);
        if (index >= 0) nativePending.splice(index, 1);
        nativeState = "offline";
        try { port.disconnect(); } catch { /* ignore */ }
        nativePort = null;
        reject(new Error("The desktop app did not answer in time."));
      }, timeoutMs)
    };

    nativePending.push(waiter);
    try {
      port.postMessage(payload);
    } catch (error) {
      const index = nativePending.indexOf(waiter);
      if (index >= 0) nativePending.splice(index, 1);
      clearTimeout(waiter.timer);
      reject(error);
    }
  });
}

/* ------------------------------------------------------------- HTTP bridge */

async function port() {
  const settings = await getSettings();
  return Number(settings.port) || DEFAULT_PORT;
}

async function callHttp(path, method = "POST", body = null, timeoutMs = 25000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(`http://127.0.0.1:${await port()}${path}`, {
      method,
      headers: body ? { "Content-Type": "application/json" } : undefined,
      body: body ? JSON.stringify(body) : undefined,
      signal: controller.signal
    });
    const text = await response.text();
    const data = text ? JSON.parse(text) : {};
    if (!response.ok) throw new Error(data.error || `HTTP ${response.status}`);
    return data;
  } finally {
    clearTimeout(timer);
  }
}

const ENDPOINTS = {
  add: "/add",
  formats: "/formats",
  pause: "/pause",
  resume: "/resume",
  remove: "/remove",
  reveal: "/reveal",
  list: "/status",
  ping: "/ping",
  show: "/show"
};

/** Send a command to the desktop app, native host first, HTTP second. */
async function api(command, payload = {}, timeoutMs = 25000) {
  const nativePayload = { cmd: command, ...payload };
  let nativeError = null;

  try {
    const result = await callNative(nativePayload, timeoutMs);
    if (result && result.ok !== false) {
      nativeState = "online";
      nativeCheckedAt = Date.now();
      return result;
    }
    nativeError = result?.error || "The desktop app returned an error.";
    nativeState = result ? "offline" : "unknown";
  } catch (error) {
    nativeError = error.message;
  }

  const path = ENDPOINTS[command] || "/status";
  try {
    const result = await callHttp(path, command === "list" || command === "ping" ? "GET" : "POST",
      command === "list" || command === "ping" ? null : payload, timeoutMs);
    if (result && result.ok !== false) {
      nativeState = "online";
      nativeCheckedAt = Date.now();
      return result;
    }
    throw new Error(result?.error || "The desktop app returned an error.");
  } catch (error) {
    nativeState = "offline";
    nativeCheckedAt = Date.now();
    throw new Error(nativeError ? `${nativeError}` : error.message);
  }
}

async function connectionState() {
  if (nativeState === "online" && Date.now() - nativeCheckedAt < 8000) return "online";
  try {
    await api("ping");
    return "online";
  } catch {
    return "offline";
  }
}

/** What the desktop app can do right now. The video-site button is only shown
 *  when the app reports a working yt-dlp engine. */
let capabilities = { ytdlp: false, ffmpeg: false, videoSites: false, quality: "best", checkedAt: 0 };

async function appCapabilities(force = false) {
  if (!force && Date.now() - capabilities.checkedAt < 15000) return capabilities;
  try {
    const result = await api("ping");
    capabilities = {
      ytdlp: !!result?.ytdlp,
      ffmpeg: !!result?.ffmpeg,
      videoSites: result?.videoSites !== false,
      quality: result?.quality || "best",
      checkedAt: Date.now()
    };
  } catch {
    capabilities = { ...capabilities, checkedAt: Date.now() };
  }
  return capabilities;
}

/* ------------------------------------------------------------------ helpers */

function baseName(value) {
  if (!value) return undefined;
  const clean = String(value).split(/[\\/]/).pop().split("?")[0].split("#")[0];
  return clean || undefined;
}

function kindFor(url, contentType = "") {
  const type = String(contentType).toLowerCase();
  if (type.includes("mpegurl") || /\.m3u8(?:[?#]|$)/i.test(url)) return "stream";
  if (type.includes("dash+xml") || /\.mpd(?:[?#]|$)/i.test(url)) return "stream";
  if (type.startsWith("video/") || /\.(mp4|m4v|mkv|webm|mov|avi|flv|ts|m4s)(?:[?#]|$)/i.test(url)) return "video";
  if (type.startsWith("audio/") || /\.(mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i.test(url)) return "audio";
  if (type.startsWith("image/") || /\.(jpg|jpeg|png|gif|webp|svg|bmp|avif)(?:[?#]|$)/i.test(url)) return "image";
  return null;
}

function isStreamUrl(url) {
  return /\.(m3u8|mpd)(?:[?#]|$)/i.test(url);
}

/** RapidFetch can fetch direct files and HLS. DASH manifests/fragments and
 *  YouTube's segmented googlevideo URLs need muxing, so they are not offered. */
function isSupportedMediaUrl(url) {
  if (!url || !/^https?:/i.test(url)) return false;
  if (/\.(mpd|m4s)(?:[?#]|$)/i.test(url)) return false;
  try {
    const host = new URL(url).hostname.toLowerCase();
    if (host === "googlevideo.com" || host.endsWith(".googlevideo.com")) return false;
  } catch { /* ignore */ }
  return true;
}

async function addDownload(url, options = {}) {
  if (!url) throw new Error("Nothing to download.");
  const settings = await getSettings();
  const payload = { url };
  if (options.referrer) payload.referrer = options.referrer;
  if (options.fileName) payload.fileName = baseName(options.fileName);
  if (options.category) payload.category = options.category;
  if (options.quality) payload.quality = options.quality;
  if (options.connections || settings.connections) payload.connections = options.connections || settings.connections;
  if (options.queue) payload.queue = options.queue;
  if (options.source) payload.source = options.source;
  return api("add", payload);
}

/** True when this URL was already handed over a moment ago. */
async function recentlyCaptured(url) {
  try {
    const stored = (await chrome.storage.local.get(CAPTURE_HISTORY_KEY))[CAPTURE_HISTORY_KEY] || {};
    const stamp = Number(stored[url]) || 0;
    return Date.now() - stamp < CAPTURE_HISTORY_MS;
  } catch {
    return false;
  }
}

async function rememberCapture(url) {
  try {
    const stored = (await chrome.storage.local.get(CAPTURE_HISTORY_KEY))[CAPTURE_HISTORY_KEY] || {};
    const cutoff = Date.now() - 24 * 60 * 60 * 1000;
    for (const [key, value] of Object.entries(stored)) {
      if (Number(value) < cutoff) delete stored[key];
    }
    stored[url] = Date.now();
    await chrome.storage.local.set({ [CAPTURE_HISTORY_KEY]: stored });
  } catch { /* ignore */ }
}

async function addMany(urls, options = {}) {
  const results = [];
  let added = 0;
  let failed = 0;
  for (const url of urls.slice(0, 200)) {
    try {
      await addDownload(url, options);
      added++;
      results.push({ url, ok: true });
    } catch (error) {
      failed++;
      results.push({ url, ok: false, error: error.message });
    }
  }
  return { ok: failed === 0, added, failed, results };
}

function notify(title, message) {
  getSettings().then((settings) => {
    if (!settings.notify) return;
    chrome.notifications.create("rf-" + Date.now(), {
      type: "basic",
      iconUrl: "icons/icon128.png",
      title,
      message
    });
  });
}

/* -------------------------------------------------------------------- media */

const mediaQueue = { chain: Promise.resolve() };

function recordMedia(tabId, entry, pageUrl) {
  if (tabId === undefined || tabId < 0 || !entry?.url) return Promise.resolve();
  if (!isSupportedMediaUrl(entry.url)) return Promise.resolve();
  const key = "media:" + tabId;

  mediaQueue.chain = mediaQueue.chain.then(async () => {
    const stored = (await chrome.storage.session.get(key))[key] || { items: [], pageUrl: pageUrl || "" };
    if (stored.items.some((item) => item.url === entry.url)) return;

    const item = {
      url: entry.url,
      kind: entry.kind || kindFor(entry.url, entry.contentType) || "video",
      title: entry.title || "",
      size: entry.size || 0,
      contentType: entry.contentType || "",
      foundAt: Date.now()
    };
    stored.items.unshift(item);
    if (stored.items.length > 60) stored.items.length = 60;
    if (pageUrl) stored.pageUrl = pageUrl;
    await chrome.storage.session.set({ [key]: stored });
    updateBadge(tabId, stored.items.length);
    void syncTab(tabId);
  }).catch(() => {});

  return mediaQueue.chain;
}

async function syncTab(tabId) {
  try {
    const stored = await mediaForTab(tabId);
    await chrome.tabs.sendMessage(tabId, {
      type: "rf:media",
      items: stored.items,
      pageUrl: stored.pageUrl
    });
  } catch { /* no content script or tab is gone */ }
}

async function mediaForTab(tabId) {
  const key = "media:" + tabId;
  const stored = (await chrome.storage.session.get(key))[key] || { items: [], pageUrl: "" };
  return stored;
}

async function updateBadge(tabId, count) {
  const settings = await getSettings();
  if (!settings.badge) {
    await chrome.action.setBadgeText({ tabId, text: "" });
    return;
  }
  try {
    await chrome.action.setBadgeBackgroundColor({ tabId, color: "#3B82F6" });
    await chrome.action.setBadgeText({ tabId, text: count > 0 ? String(count) : "" });
  } catch { /* tab may be gone */ }
}

async function broadcastSettings() {
  const settings = await getSettings();
  try {
    const tabs = await chrome.tabs.query({});
    await Promise.allSettled(tabs.map((tab) => tab.id === undefined
      ? Promise.resolve()
      : chrome.tabs.sendMessage(tab.id, { type: "rf:settings", settings })));
  } catch { /* ignore */ }
}

async function collectFromTab(tabId) {
  try {
    const direct = await chrome.tabs.sendMessage(tabId, { type: "collect" });
    if (direct) return direct;
  } catch { /* no content script, fall through */ }

  try {
    const [result] = await chrome.scripting.executeScript({ target: { tabId }, func: collectInPage });
    return result?.result || { media: [], links: [] };
  } catch {
    return { media: [], links: [] };
  }
}

/* Runs inside the page. Must stay self-contained. */
function collectInPage() {
  const mediaRe = /\.(mp4|m4v|mkv|webm|mov|avi|flv|ts|m4s|m3u8|mpd|mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i;

  const media = [];
  const seen = new Set();
  const push = (url, kind, title) => {
    if (!url || !/^https?:/i.test(url) || seen.has(url)) return;
    seen.add(url);
    media.push({ url, kind: kind || (/\.(mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i.test(url) ? "audio" : "video"), title: title || "" });
  };

  for (const element of document.querySelectorAll("video, audio")) {
    const kind = element.tagName === "VIDEO" ? "video" : "audio";
    push(element.currentSrc || element.src, kind, element.title || document.title);
    for (const source of element.querySelectorAll("source")) push(source.src, kind, "");
  }

  try {
    for (const entry of performance.getEntriesByType("resource")) {
      const url = entry.name;
      if (mediaRe.test(url)) push(url, /\.(m3u8|mpd)(?:[?#]|$)/i.test(url) ? "stream" : undefined, "");
    }
  } catch { /* ignore */ }

  const links = [];
  const seenLinks = new Set();
  for (const anchor of document.querySelectorAll("a[href]")) {
    const url = anchor.href;
    if (!url || !/^https?:/i.test(url) || seenLinks.has(url)) continue;
    seenLinks.add(url);
    links.push({ url, text: (anchor.textContent || "").trim().slice(0, 160) });
  }

  return { media, links, pageUrl: location.href, title: document.title };
}

/* --------------------------------------------------------- request routing */

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  (async () => {
    try {
      switch (message.type) {
        case "ping":
          return sendResponse({ ok: true, state: await connectionState() });

        case "capabilities": {
          const caps = await appCapabilities();
          return sendResponse({ ok: true, ...caps });
        }

        case "status": {
          const state = await connectionState().catch(() => "offline");
          try {
            const data = await api("list");
            return sendResponse({ ok: true, state: "online", data });
          } catch (error) {
            return sendResponse({ ok: false, state, error: error.message });
          }
        }

        case "add": {
          const result = await addDownload(message.url, message);
          return sendResponse({ ok: true, result });
        }

        case "formats": {
          try {
            const result = await api("formats", { url: message.url }, 60000);
            return sendResponse({ ok: true, ...result });
          } catch (error) {
            return sendResponse({ ok: false, error: error.message });
          }
        }

        case "add-many": {
          const result = await addMany(message.urls || [], message);
          return sendResponse({ ok: true, result });
        }

        case "media:list": {
          const tabId = message.tabId ?? sender.tab?.id;
          if (tabId === undefined) return sendResponse({ ok: true, items: [] });
          let stored = await mediaForTab(tabId);
          if (message.collect !== false) {
            const collected = await collectFromTab(tabId);
            for (const item of collected.media || []) {
              await recordMedia(tabId, item, collected.pageUrl);
            }
            stored = await mediaForTab(tabId);
          }
          return sendResponse({ ok: true, ...stored });
        }

        case "media:sync": {
          const tabId = sender.tab?.id ?? message.tabId;
          if (tabId === undefined) return sendResponse({ ok: false, items: [] });
          const stored = await mediaForTab(tabId);
          return sendResponse({ ok: true, ...stored });
        }

        case "media:clear": {
          const tabId = sender.tab?.id ?? message.tabId;
          if (tabId === undefined) return sendResponse({ ok: false });
          await chrome.storage.session.remove("media:" + tabId);
          await updateBadge(tabId, 0);
          return sendResponse({ ok: true });
        }

        case "media:found": {
          const tabId = sender.tab?.id;
          for (const item of message.items || []) {
            await recordMedia(tabId, item, message.pageUrl);
          }
          return sendResponse({ ok: true });
        }

        case "collect": {
          const tabId = message.tabId ?? sender.tab?.id;
          const collected = tabId !== undefined ? await collectFromTab(tabId) : { media: [], links: [] };
          return sendResponse({ ok: true, ...collected });
        }

        case "show":
          try {
            await api("show");
            return sendResponse({ ok: true });
          } catch (error) {
            return sendResponse({ ok: false, error: error.message });
          }

        case "reveal": {
          const result = await api("reveal", { id: message.id });
          return sendResponse({ ok: true, result });
        }

        case "settings:get":
          return sendResponse({ ok: true, settings: await getSettings() });

        case "settings:set":
          await chrome.storage.sync.set(message.settings || {});
          await syncMenus();
          await broadcastSettings();
          return sendResponse({ ok: true });

        case "capture:toggle": {
          const settings = await getSettings();
          const next = settings.captureMode === "off" ? "auto" : "off";
          await chrome.storage.sync.set({ captureMode: next });
          await syncMenus();
          await broadcastSettings();
          notify("RapidFetch", next === "off" ? "Automatic capture is off." : "Automatic capture is on.");
          return sendResponse({ ok: true, captureMode: next });
        }

        default:
          return sendResponse({ ok: false, error: "Unknown message." });
      }
    } catch (error) {
      return sendResponse({ ok: false, error: error.message });
    }
  })();
  return true;
});

/* -------------------------------------------------------- download capture */

const captureSkipped = new Set();

chrome.downloads.onCreated.addListener(async (item) => {
  if (!item.url || !/^https?:/i.test(item.url)) return;
  if (item.incognito) return;
  if (item.byExtensionId) return;
  if (/chrome\.google\.com\/webstore|clients2\.google\.com|update\.google\.com/i.test(item.url)) return;
  if (captureSkipped.has(item.url)) return;

  // Chrome re-announces downloads from the previous session when the service
  // worker starts. Those carry their original start time; taking them over
  // would download every file in the browser's history all over again.
  const started = Date.parse(item.startTime || "");
  if (Number.isFinite(started) && Date.now() - started > RECENT_DOWNLOAD_MS) return;

  // Same file already sent to the desktop app moments ago (Chrome sometimes
  // restarts a restored download with a new start time).
  if (await recentlyCaptured(item.url)) return;

  const settings = await getSettings();
  if (settings.captureMode === "off") return;
  if (isSkipped(item.url, settings)) return;

  const fileName = baseName(item.filename) || baseName(item.url);
  const payload = {
    url: item.url,
    referrer: item.referrer || undefined,
    fileName,
    category: undefined,
    connections: settings.connections,
    source: "capture"
  };

  if (settings.captureMode === "ask") {
    try {
      await chrome.downloads.pause(item.id);
    } catch { /* completed too fast — fall through to auto-capture */ }

    const notificationId = "capture-" + item.id;
    await chrome.storage.session.set({
      [notificationId]: { downloadId: item.id, payload }
    });

    chrome.notifications.create(notificationId, {
      type: "basic",
      iconUrl: "icons/icon128.png",
      title: "Download with RapidFetch?",
      message: fileName || item.url,
      buttons: [{ title: "Download with RapidFetch" }, { title: "Keep in Chrome" }],
      requireInteraction: true
    });
    return;
  }

  await takeOver(item.id, payload);
});

async function takeOver(downloadId, payload) {
  try { await chrome.downloads.cancel(downloadId); } catch { /* ignore */ }
  try { await chrome.downloads.erase({ id: downloadId }); } catch { /* ignore */ }

  try {
    const result = await addDownload(payload.url, payload);
    await rememberCapture(payload.url);
    // The desktop app already has this download (completed or in progress):
    // stay quiet instead of firing a notification for a history replay.
    if (!result?.duplicate) notify("Sent to RapidFetch", payload.fileName || payload.url);
  } catch (error) {
    captureSkipped.add(payload.url);
    notify("RapidFetch is not reachable", error.message);
    try {
      await chrome.downloads.download({ url: payload.url, filename: payload.fileName });
    } catch { /* ignore */ }
  }
}

chrome.notifications.onButtonClicked.addListener(async (notificationId, buttonIndex) => {
  if (!notificationId.startsWith("capture-")) return;
  const key = notificationId;
  const stored = (await chrome.storage.session.get(key))[key];
  chrome.notifications.clear(notificationId);
  if (!stored) return;

  if (buttonIndex === 0) {
    await takeOver(stored.downloadId, stored.payload);
  } else {
    try { await chrome.downloads.resume(stored.downloadId); } catch { /* ignore */ }
  }
  await chrome.storage.session.remove(key);
});

chrome.notifications.onClicked.addListener(async (notificationId) => {
  if (!notificationId.startsWith("capture-")) return;
  const stored = (await chrome.storage.session.get(notificationId))[notificationId];
  chrome.notifications.clear(notificationId);
  if (!stored) return;
  await takeOver(stored.downloadId, stored.payload);
  await chrome.storage.session.remove(notificationId);
});

/* ----------------------------------------------------------- media sniffing */

chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    if (details.tabId < 0 || details.type !== "media") return;
    recordMedia(details.tabId, { url: details.url, kind: kindFor(details.url) || "video" });
  },
  { urls: ["<all_urls>"], types: ["media"] }
);

chrome.webRequest.onHeadersReceived.addListener(
  (details) => {
    if (details.tabId < 0) return;
    if (!["media", "xmlhttprequest", "other"].includes(details.type)) return;

    const headers = details.responseHeaders || [];
    const contentType = headers.find((header) => header.name.toLowerCase() === "content-type")?.value || "";
    const lengthHeader = headers.find((header) => header.name.toLowerCase() === "content-length")?.value;
    const kind = kindFor(details.url, contentType);
    if (!kind || kind === "image") return;
    if (!MEDIA_RE.test(details.url) && !/^(video|audio)\//i.test(contentType) && !/mpegurl|dash\+xml/i.test(contentType)) return;

    recordMedia(details.tabId, {
      url: details.url,
      kind,
      contentType,
      size: lengthHeader ? Number(lengthHeader) || 0 : 0
    });
  },
  { urls: ["<all_urls>"], types: ["media", "xmlhttprequest", "other"] },
  ["responseHeaders"]
);

chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "sync") void broadcastSettings();
});

chrome.tabs.onUpdated.addListener(async (tabId, changeInfo) => {
  if (changeInfo.status === "loading") {
    await chrome.storage.session.remove("media:" + tabId);
    await updateBadge(tabId, 0);
  } else if (changeInfo.status === "complete") {
    const settings = await getSettings();
    if (!settings.captureMedia) return;
    setTimeout(async () => {
      const collected = await collectFromTab(tabId);
      for (const item of collected.media || []) {
        await recordMedia(tabId, item, collected.pageUrl);
      }
      void syncTab(tabId);
    }, 2500);
  }
});

chrome.tabs.onRemoved.addListener((tabId) => {
  chrome.storage.session.remove("media:" + tabId);
});

/* ------------------------------------------------------------ context menus */

const MENU_ITEMS = [
  { id: "rf-link", title: "Download with RapidFetch", contexts: ["link"] },
  { id: "rf-media", title: "Download with RapidFetch", contexts: ["video", "audio"] },
  { id: "rf-image", title: "Download image with RapidFetch", contexts: ["image"] },
  { id: "rf-selection", title: "Download links in selection", contexts: ["selection"] },
  { id: "rf-sep-1", type: "separator", contexts: ["page", "link", "video", "audio", "image", "selection"] },
  { id: "rf-page-links", title: "Download all links with RapidFetch", contexts: ["page"] },
  { id: "rf-page-media", title: "Download media on this page", contexts: ["page"] },
  { id: "rf-sep-2", type: "separator", contexts: ["page", "link", "video", "audio", "image"] },
  { id: "rf-toggle", title: "Turn automatic capture off", contexts: ["page", "link", "video", "audio", "image"] },
  { id: "rf-options", title: "RapidFetch options…", contexts: ["page", "link", "video", "audio", "image"] }
];

async function createMenus() {
  await chrome.contextMenus.removeAll();
  for (const item of MENU_ITEMS) chrome.contextMenus.create(item);
  await syncMenus();
}

async function syncMenus() {
  const settings = await getSettings();
  try {
    await chrome.contextMenus.update("rf-toggle", {
      title: settings.captureMode === "off"
        ? "Turn automatic capture on"
        : "Turn automatic capture off"
    });
  } catch { /* menu not created yet */ }
}

chrome.runtime.onInstalled.addListener(createMenus);
chrome.runtime.onStartup.addListener(createMenus);
createMenus();

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  const tabId = tab?.id;

  try {
    switch (info.menuItemId) {
      case "rf-link":
        if (info.linkUrl) await addDownload(info.linkUrl, { referrer: info.pageUrl });
        notify("Sent to RapidFetch", baseName(info.linkUrl) || info.linkUrl);
        break;

      case "rf-media":
      case "rf-image":
        if (info.srcUrl) await addDownload(info.srcUrl, { referrer: info.pageUrl });
        notify("Sent to RapidFetch", baseName(info.srcUrl) || info.srcUrl);
        break;

      case "rf-selection": {
        const urls = String(info.selectionText || "")
          .split(/[\s<>"'()]+/)
          .filter((value) => /^https?:\/\//i.test(value));
        if (urls.length === 0) {
          notify("RapidFetch", "No links found in the selection.");
          break;
        }
        const result = await addMany(urls, { referrer: info.pageUrl });
        notify("RapidFetch", `Queued ${result.added} download${result.added === 1 ? "" : "s"}.`);
        break;
      }

      case "rf-page-links":
      case "rf-page-media": {
        if (tabId === undefined) return;
        const mode = info.menuItemId === "rf-page-media" ? "media" : "links";
        await chrome.tabs.create({
          url: chrome.runtime.getURL(`picker.html?tabId=${tabId}&mode=${mode}`)
        });
        break;
      }

      case "rf-toggle": {
        const settings = await getSettings();
        const next = settings.captureMode === "off" ? "auto" : "off";
        await chrome.storage.sync.set({ captureMode: next });
        await syncMenus();
        notify("RapidFetch", next === "off" ? "Automatic capture is off." : "Automatic capture is on.");
        break;
      }

      case "rf-options":
        chrome.runtime.openOptionsPage();
        break;
    }
  } catch (error) {
    notify("RapidFetch", error.message);
  }
});

chrome.commands.onCommand.addListener(async (command) => {
  if (command !== "toggle-capture") return;
  const settings = await getSettings();
  const next = settings.captureMode === "off" ? "auto" : "off";
  await chrome.storage.sync.set({ captureMode: next });
  await syncMenus();
  notify("RapidFetch", next === "off" ? "Automatic capture is off." : "Automatic capture is on.");
});

chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "sync" && changes.captureMode) syncMenus();
});

/* Helpers used by picker.html */
chrome.runtime.onConnect.addListener((port) => {
  if (port.name !== "rapidfetch-picker") return;
  port.onMessage.addListener(async (message) => {
    if (message.type === "collect") {
      port.postMessage({ type: "collected", data: await collectFromTab(message.tabId) });
    } else if (message.type === "add-many") {
      port.postMessage({ type: "added", data: await addMany(message.urls, message.options || {}) });
    }
  });
});
