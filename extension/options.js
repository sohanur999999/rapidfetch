/* RapidFetch options page */

const $ = (id) => document.getElementById(id);
const DEFAULTS = {
  port: 55123,
  captureMode: "auto",
  captureMedia: true,
  notify: true,
  connections: 8,
  badge: true,
  floatingButton: true,
  skipHosts: ""
};

const el = {
  statePill: $("statePill"),
  port: $("port"),
  connections: $("connections"),
  captureMode: $("captureMode"),
  captureMedia: $("captureMedia"),
  floatingButton: $("floatingButton"),
  notify: $("notify"),
  badge: $("badge"),
  skipHosts: $("skipHosts"),
  test: $("test"),
  startApp: $("startApp"),
  testResult: $("testResult"),
  save: $("save"),
  reset: $("reset"),
  savedNote: $("savedNote"),
  identity: $("identity")
};

let saveTimer = null;

async function send(message) {
  try {
    return await chrome.runtime.sendMessage(message);
  } catch (error) {
    return { ok: false, error: error.message };
  }
}

function setState(value) {
  el.statePill.className = "pill " + (value === "online" ? "pill-online" : value === "offline" ? "pill-offline" : "pill-wait");
  el.statePill.textContent = value === "online"
    ? "Desktop app connected"
    : value === "offline" ? "Desktop app offline" : "Checking connection…";
}

function readForm() {
  return {
    port: Math.min(65535, Math.max(1024, Number(el.port.value) || DEFAULTS.port)),
    connections: Math.min(32, Math.max(1, Number(el.connections.value) || DEFAULTS.connections)),
    captureMode: el.captureMode.value,
    captureMedia: el.captureMedia.checked,
    floatingButton: el.floatingButton.checked,
    notify: el.notify.checked,
    badge: el.badge.checked,
    skipHosts: el.skipHosts.value.trim()
  };
}

function fill(settings) {
  el.port.value = settings.port ?? DEFAULTS.port;
  el.connections.value = settings.connections ?? DEFAULTS.connections;
  el.captureMode.value = settings.captureMode || DEFAULTS.captureMode;
  el.captureMedia.checked = settings.captureMedia !== false;
  el.floatingButton.checked = settings.floatingButton !== false;
  el.notify.checked = settings.notify !== false;
  el.badge.checked = settings.badge !== false;
  el.skipHosts.value = settings.skipHosts || "";
}

function note(message, error = false) {
  el.savedNote.textContent = message;
  el.savedNote.style.color = error ? "var(--danger)" : "var(--muted)";
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    el.savedNote.textContent = "";
    el.savedNote.style.color = "var(--muted)";
  }, 4000);
}

async function testConnection(silent = false) {
  if (!silent) el.testResult.textContent = "Checking…";
  const result = await send({ type: "ping" });
  const state = result?.state || "offline";
  setState(state);
  if (!silent) {
    el.testResult.textContent = state === "online"
      ? "Connected — the desktop app answered."
      : "Not reachable. Start RapidFetch and try again.";
    el.testResult.style.color = state === "online" ? "var(--success)" : "var(--danger)";
  }
  return state === "online";
}

async function save() {
  const settings = readForm();
  const result = await send({ type: "settings:set", settings });
  if (!result?.ok) {
    note(result?.error || "Could not save.", true);
    return;
  }
  note("Saved.");
  await testConnection(true);
}

el.save.addEventListener("click", save);

el.reset.addEventListener("click", async () => {
  fill(DEFAULTS);
  await save();
  note("Defaults restored.");
});

el.test.addEventListener("click", () => testConnection(false));

el.startApp.addEventListener("click", async () => {
  el.testResult.textContent = "Starting…";
  const result = await send({ type: "show" });
  if (!result?.ok) {
    el.testResult.textContent = result?.error || "Could not start the app.";
    el.testResult.style.color = "var(--danger)";
    return;
  }
  setTimeout(() => testConnection(false), 1500);
});

el.captureMode.addEventListener("change", () => {
  if (el.captureMode.value === "off") {
    el.captureMedia.checked = el.captureMedia.checked;
  }
});

for (const input of [el.port, el.connections, el.captureMode, el.captureMedia,
  el.floatingButton, el.notify, el.badge, el.skipHosts]) {
  input.addEventListener("change", save);
}

(async function init() {
  const result = await send({ type: "settings:get" });
  fill(result?.ok ? result.settings : DEFAULTS);
  el.identity.textContent = `Extension ID: ${chrome.runtime.id} · Native host: com.rapidfetch.host`;
  await testConnection(true);
})();
