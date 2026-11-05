/* RapidFetch popup */

const $ = (id) => document.getElementById(id);
const ui = {
  statePill: $("statePill"),
  mediaList: $("mediaList"),
  mediaEmpty: $("mediaEmpty"),
  mediaCount: $("mediaCount"),
  activeCount: $("activeCount"),
  pageTitle: $("pageTitle"),
  addAllMedia: $("addAllMedia"),
  downloadList: $("downloadList"),
  downloadEmpty: $("downloadEmpty"),
  addForm: $("addForm"),
  addUrl: $("addUrl"),
  openPicker: $("openPicker"),
  openOptions: $("openOptions"),
  optionsLink: $("optionsLink"),
  toggleCapture: $("toggleCapture"),
  offlineBar: $("offlineBar"),
  startApp: $("startApp"),
  footerStats: $("footerStats"),
  paneMedia: $("paneMedia"),
  paneDownloads: $("paneDownloads")
};

const state = {
  tabId: null,
  tabUrl: "",
  tabTitle: "",
  media: [],
  downloads: [],
  settings: null,
  caps: null,
  online: false,
  busy: false,
  mediaAdded: new Set()
};

const VIDEO_SITE_RE = /(^|\.)(youtube\.com|youtu\.be|vimeo\.com|dailymotion\.com|dai\.ly|soundcloud\.com|bandcamp\.com|twitch\.tv|bilibili\.com|facebook\.com|fb\.watch)$/i;

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

/** Page-level entries for sites that need yt-dlp (separate audio/video tracks). */
function pageItems() {
  if (!state.caps?.ytdlp) return [];
  if (!isVideoPageUrl(state.tabUrl)) return [];

  const title = (state.tabTitle || "This page")
    .replace(/\s+-\s+YouTube$/i, "")
    .trim() || "This page";
  const url = state.tabUrl;
  const quality = state.caps.quality || "best";
  const label = quality === "best" ? "Best available" : `Up to ${quality}p`;
  return [
    {
      key: "page:best|" + url, url, kind: "video", quality, title,
      subtitle: `${label} · merges audio and video`
    },
    {
      key: "page:audio|" + url, url, kind: "audio", quality: "audio", title,
      subtitle: "Audio only"
    }
  ];
}

/* ----------------------------------------------------------------- helpers */

function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, (c) => (
    { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]
  ));
}

