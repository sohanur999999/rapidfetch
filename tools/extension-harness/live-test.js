/* Live extension check against a headless Chromium browser.
 *
 * Usage:
 *   node tools/extension-harness/live-test.js <debug-port> <url> <screenshot>
 *
 * The browser must already be running with the unpacked RapidFetch extension
 * loaded and a remote-debugging port. The script opens the URL, clicks the
 * "Download this video" bar, waits for the format list, and writes a PNG.
 */

const fs = require("fs");

const port = Number(process.argv[2] || 9334);
const targetUrl = process.argv[3] || "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
const screenshotPath = process.argv[4] || "docs/screenshots/media-bar-live.png";

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function main() {
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const page = targets.find((item) => item.type === "page" && item.url === "about:blank") ||
    targets.find((item) => item.type === "page");
  if (!page) throw new Error("No page target. Is the browser running?");

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 1;
  const events = [];

  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params }));
  });

  await new Promise((resolve, reject) => {
    ws.addEventListener("open", resolve, { once: true });
    ws.addEventListener("error", reject, { once: true });
  });

  ws.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
      const { resolve, reject } = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) reject(new Error(message.error.message));
      else resolve(message.result);
      return;
    }
    if (message.method) events.push(message.method);
  });

  const evaluate = async (script) => {
    const result = await send("Runtime.evaluate", {
      expression: script,
      returnByValue: true,
      awaitPromise: true
    });
    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description ||
        result.exceptionDetails.text);
    }
    return result.result?.value;
  };

  const waitFor = async (label, predicate, timeoutMs, intervalMs = 400) => {
    const started = Date.now();
    let last;
    while (Date.now() - started < timeoutMs) {
      try {
        last = await predicate();
        if (last) return last;
      } catch (error) {
        last = error.message;
      }
      await sleep(intervalMs);
    }
    throw new Error(`Timed out waiting for ${label}. Last: ${JSON.stringify(last)}`);
  };

  await send("Page.enable");
  await send("Runtime.enable");
  await send("Emulation.setDeviceMetricsOverride", {
    width: 1440, height: 960, deviceScaleFactor: 1, mobile: false
  });
  await send("Page.navigate", { url: targetUrl });

  const bar = await waitFor("content script", async () => {
    return evaluate(`(() => {
      const host = document.querySelector('#rapidfetch-floating-host');
      const bar = host?.shadowRoot?.querySelector('.bar');
      if (!bar) return null;
      return { text: bar.innerText, hidden: host.style.display === 'none' };
    })()`);
  }, 45000);

  if (bar.hidden) throw new Error("The media bar is hidden on this page.");

  await evaluate(`document.querySelector('#rapidfetch-floating-host').shadowRoot.querySelector('.bar').click()`);

  await waitFor("open panel", async () => {
    return evaluate(`document.querySelector('#rapidfetch-floating-host')
      .shadowRoot.querySelector('.panel').classList.contains('open')`);
  }, 10000);

  const result = await waitFor("format list", async () => {
    return evaluate(`(() => {
      const shadow = document.querySelector('#rapidfetch-floating-host').shadowRoot;
      if (!shadow.querySelector('.panel').classList.contains('open')) return null;
      if (shadow.querySelector('.spinner')) return null;
      const rows = [...shadow.querySelectorAll('.row')].map((row) => ({
        title: row.querySelector('b')?.textContent || '',
        detail: row.querySelector('small')?.textContent || '',
        tail: row.querySelector('.tail')?.textContent || ''
      }));
      const notice = shadow.querySelector('.notice')?.innerText || '';
      // Site formats are titled with the page/video title, while the local
      // harness labels rows "MP4 file, quality ...". Treat any rendered row as
      // a loaded list so the same check works for both.
      const hasFormats = rows.length > 0;
      if (!hasFormats && !notice) return null;
      return {
        bar: shadow.querySelector('.bar').innerText,
        panelOpen: true,
        all: shadow.querySelector('.all')?.hidden === false,
        notice,
        rows
      };
    })()`);
  }, 90000);

  const geometry = await evaluate(`(() => {
    const shadow = document.querySelector('#rapidfetch-floating-host').shadowRoot;
    const panel = shadow.querySelector('.panel').getBoundingClientRect();
    return { panel: { top: panel.top, bottom: panel.bottom, left: panel.left, right: panel.right },
      viewport: { width: innerWidth, height: innerHeight } };
  })()`);

  const shot = await send("Page.captureScreenshot", { format: "png" });
  fs.mkdirSync(require("path").dirname(screenshotPath), { recursive: true });
  fs.writeFileSync(screenshotPath, Buffer.from(shot.data, "base64"));

  console.log(JSON.stringify({ url: targetUrl, bar, result, geometry,
    screenshot: screenshotPath }, null, 2));

  ws.close();
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
