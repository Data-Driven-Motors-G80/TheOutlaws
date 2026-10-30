// Local-only static server for the prototype. Run from any directory with Node.js.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../Builds/TransformStunt-WebGL');
const types = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm',
  '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon', '.json': 'application/json' };
if (!fs.existsSync(path.join(root, 'index.html'))) {
  console.error('Build missing. In Unity use The Outlaws > Transform Stunt > Build WebGL.');
  process.exit(1);
}
const server = http.createServer((req, res) => {
  let file;
  try {
    const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    file = path.resolve(root, '.' + (pathname === '/' ? '/index.html' : pathname));
    const relative = path.relative(root, file);
    if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error('Outside build');
  } catch {
    res.writeHead(400); res.end('Invalid path'); return;
  }
  fs.stat(file, (error, stat) => {
    if (error || !stat.isFile()) { res.writeHead(404); res.end('Not found'); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream',
      'Content-Length': stat.size, 'Cache-Control': 'no-store' });
    if (req.method === 'HEAD') { res.end(); return; }
    const stream = fs.createReadStream(file);
    stream.on('error', () => res.destroy()); stream.pipe(res);
  });
});
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
server.listen(8080, '127.0.0.1', () => console.log('Transform / Drive: http://127.0.0.1:8080  (Ctrl+C to stop)'));
