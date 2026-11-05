/* RapidFetch content script: reports media the DOM already knows about and
 * shows the IDM-style "Download this video" bar with an expandable format list. */

(() => {
  if (window.__rapidFetchInjected) return;
  window.__rapidFetchInjected = true;

  const MEDIA_RE = /\.(mp4|m4v|mkv|webm|mov|avi|flv|ts|m4s|m3u8|mpd|mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i;
  const VIDEO_SITE_RE = /(^|\.)(youtube\.com|youtu\.be|vimeo\.com|dailymotion\.com|dai\.ly|soundcloud\.com|bandcamp\.com|twitch\.tv|bilibili\.com|facebook\.com|fb\.watch)$/i;
  const settings = { floatingButton: true, captureMedia: true };

  let detectedMedia = [];
  let visibleMedia = [];
  let capabilities = { ytdlp: false, videoSites: true, quality: "best" };
  let host = null;
  let ui = null;
  let panelOpen = false;
  let toastTimer = null;
  let dismissedFor = "";
  let capsTimer = null;
  let invalidated = false;
  let observer = null;
  let pending = null;
  const sent = new Set();
  const sending = new Set();
  const formats = { url: "", loading: false, loaded: false, error: "", title: "", items: [] };

  /* --------------------------------------------------------------- helpers */

  function supported(url) {
    if (!url || !/^https?:/i.test(url)) return false;
    if (/\.(mpd|m4s)(?:[?#]|$)/i.test(url)) return false;
    try {
      const hostName = new URL(url).hostname.toLowerCase();
      if (hostName === "googlevideo.com" || hostName.endsWith(".googlevideo.com")) return false;
    } catch { /* not a URL we can inspect */ }
    return true;
  }

  function kindOf(url, hint) {
    if (hint) return hint;
    if (/\.m3u8(?:[?#]|$)/i.test(url)) return "stream";
    if (/\.(mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i.test(url)) return "audio";
    return "video";
  }

  function nameOf(url) {
    try {
      const name = new URL(url).pathname.split("/").filter(Boolean).pop() || "";
      return decodeURIComponent(name) || "media";
    } catch {
      return "media";
    }
  }

  function bytes(value) {
    const size = Number(value) || 0;
    if (size < 1024) return "";
    const units = ["KB", "MB", "GB"];
    let amount = size / 1024;
    let index = 0;
    while (amount >= 1024 && index < units.length - 1) { amount /= 1024; index++; }
    return `${amount >= 100 ? amount.toFixed(0) : amount.toFixed(1)} ${units[index]}`;
  }

  function esc(value) {
    return String(value ?? "").replace(/[&<>"']/g, (c) => (
      { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]
    ));
  }

  function kindLabel(kind) {
    return { video: "Video", audio: "Audio", stream: "HLS stream" }[kind] || "Media";
  }

  function pageKey() {
    return location.href.split("#")[0];
  }

  function cleanTitle() {
    return (document.title || "This page")
      .replace(/\s+-\s+YouTube$/i, "")
      .replace(/\s+/g, " ")
      .trim() || "This page";
  }

  function isInvalidated(message) {
    return /extension context invalidated|message port closed|receiving end does not exist/i
      .test(String(message || ""));
  }

  /** The extension was reloaded or updated; this page still runs the old script. */
  function shutDown() {
    if (invalidated) return;
    invalidated = true;
    clearInterval(capsTimer);
    capsTimer = null;
    clearTimeout(toastTimer);
    toastTimer = null;
    clearTimeout(pending);
    pending = null;
    try { observer?.disconnect(); } catch { /* ignore */ }
    if (host) {
      host.remove();
      host = null;
      ui = null;
    }
    panelOpen = false;
  }

  function send(message) {
    return new Promise((resolve) => {
      if (invalidated) {
        resolve({ ok: false, invalidated: true });
        return;
      }
      try {
        if (!chrome.runtime?.id) {
          shutDown();
          resolve({ ok: false, invalidated: true });
          return;
        }
        chrome.runtime.sendMessage(message, (response) => {
          const error = chrome.runtime.lastError;
          if (error && isInvalidated(error.message)) {
            shutDown();
            resolve({ ok: false, invalidated: true });
            return;
          }
          resolve(error ? { ok: false, error: error.message } : response || { ok: false });
        });
      } catch (error) {
        if (isInvalidated(error.message)) {
          shutDown();
          resolve({ ok: false, invalidated: true });
          return;
        }
        resolve({ ok: false, error: error.message });
      }
    });
  }

  /* -------------------------------------------------------------- collecting */

  function collect() {
    const media = [];
    const seen = new Set();

    const push = (url, hint, title) => {
      if (!supported(url) || seen.has(url)) return;
      seen.add(url);
      media.push({ url, kind: kindOf(url, hint), title: title || "" });
    };

    for (const element of document.querySelectorAll("video, audio")) {
      const hint = element.tagName === "VIDEO" ? "video" : "audio";
      push(element.currentSrc || element.src, hint, element.title || document.title);
      for (const source of element.querySelectorAll("source")) push(source.src, hint, "");
    }

    try {
      for (const entry of performance.getEntriesByType("resource")) {
        if (MEDIA_RE.test(entry.name)) push(entry.name, null, "");
      }
    } catch { /* performance API not available */ }

    const links = [];
    const seenLinks = new Set();
    for (const anchor of document.querySelectorAll("a[href]")) {
      const url = anchor.href;
      if (!url || !/^https?:/i.test(url) || seenLinks.has(url)) continue;
      seenLinks.add(url);
      links.push({
        url,
        text: (anchor.textContent || "").trim().replace(/\s+/g, " ").slice(0, 160)
      });
    }

    return { media, links, pageUrl: location.href, title: document.title };
  }

  function merge(items) {
    const byUrl = new Map(detectedMedia.map((item) => [item.url, item]));
    let changed = false;

    for (const item of items || []) {
      if (!item?.url || !supported(item.url)) continue;
      const existing = byUrl.get(item.url);
      if (existing) {
        if (!existing.title && item.title) { existing.title = item.title; changed = true; }
        if (!existing.size && item.size) { existing.size = item.size; changed = true; }
        continue;
      }
      const entry = {
        key: item.url,
        url: item.url,
        kind: item.kind || kindOf(item.url),
        title: item.title || "",
        size: item.size || 0
      };
      byUrl.set(entry.url, entry);
      detectedMedia.push(entry);
      changed = true;
    }

    const order = { stream: 0, video: 1, audio: 2 };
    detectedMedia = [...byUrl.values()].sort((a, b) =>
      (order[a.kind] ?? 3) - (order[b.kind] ?? 3));
    rebuildVisible();
    if (changed || capabilities.ytdlp) render();
  }

  /** True for a page that yt-dlp can resolve as a single video. */
  function isVideoPageUrl(url) {
    try {
      const parsed = new URL(url);
      const host = parsed.hostname.toLowerCase();
      const path = parsed.pathname.toLowerCase();
      if (host === "fb.watch") return path.length > 1;
      if (host === "facebook.com" || host.endsWith(".facebook.com")) {
        return path.startsWith("/watch") || path.startsWith("/reel/")
          || path.startsWith("/share/v/") || path.startsWith("/share/r/")
          || path.startsWith("/share/reel/") || path.startsWith("/video.php")
          || path.includes("/videos/");
      }
      return VIDEO_SITE_RE.test(host);
    } catch {
      return false;
    }
  }

  /** Pages whose player exposes separate tracks are offered through yt-dlp. */
  function isVideoSitePage() {
    const videoSite = window.__rapidFetchHarnessVideoSite === true ||
      isVideoPageUrl(location.href);
    return capabilities.videoSites !== false && videoSite &&
      /^https?:/i.test(location.href);
  }

  function siteItems() {
    if (!capabilities.ytdlp || !isVideoSitePage()) return [];
    const title = cleanTitle();
    const page = pageKey();
    const quality = capabilities.quality || "best";
    const label = quality === "best" ? "Best available" : `Up to ${quality}p`;
    return [
      {
        key: "page:best|" + page,
        url: page,
        kind: "video",
        quality,
        title,
        subtitle: `${label} · merges audio and video`
      },
      {
        key: "page:audio|" + page,
        url: page,
        kind: "audio",
        quality: "audio",
        title,
        subtitle: "Audio only"
      }
    ];
  }

  function formatItems() {
    if (!formats.loaded || formats.items.length === 0) return [];
    const page = pageKey();
    const title = formats.title || cleanTitle();
    return formats.items.map((entry) => {
      const quality = String(entry.quality || "best");
      const isAudio = entry.kind === "audio" || quality === "audio";
      const ext = String(entry.ext || (isAudio ? "m4a" : "mp4")).toUpperCase();
      const label = entry.label || (isAudio ? "Audio only" : `${quality}p`);
      return {
        key: "fmt|" + page + "|" + quality,
        url: page,
        kind: isAudio ? "audio" : "video",
        quality,
        label,
        title: isAudio ? `${ext} file, audio only` : `${ext} file, quality ${label}`,
        subtitle: [entry.size ? bytes(entry.size) : "", title].filter(Boolean).join(" · "),
        size: Number(entry.size) || 0
      };
    });
  }

  function rebuildVisible() {
    visibleMedia = formatItems();
    if (visibleMedia.length > 0) return;

    // On a page handled by yt-dlp, direct player fragments are not complete
    // downloads. Keep the page-level options until the real format list loads.
    if (isVideoSitePage() && capabilities.ytdlp) {
      visibleMedia = siteItems();
      return;
    }
    visibleMedia = siteItems().concat(detectedMedia);
  }

  function keyOf(item) {
    return item.key || item.url;
  }

  function resetPageIfNeeded() {
    const page = pageKey();
    if (!formats.url || formats.url === page) return;
    formats.url = "";
    formats.loading = false;
    formats.loaded = false;
    formats.error = "";
    formats.title = "";
    formats.items = [];
    panelOpen = false;
    dismissedFor = "";
  }

  async function loadFormats(force = false) {
    if (invalidated || !isVideoSitePage() || !capabilities.ytdlp) return;
    const page = pageKey();
    if (!force && formats.url === page) return;

    formats.url = page;
    formats.loading = true;
    formats.loaded = false;
    formats.error = "";
    formats.title = cleanTitle();
    formats.items = [];
    rebuildVisible();
    render();

    const result = await send({ type: "formats", url: page });
    if (invalidated || formats.url !== page) return;

    formats.loading = false;
    if (result?.ok && Array.isArray(result.formats) && result.formats.length > 0) {
      formats.items = result.formats;
      formats.title = result.title || formats.title;
      formats.loaded = true;
      formats.error = "";
    } else {
      formats.error = result?.error || "Video formats are not available.";
    }

    rebuildVisible();
    render();
    if (formats.error && panelOpen) toast("Formats unavailable. Quick options are shown.", true);
  }

  /* --------------------------------------------------------------------- UI */

  function ensureUi() {
    if (invalidated || host) return;

    host = document.createElement("div");
    host.id = "rapidfetch-floating-host";
    host.style.cssText = "all:initial;position:fixed;inset:0;z-index:2147483646;pointer-events:none;";
    const shadow = host.attachShadow({ mode: "open" });

    shadow.innerHTML = `
      <style>
        :host { all: initial; }
        * { box-sizing: border-box; }
        .wrap {
          position: fixed; top: 74px; right: 16px; display: flex;
          flex-direction: column; align-items: flex-end; gap: 4px;
          max-width: calc(100vw - 24px);
          font-family: "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif;
          font-size: 12px; color: #dbe3ef; pointer-events: none;
        }
        .wrap > * { pointer-events: auto; }
        .bar {
          display: flex; align-items: center; gap: 7px; height: 28px;
          padding: 0 4px 0 7px; border-radius: 7px; cursor: pointer;
          border: 1px solid rgba(255, 255, 255, .16); color: #dbe3ef;
          background: rgba(15, 20, 29, .88);
          box-shadow: 0 8px 22px rgba(0, 0, 0, .45), 0 1px 0 rgba(255, 255, 255, .06) inset;
          backdrop-filter: blur(18px) saturate(120%);
          font: 600 11.5px/1 "Segoe UI Variable Text", "Segoe UI", sans-serif;
          white-space: nowrap; user-select: none;
        }
        .bar:hover { background: rgba(22, 29, 41, .96); border-color: rgba(96, 165, 250, .45); }
        .mark {
          width: 17px; height: 17px; border-radius: 4px; flex: 0 0 auto;
          background: linear-gradient(135deg, #3b82f6, #1d4ed8);
          display: flex; align-items: center; justify-content: center;
        }
        .mark svg { width: 11px; height: 11px; }
        .bar .chev { color: #8a93a5; font-size: 9px; margin-left: 1px; }
        .bar .close {
          border: 0; background: transparent; color: #8a93a5; width: 20px; height: 20px;
          border-radius: 4px; cursor: pointer; font: 14px/1 "Segoe UI", sans-serif;
          display: flex; align-items: center; justify-content: center;
        }
        .bar .close:hover { background: rgba(255, 255, 255, .08); color: #fff; }
        .panel {
          width: 460px; max-width: calc(100vw - 24px);
          max-height: min(430px, calc(100vh - 120px));
          display: none; flex-direction: column; overflow: hidden;
          background: rgba(11, 15, 22, .97); border: 1px solid rgba(255, 255, 255, .14);
          border-radius: 8px;
          box-shadow: 0 24px 60px rgba(0, 0, 0, .65), 0 1px 0 rgba(255, 255, 255, .05) inset;
          backdrop-filter: blur(20px) saturate(115%);
        }
        .panel.open { display: flex; }
        .all {
          display: flex; align-items: center; gap: 7px; width: 100%;
          border: 0; border-bottom: 1px solid rgba(255, 255, 255, .08);
          background: rgba(20, 27, 39, .95); color: #dbe3ef; padding: 8px 10px;
          cursor: pointer; text-align: left;
          font: 600 11.5px/1 "Segoe UI Variable Text", "Segoe UI", sans-serif;
        }
        .all:hover { background: #1a2330; color: #fff; }
        .all svg { width: 13px; height: 13px; color: #60a5fa; flex: 0 0 auto; }
        .list { overflow: auto; padding: 2px 0; }
        .list::-webkit-scrollbar { width: 8px; }
        .list::-webkit-scrollbar-thumb { background: #2b3546; border-radius: 8px; }
        .row {
          display: flex; align-items: center; gap: 8px; width: 100%;
          padding: 6px 10px; border: 0; border-bottom: 1px solid rgba(255, 255, 255, .035);
          background: transparent; color: inherit; cursor: pointer;
          text-align: left; font: inherit;
        }
        .row:hover { background: #151d29; }
        .row.done { opacity: .5; cursor: default; }
        .row .num { width: 17px; flex: 0 0 auto; text-align: right; color: #6b7689; font-size: 10.5px; }
        .row .meta { flex: 1; min-width: 0; }
        .row .meta b {
          display: block; font-size: 11.5px; font-weight: 600; line-height: 1.25; color: #e5ebf4;
          white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .row .meta small {
          display: block; margin-top: 1px; color: #8a96a9; font-size: 10.5px; line-height: 1.25;
          white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .row:hover .meta b { color: #fff; }
        .row .tail { flex: 0 0 auto; min-width: 38px; text-align: right; color: #8a96a9; font-size: 10.5px; }
        .row:hover .tail { color: #93c5fd; }
        .row .ok { color: #34d399; }
        .row .busy { color: #93c5fd; }
        .empty {
          display: flex; align-items: center; gap: 8px;
          padding: 15px 12px; color: #8a96a9; font-size: 11px;
        }
        .spinner {
          width: 12px; height: 12px; flex: 0 0 auto; border-radius: 50%;
          border: 2px solid #334155; border-top-color: #60a5fa;
          animation: spin .8s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }
        .notice {
          display: flex; align-items: center; gap: 8px; padding: 7px 10px;
          border-bottom: 1px solid rgba(255, 255, 255, .06);
          color: #fca5a5; font-size: 10.5px; background: rgba(127, 29, 29, .16);
        }
        .notice span { flex: 1; min-width: 0; }
        .retry {
          flex: 0 0 auto; border: 1px solid rgba(255, 255, 255, .16); border-radius: 5px;
          background: transparent; color: #e5ebf4; padding: 3px 7px; cursor: pointer;
          font: 600 10px/1 "Segoe UI Variable Text", "Segoe UI", sans-serif;
        }
        .retry:hover { background: rgba(255, 255, 255, .08); }
        .toast {
          min-height: 0; padding: 0 11px; color: #9aa6b8; font-size: 10.5px;
          background: #0d121a; transition: min-height .15s ease, padding .15s ease;
        }
        .toast.show { min-height: 26px; padding: 7px 11px; }
        .toast.bad { color: #fca5a5; }
      </style>
      <div class="wrap">
        <div class="bar" role="button" tabindex="0" aria-expanded="false"
             title="Download with RapidFetch">
          <span class="mark">
            <svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.4"
                 stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 3v12m0 0 4.5-4.5M12 15l-4.5-4.5M4 20h16"/>
            </svg>
          </span>
          <span class="label">Download this video</span>
          <span class="chev">▾</span>
          <button class="close" title="Hide for this page">&#x2715;</button>
        </div>
        <div class="panel" part="panel">
          <button class="all" hidden>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"
                 stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 3v12m0 0 4.5-4.5M12 15l-4.5-4.5M4 20h16"/>
            </svg>
            <span>Download all</span>
          </button>
          <div class="list"></div>
          <div class="toast"></div>
        </div>
      </div>`;

    ui = {
      wrap: shadow.querySelector(".wrap"),
      bar: shadow.querySelector(".bar"),
      panel: shadow.querySelector(".panel"),
      label: shadow.querySelector(".bar .label"),
      chev: shadow.querySelector(".bar .chev"),
      close: shadow.querySelector(".bar .close"),
      all: shadow.querySelector(".all"),
      list: shadow.querySelector(".list"),
      toast: shadow.querySelector(".toast")
    };

    ui.bar.addEventListener("click", (event) => {
      if (event.target.closest(".close")) return;
      event.stopPropagation();
      if (!panelOpen && !formats.loaded && !formats.loading &&
          isVideoSitePage() && capabilities.ytdlp) {
        void loadFormats();
      }
      panelOpen = !panelOpen;
      render();
    });

    ui.bar.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") return;
      event.preventDefault();
      ui.bar.click();
    });

    ui.close.addEventListener("click", (event) => {
      event.stopPropagation();
      dismissedFor = pageKey();
      panelOpen = false;
      render();
    });

    ui.all.addEventListener("click", async (event) => {
      event.stopPropagation();
      for (const item of visibleMedia) {
        if (item.kind === "audio") continue;
        if (!sent.has(keyOf(item))) await addOne(item);
      }
    });

    ui.list.addEventListener("click", (event) => {
      event.stopPropagation();
      if (event.target.closest(".retry")) {
        void loadFormats(true);
        return;
      }
      const row = event.target.closest(".row");
      if (!row || row.classList.contains("done")) return;
      const item = visibleMedia.find((entry) => keyOf(entry) === row.dataset.key);
      if (item) void addOne(item);
    });

    shadow.addEventListener("click", (event) => event.stopPropagation());
    document.addEventListener("click", () => {
      if (!panelOpen) return;
      panelOpen = false;
      render();
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && panelOpen) {
        panelOpen = false;
        render();
      }
    });

    let placeFrame = 0;
    const schedulePlace = () => {
      if (placeFrame) return;
      placeFrame = requestAnimationFrame(() => {
        placeFrame = 0;
        place();
      });
    };
    window.addEventListener("resize", schedulePlace);
    document.addEventListener("scroll", schedulePlace, true);

    document.documentElement.appendChild(host);
    render();
  }

  function bestVideoRect() {
    let best = null;
    for (const video of document.querySelectorAll("video")) {
      const rect = video.getBoundingClientRect();
      if (rect.width < 280 || rect.height < 150) continue;
      if (rect.bottom <= 0 || rect.top >= window.innerHeight) continue;
      if (rect.right <= 0 || rect.left >= window.innerWidth) continue;
      if (!best || rect.width * rect.height > best.width * best.height) best = rect;
    }
    return best;
  }

  function place() {
    if (!ui) return;
    let top = 74;
    let right = 16;
    const rect = bestVideoRect();
    if (rect) {
      top = Math.max(64, Math.min(window.innerHeight - 180, rect.top + 2));
      right = Math.max(12, Math.min(window.innerWidth - 320, window.innerWidth - rect.right + 6));
    }
    ui.wrap.style.top = `${Math.round(top)}px`;
    ui.wrap.style.right = `${Math.round(right)}px`;
    const room = Math.max(140, Math.min(430, window.innerHeight - top - 42));
    ui.panel.style.maxHeight = `${Math.round(room)}px`;
  }

  function listHtml() {
    if (formats.loading) {
      return `<div class="empty"><span class="spinner"></span>Looking for formats…</div>`;
    }

    const notice = formats.error
      ? `<div class="notice"><span>${esc(formats.error)}</span><button class="retry">Retry</button></div>`
      : "";

    if (visibleMedia.length === 0) {
      return notice + `<div class="empty">No downloadable media found on this page.</div>`;
    }

    return notice + visibleMedia.map((item, index) => {
      const key = keyOf(item);
      const isSent = sent.has(key);
      const isBusy = sending.has(key);
      const title = item.title || nameOf(item.url);
      const detail = item.subtitle
        || [kindLabel(item.kind), bytes(item.size)].filter(Boolean).join(" · ");
      const tail = isSent
        ? `<span class="ok">✓</span>`
        : isBusy
          ? `<span class="busy">…</span>`
          : `<span class="tail">${esc(bytes(item.size)) || "↓"}</span>`;
      return `
        <button class="row ${isSent ? "done" : ""}" data-key="${esc(key)}"
                title="${esc(item.url)}">
          <span class="num">${index + 1}.</span>
          <span class="meta"><b>${esc(title)}</b><small>${esc(detail)}</small></span>
          ${tail}
        </button>`;
    }).join("");
  }

  function render() {
    if (invalidated) return;
    ensureUi();
    resetPageIfNeeded();

    const enabled = settings.floatingButton !== false && settings.captureMedia !== false;
    if (!enabled || dismissedFor === pageKey()) {
      host.style.display = "none";
      panelOpen = false;
      ui.panel.classList.remove("open");
      return;
    }

    const canOffer = visibleMedia.length > 0 ||
      (isVideoSitePage() && capabilities.ytdlp);
    if (!canOffer) {
      host.style.display = "none";
      panelOpen = false;
      ui.panel.classList.remove("open");
      return;
    }

    host.style.display = "";
    place();

    const count = visibleMedia.length;
    ui.label.textContent = visibleMedia.some((item) => item.kind !== "audio")
      ? "Download this video"
      : "Download audio";
    ui.chev.textContent = panelOpen ? "▴" : "▾";
    ui.bar.setAttribute("aria-expanded", panelOpen ? "true" : "false");
    ui.panel.classList.toggle("open", panelOpen);

    // "Download all" only makes sense when there is more than one video row;
    // audio-only lists are downloaded one track at a time.
    ui.all.hidden = formats.loading || count < 2 ||
      !visibleMedia.some((item) => item.kind !== "audio");
    ui.all.disabled = count === 0;
    ui.list.innerHTML = listHtml();
  }

  async function addOne(item) {
    if (invalidated) return;
    const key = keyOf(item);
    if (!item?.url || sent.has(key) || sending.has(key)) return;
    sending.add(key);
    render();

    const payload = { type: "add", url: item.url, referrer: location.href };
    if (item.quality) payload.quality = item.quality;
    const result = await send(payload);
    sending.delete(key);
    if (invalidated) return;

    if (result?.ok) {
      sent.add(key);
      const label = item.label || (item.quality === "audio" ? "audio" : item.quality ? `${item.quality}p` : "");
      toast(label
        ? `Sent ${label} to RapidFetch.`
        : `Sent ${nameOf(item.url)} to RapidFetch.`);
    } else {
      toast(result?.error || "RapidFetch is not reachable. Start the desktop app.", true);
    }
    render();
  }

  function toast(message, bad = false) {
    if (invalidated) return;
    ensureUi();
    if (!ui) return;
    ui.toast.textContent = message;
    ui.toast.classList.toggle("bad", bad);
    ui.toast.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
      if (!ui) return;
      ui.toast.classList.remove("show");
      ui.toast.textContent = "";
    }, 4200);
  }

  /* ------------------------------------------------------------- messaging */

  chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (invalidated) return false;
    if (message?.type === "collect") {
      sendResponse(collect());
      return false;
    }
    if (message?.type === "ping-page") {
      sendResponse({ ok: true, title: document.title });
      return false;
    }
    if (message?.type === "rf:settings" && message.settings) {
      Object.assign(settings, message.settings);
      render();
      return false;
    }
    if (message?.type === "rf:media") {
      merge(message.items || []);
      return false;
    }
    return false;
  });

  /* ---------------------------------------------------------------- start */

  function report() {
    if (invalidated) return;
    resetPageIfNeeded();
    const data = collect();
    if (data.media.length > 0) {
      void send({
        type: "media:found",
        items: data.media,
        pageUrl: data.pageUrl
      });
    }
    merge(data.media);
    rebuildVisible();
    render();
  }

  async function refreshCapabilities() {
    if (invalidated) return false;
    const caps = await send({ type: "capabilities" });
    if (invalidated || !caps?.ok) return false;
    const wasAvailable = capabilities.ytdlp;
    const next = {
      ytdlp: !!caps.ytdlp,
      videoSites: caps.videoSites !== false,
      quality: caps.quality || "best"
    };
    const changed = next.ytdlp !== capabilities.ytdlp ||
      next.videoSites !== capabilities.videoSites ||
      next.quality !== capabilities.quality;
    capabilities = next;
    if (changed) {
      rebuildVisible();
      render();
    }
    return !wasAvailable && capabilities.ytdlp;
  }

  /** Keep checking until the desktop app is reachable, then stop polling. */
  function watchForDesktop() {
    if (invalidated || capsTimer || capabilities.ytdlp) return;
    capsTimer = setInterval(async () => {
      if (invalidated) {
        clearInterval(capsTimer);
        capsTimer = null;
        return;
      }
      if (await refreshCapabilities()) {
        clearInterval(capsTimer);
        capsTimer = null;
      }
    }, 30000);
  }

  document.addEventListener("visibilitychange", () => {
    if (!invalidated && document.visibilityState === "visible" && !capabilities.ytdlp) {
      void refreshCapabilities();
    }
  });

  async function bootstrap() {
    if (invalidated) return;
    await refreshCapabilities();
    if (invalidated) return;
    rebuildVisible();
    render();
    watchForDesktop();

    const result = await send({ type: "media:sync" });
    if (invalidated) return;
    if (result?.ok) merge(result.items || []);
    report();
  }

  setTimeout(bootstrap, 1200);
  setTimeout(report, 6000);

  // Media added by players after interaction.
  const schedule = () => {
    if (invalidated || pending) return;
    pending = setTimeout(() => {
      pending = null;
      if (!invalidated) report();
    }, 2000);
  };

  try {
    observer = new MutationObserver(schedule);
    observer.observe(document.documentElement, {
      subtree: true,
      childList: true
    });
  } catch { /* ignore */ }
})();