function bytes(value) {
  const size = Number(value) || 0;
  if (size < 1024) return `${size} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let amount = size / 1024;
  let index = 0;
  while (amount >= 1024 && index < units.length - 1) {
    amount /= 1024;
    index++;
  }
  return `${amount >= 100 ? amount.toFixed(0) : amount.toFixed(1)} ${units[index]}`;
}

function speed(value) {
  const rate = Number(value) || 0;
  return rate > 0 ? `${bytes(rate)}/s` : "";
}

function nameFromUrl(url) {
  try {
    const clean = new URL(url).pathname.split("/").filter(Boolean).pop() || "";
    return decodeURIComponent(clean) || url;
  } catch {
    return url;
  }
}

function kindLabel(kind) {
  return { video: "Video", audio: "Audio", stream: "Stream", image: "Image" }[kind] || "File";
}

function kindGlyph(kind) {
  return { video: "▶", audio: "♪", stream: "≋", image: "▣" }[kind] || "⬇";
}

/** RapidFetch downloads direct files and HLS. DASH manifests/fragments and
 *  YouTube's segmented googlevideo URLs would need muxing, so hide them. */
function supportedMedia(url) {
  if (!url || !/^https?:/i.test(url)) return false;
  if (/\.(mpd|m4s)(?:[?#]|$)/i.test(url)) return false;
  try {
    const host = new URL(url).hostname.toLowerCase();
    if (host === "googlevideo.com" || host.endsWith(".googlevideo.com")) return false;
  } catch { /* ignore */ }
  return true;
}

function statusKind(status) {
  const value = String(status || "").toLowerCase();
  if (value.includes("complete")) return "complete";
  if (value.includes("fail") || value.includes("error")) return "failed";
  if (value.includes("pause")) return "paused";
  if (value.includes("cancel")) return "failed";
  if (value.includes("download") || value.includes("connect")) return "downloading";
  return "queued";
}

async function send(message) {
  try {
    return await chrome.runtime.sendMessage(message);
  } catch (error) {
    return { ok: false, error: error.message };
  }
}

/* -------------------------------------------------------------- connection */

function setState(value) {
  ui.statePill.className = "pill " + (value === "online" ? "pill-online" : value === "offline" ? "pill-offline" : "pill-wait");
  ui.statePill.textContent = value === "online" ? "Connected" : value === "offline" ? "Offline" : "Connecting";
  state.online = value === "online";
  ui.offlineBar.hidden = value !== "offline";
}

async function refreshConnection() {
  const result = await send({ type: "ping" });
  setState(result?.state || (result?.ok ? "online" : "offline"));
}

/* ------------------------------------------------------------------- media */

function renderMedia() {
  ui.mediaCount.textContent = String(state.media.length);
  ui.mediaEmpty.hidden = state.media.length > 0;
  ui.addAllMedia.hidden = state.media.length === 0;

  ui.mediaList.innerHTML = state.media.map((item, index) => {
    const key = item.key || item.url;
    const added = state.mediaAdded.has(key);
    return `
      <div class="card" data-index="${index}">
        <div class="card-icon ${esc(item.kind)}">${kindGlyph(item.kind)}</div>
        <div class="card-body">
          <div class="card-title" title="${esc(item.url)}">${esc(item.title || nameFromUrl(item.url))}</div>
          <div class="card-sub">${esc(item.subtitle || kindLabel(item.kind))}${!item.subtitle && item.size ? " · " + bytes(item.size) : ""}</div>
        </div>
        <div class="card-actions">
          <button class="dl-btn add-media ${added ? "done" : ""}" data-key="${esc(key)}"
                  title="${added ? "Sent to RapidFetch" : "Download with RapidFetch"}"
                  ${added ? "disabled" : ""}>${added ? "Sent ✓" : "Download"}</button>
        </div>
      </div>`;
  }).join("");
}

async function loadMedia() {
  if (state.tabId == null) return;
  ui.pageTitle.textContent = "Reading the page…";
  const result = await send({ type: "media:list", tabId: state.tabId, collect: true });
  if (!result?.ok) {
    ui.pageTitle.textContent = "Page not readable";
    return;
  }

  const page = pageItems();
  const seen = new Set(page.map((item) => item.key));
  const files = (result.items || []).filter((item) => {
    if (!supportedMedia(item.url) || seen.has(item.url)) return false;
    seen.add(item.url);
    return true;
  }).map((item) => ({ ...item, key: item.url }));

  state.media = page.concat(files).sort((a, b) => {
    if (!!a.quality !== !!b.quality) return a.quality ? -1 : 1;
    const rank = { stream: 0, video: 1, audio: 2 };
    return (rank[a.kind] ?? 3) - (rank[b.kind] ?? 3) || (b.foundAt || 0) - (a.foundAt || 0);
  });

  const title = state.tabTitle || (result.pageUrl ? nameFromUrl(result.pageUrl) : "This page");
  ui.pageTitle.textContent = title + (state.media.length ? ` · ${state.media.length} item${state.media.length === 1 ? "" : "s"}` : "");
  renderMedia();
}

async function addMedia(key) {
  const item = state.media.find((entry) => (entry.key || entry.url) === key);
  if (!item || state.mediaAdded.has(key)) return;

  const payload = { type: "add", url: item.url, referrer: state.tabUrl };
  if (item.quality) payload.quality = item.quality;
  const result = await send(payload);
  if (result?.ok) {
    state.mediaAdded.add(key);
    renderMedia();
    flash(`Sent ${item.title || nameFromUrl(item.url)}`);
    await refreshDownloads();
  } else {
    flash(result?.error || "Could not add that item.", true);
    await refreshConnection();
  }
}

async function addAllMedia() {
  const pending = state.media.filter((item) =>
    !state.mediaAdded.has(item.key || item.url) && item.quality !== "audio");
  if (pending.length === 0) return;

  ui.addAllMedia.disabled = true;
  let added = 0;
  for (const item of pending) {
    const payload = { type: "add", url: item.url, referrer: state.tabUrl };
    if (item.quality) payload.quality = item.quality;
    const result = await send(payload);
    if (result?.ok) {
      state.mediaAdded.add(item.key || item.url);
      added++;
    }
  }
  ui.addAllMedia.disabled = false;
  renderMedia();
  if (added > 0) {
    flash(`Queued ${added} download${added === 1 ? "" : "s"}`);
    await refreshDownloads();
  } else {
    flash("Could not add those items.", true);
  }
}

/* --------------------------------------------------------------- downloads */

function renderDownloads() {
  const items = state.downloads;
  const active = items.filter((item) => statusKind(item.status) === "downloading" || statusKind(item.status) === "queued").length;
  ui.activeCount.textContent = String(items.length);
  ui.downloadEmpty.hidden = items.length > 0;

  const totalSpeed = items.reduce((sum, item) => sum + (Number(item.speed) || 0), 0);
  ui.footerStats.textContent = items.length === 0
    ? "Ready"
    : `${active} active${totalSpeed > 0 ? " · " + speed(totalSpeed) : ""}`;

  ui.downloadList.innerHTML = items.map((item, index) => {
    const kind = statusKind(item.status);
    const percent = item.total > 0 ? Math.min(100, Math.max(0, (item.downloaded / item.total) * 100)) : 0;
    const subtitle = kind === "complete"
      ? bytes(item.total || item.downloaded)
      : [item.total ? `${bytes(item.downloaded)} of ${bytes(item.total)}` : bytes(item.downloaded), speed(item.speed), item.status].filter(Boolean).join(" · ");

    const resume = ["paused", "failed", "queued"].includes(kind);
    const pause = kind === "downloading";
    const reveal = kind === "complete";

    return `
      <div class="card ${kind}" data-download="${index}">
        <div class="card-icon ${kind === "downloading" ? "stream" : "file"}">${kind === "complete" ? "✓" : kind === "failed" ? "!" : "⇩"}</div>
        <div class="card-body">
          <div class="card-title" title="${esc(item.url)}">${esc(item.fileName || nameFromUrl(item.url))}</div>
          <div class="card-sub">${esc(subtitle)}</div>
          <div class="bar"><span style="width:${kind === "complete" ? 100 : percent.toFixed(1)}%"></span></div>
        </div>
        <div class="card-actions">
          ${pause ? `<button class="mini cmd" data-cmd="pause" data-id="${esc(item.id)}" title="Pause">❚❚</button>` : ""}
          ${resume ? `<button class="mini cmd" data-cmd="resume" data-id="${esc(item.id)}" title="Resume">▶</button>` : ""}
          ${reveal ? `<button class="mini cmd" data-cmd="reveal" data-id="${esc(item.id)}" title="Show in folder">▤</button>` : ""}
          <button class="mini danger cmd" data-cmd="remove" data-id="${esc(item.id)}" title="Remove">✕</button>
        </div>
      </div>`;
  }).join("");
}

async function refreshDownloads() {
  const result = await send({ type: "status" });
  if (!result?.ok) {
    setState(result?.state || "offline");
    ui.footerStats.textContent = "Desktop app unreachable";
    return;
  }
  setState("online");
  state.downloads = result.data?.downloads || [];
  renderDownloads();
}

async function downloadCommand(command, id) {
  if (state.busy) return;
  state.busy = true;
  try {
    const result = await send({ type: command, id });
    if (!result?.ok) {
      flash(result?.error || "The app did not accept that.", true);
      await refreshConnection();
    }
    await refreshDownloads();
  } finally {
    state.busy = false;
  }
}

/* -------------------------------------------------------------------- misc */

let flashTimer = null;
function flash(message, error = false) {
  ui.footerStats.textContent = message;
  ui.footerStats.style.color = error ? "var(--danger)" : "";
  clearTimeout(flashTimer);
  flashTimer = setTimeout(() => {
    ui.footerStats.style.color = "";
    if (state.downloads.length === 0) {
      ui.footerStats.textContent = "Ready";
    } else {
      const active = state.downloads.filter((item) => ["downloading", "queued"].includes(statusKind(item.status))).length;
      ui.footerStats.textContent = `${active} active`;
    }
  }, 3200);
}

function applySettings() {
  if (!state.settings) return;
  const off = state.settings.captureMode === "off";
  ui.toggleCapture.classList.toggle("on", !off);
  ui.toggleCapture.classList.toggle("off", off);
  ui.toggleCapture.title = off ? "Automatic capture is off" : "Automatic capture is on";
}

function switchTab(tab) {
  for (const button of document.querySelectorAll(".seg")) {
    button.classList.toggle("seg-active", button.dataset.tab === tab);
  }
  ui.paneMedia.hidden = tab !== "media";
  ui.paneDownloads.hidden = tab !== "downloads";
}

/* ------------------------------------------------------------------- wiring */

document.querySelectorAll(".seg").forEach((button) => {
  button.addEventListener("click", () => switchTab(button.dataset.tab));
});

ui.mediaList.addEventListener("click", (event) => {
  const button = event.target.closest(".add-media");
  if (!button) return;
  addMedia(button.dataset.key);
});

ui.addAllMedia.addEventListener("click", addAllMedia);

ui.downloadList.addEventListener("click", (event) => {
  const button = event.target.closest(".cmd");
  if (!button) return;
  downloadCommand(button.dataset.cmd, button.dataset.id);
});

ui.addForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const url = ui.addUrl.value.trim();
  if (!/^https?:\/\//i.test(url)) {
    flash("Paste a full http(s) address.", true);
    return;
  }
  ui.addUrl.value = "";
  const result = await send({ type: "add", url, referrer: state.tabUrl });
  if (result?.ok) {
    flash(`Sent ${nameFromUrl(url)}`);
    switchTab("downloads");
    await refreshDownloads();
  } else {
    flash(result?.error || "Could not add that address.", true);
    await refreshConnection();
  }
});

ui.openPicker.addEventListener("click", () => {
  if (state.tabId == null) return;
  chrome.tabs.create({ url: chrome.runtime.getURL(`picker.html?tabId=${state.tabId}&mode=links`) });
});

ui.openOptions.addEventListener("click", () => chrome.runtime.openOptionsPage());
ui.optionsLink.addEventListener("click", (event) => {
  event.preventDefault();
  chrome.runtime.openOptionsPage();
});

ui.toggleCapture.addEventListener("click", async () => {
  const result = await send({ type: "capture:toggle" });
  const settings = await send({ type: "settings:get" });
  if (settings?.ok) {
    state.settings = settings.settings;
    applySettings();
  }
  if (result?.ok) {
    flash(result.captureMode === "off" ? "Automatic capture is off" : "Automatic capture is on");
  }
});

ui.startApp.addEventListener("click", async () => {
  flash("Starting RapidFetch…");
  const result = await send({ type: "show" });
  if (!result?.ok) {
    flash(result?.error || "Could not start the app.", true);
    return;
  }
  setTimeout(refreshConnection, 1200);
  setTimeout(refreshDownloads, 2200);
});

/* --------------------------------------------------------------------- init */

(async function init() {
  const settings = await send({ type: "settings:get" });
  if (settings?.ok) {
    state.settings = settings.settings;
    applySettings();
  }

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (tab) {
    state.tabId = tab.id;
    state.tabUrl = tab.url || "";
    state.tabTitle = tab.title || "";
  }

  // Deep link: popup.html?tabId=… inspects a specific tab (used by the picker
  // and by automated UI checks).
  const requested = Number(new URLSearchParams(location.search).get("tabId"));
  if (Number.isFinite(requested) && requested > 0) {
    try {
      const target = await chrome.tabs.get(requested);
      state.tabId = target.id;
      state.tabUrl = target.url || state.tabUrl;
      state.tabTitle = target.title || state.tabTitle;
    } catch { /* keep the active tab */ }
  }

  switchTab("media");
  refreshConnection();

  const caps = await send({ type: "capabilities" });
  state.caps = caps?.ok ? caps : null;

  await loadMedia();
  await refreshDownloads();

  setInterval(() => {
    if (!ui.paneDownloads.hidden) refreshDownloads();
  }, 1200);
})();
