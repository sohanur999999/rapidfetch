const http = require("http");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..", "..");
const port = Number(process.argv[2] || 6123);

const types = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".mp4": "video/mp4",
  ".mp3": "audio/mpeg",
  ".m3u8": "application/vnd.apple.mpegurl",
  ".ts": "video/mp2t"
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, `http://127.0.0.1:${port}`);

  if (url.pathname === "/media/sample.mp4" || url.pathname === "/media/extra.webm") {
    res.writeHead(200, { "Content-Type": "video/mp4", "Content-Length": "1024", "Accept-Ranges": "bytes" });
    res.end(Buffer.alloc(1024, 7));
    return;
  }
  if (url.pathname === "/media/sample.mp3") {
    res.writeHead(200, { "Content-Type": "audio/mpeg", "Content-Length": "1024" });
    res.end(Buffer.alloc(1024, 3));
    return;
  }
  if (url.pathname === "/media/master.m3u8") {
    res.writeHead(200, { "Content-Type": "application/vnd.apple.mpegurl" });
    res.end("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:4\n#EXTINF:4.0,\nseg0.ts\n#EXT-X-ENDLIST\n");
    return;
  }

  const relative = decodeURIComponent(url.pathname).replace(/^\/+/, "");
  const file = path.resolve(root, relative);
  if (!file.startsWith(root) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("not found: " + url.pathname);
    return;
  }

  res.writeHead(200, { "Content-Type": types[path.extname(file).toLowerCase()] || "application/octet-stream" });
  fs.createReadStream(file).pipe(res);
});

server.listen(port, "127.0.0.1", () => {
  console.log(`harness http://127.0.0.1:${port}/tools/extension-harness/index.html`);
});
