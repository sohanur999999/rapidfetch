/* RapidFetch link picker */

const $ = (id) => document.getElementById(id);
const ui = {
  pageLabel: $("pageLabel"),
  list: $("list"),
  empty: $("empty"),
  search: $("search"),
  summary: $("summary"),
  selectAll: $("selectAll"),
  selectNone: $("selectNone"),
  download: $("download"),
  status: $("status")
};

const MEDIA_RE = /\.(mp4|m4v|mkv|webm|mov|avi|flv|ts|m4s|m3u8|mpd|mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i;
const FILE_RE = /\.(zip|rar|7z|tar|gz|bz2|xz|iso|exe|msi|msix|appx|apk|dmg|deb|rpm|jar|pdf|docx?|xlsx?|pptx?|epub|txt|csv|mp3|flac|wav|aac|ogg|m4a|opus|wma|mp4|mkv|avi|mov|webm|flv|ts|m4v|wmv|jpg|jpeg|png|gif|bmp|webp|svg|tiff|ico)(?:[?#]|$)/i;

const params = new URLSearchParams(location.search);
const tabId = Number(params.get("tabId"));
const initialMode = params.get("mode") === "media" ? "media" : "files";

const state = {
  entries: [],
  visible: [],
  selected: new Set(),
  filter: initialMode,
  search: "",
  pageUrl: "",
  pageTitle: "",
  port: null,
  adding: false
};

/* ----------------------------------------------------------------- helpers */

function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, (c) => (
    { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]
  ));
}

function bytes(value) {
  const size = Number(value) || 0;
  if (!size) return "";
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

function nameFromUrl(url) {
  try {
    const clean = new URL(url).pathname.split("/").filter(Boolean).pop() || "";
    return decodeURIComponent(clean) || url;
  } catch {
    return url;
  }
}

function kindOf(url, hint) {
  if (hint) return hint;
  if (/\.(m3u8|mpd)(?:[?#]|$)/i.test(url)) return "stream";
  if (/\.(mp3|m4a|aac|flac|ogg|opus|wav|wma)(?:[?#]|$)/i.test(url)) return "audio";
  if (/\.(jpg|jpeg|png|gif|bmp|webp|svg|avif)(?:[?#]|$)/i.test(url)) return "image";
  if (MEDIA_RE.test(url)) return "video";
  return "file";
}

function glyph(kind) {
  return { video: "▶", audio: "♪", stream: "≋", image: "▣" }[kind] || "⬇";
}

function passes(entry) {
  if (state.search) {
    const needle = state.search.toLowerCase();
    if (!entry.url.toLowerCase().includes(needle) && !(entry.title || "").toLowerCase().includes(needle)) {
      return false;
    }
  }

  if (state.filter === "media") {
    return ["video", "audio", "stream"].includes(entry.kind);
  }
  if (state.filter === "all") return true;
  return FILE_RE.test(entry.url) || ["video", "audio", "stream"].includes(entry.kind);
}

function render() {
  state.visible = state.entries.filter(passes).slice(0, 1500);

  ui.list.innerHTML = state.visible.map((entry, index) => {
    const checked = state.selected.has(entry.url) ? "checked" : "";
    return `
      <label class="row" data-index="${index}">
        <input type="checkbox" data-url="${esc(entry.url)}" ${checked} />
        <span class="icon ${esc(entry.kind)}">${glyph(entry.kind)}</span>
        <span class="body">
          <span class="name">${esc(entry.title || nameFromUrl(entry.url))}</span>
          <span class="url">${esc(entry.url)}</span>
        </span>
        <span class="meta">${entry.size ? bytes(entry.size) : ""}</span>
      </label>`;
  }).join("");

  const count = state.visible.length;
  ui.empty.hidden = count > 0;
  ui.summary.textContent = count === 0
    ? "No matches"
    : `${count} item${count === 1 ? "" : "s"} · ${state.selected.size} selected`;
  ui.download.disabled = state.selected.size === 0 || state.adding;
  ui.download.textContent = state.adding
    ? "Sending…"
    : state.selected.size > 0 ? `Download ${state.selected.size}` : "Download selected";
}

function merge(collected) {
  const map = new Map();

  for (const link of collected.links || []) {
    if (!/^https?:/i.test(link.url)) continue;
    map.set(link.url, {
      url: link.url,
      title: link.text || "",
      kind: kindOf(link.url),
      size: 0
    });
  }

  for (const item of collected.media || []) {
    if (!/^https?:/i.test(item.url)) continue;
    map.set(item.url, {
      url: item.url,
      title: nameFromUrl(item.url),
      kind: item.kind || kindOf(item.url),
      size: item.size || 0
    });
  }

  state.entries = [...map.values()];
  state.pageUrl = collected.pageUrl || "";
  state.pageTitle = collected.title || "";

  ui.pageLabel.textContent = state.pageTitle
    ? `${state.pageTitle} — ${state.pageUrl}`
    : state.pageUrl || "This page";
}

/* ------------------------------------------------------------------- wire */

function setFilter(filter) {
  state.filter = filter;
  for (const chip of document.querySelectorAll(".chip")) {
    chip.classList.toggle("chip-active", chip.dataset.filter === filter);
  }
  render();
}

document.querySelectorAll(".chip").forEach((chip) => {
  chip.addEventListener("click", () => setFilter(chip.dataset.filter));
});

ui.search.addEventListener("input", () => {
  state.search = ui.search.value.trim();
  render();
});

ui.list.addEventListener("change", (event) => {
  const box = event.target.closest("input[type=checkbox]");
  if (!box) return;
  if (box.checked) state.selected.add(box.dataset.url);
  else state.selected.delete(box.dataset.url);
  render();
});

ui.selectAll.addEventListener("click", () => {
  for (const entry of state.visible) state.selected.add(entry.url);
  render();
});

ui.selectNone.addEventListener("click", () => {
  state.selected.clear();
  render();
});

ui.download.addEventListener("click", () => {
  if (!state.port || state.selected.size === 0) return;
  state.adding = true;
  render();
  ui.status.textContent = `Sending ${state.selected.size} item${state.selected.size === 1 ? "" : "s"}…`;

  state.port.postMessage({
    type: "add-many",
    urls: [...state.selected],
    options: { referrer: state.pageUrl }
  });
});

/* ------------------------------------------------------------------- port */

function connect() {
  const port = chrome.runtime.connect({ name: "rapidfetch-picker" });
  state.port = port;

  port.onMessage.addListener((message) => {
    if (message.type === "collected") {
      merge(message.data || {});
      if (state.filter === "files" && state.entries.length === 0) setFilter("all");
      else render();
      ui.status.textContent = state.entries.length === 0
        ? "No links were readable on this page."
        : `Found ${state.entries.length} links.`;
    }

    if (message.type === "added") {
      state.adding = false;
      const result = message.data || {};
      ui.status.textContent = result.failed
        ? `Sent ${result.added} · ${result.failed} failed.`
        : `Sent ${result.added} download${result.added === 1 ? "" : "s"} to RapidFetch.`;
      if (result.added > 0) {
        state.selected.clear();
      }
      render();
    }
  });

  port.onDisconnect.addListener(() => {
    state.port = null;
    state.adding = false;
    render();
    ui.status.textContent = "The extension restarted. Reload this tab to retry.";
  });

  if (Number.isFinite(tabId)) {
    port.postMessage({ type: "collect", tabId });
  } else {
    ui.status.textContent = "No source tab was provided.";
  }
}

connect();
