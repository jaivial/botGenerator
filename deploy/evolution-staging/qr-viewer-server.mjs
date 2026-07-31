import { createServer } from 'node:http';

const bindAddress = process.env.BIND_ADDRESS;
const port = Number(process.env.PORT);
const token = process.env.VIEWER_TOKEN;
const apiKey = process.env.EVOLUTION_API_KEY;
const instance = process.env.EVOLUTION_INSTANCE;

if (!bindAddress || !port || !token || !apiKey || !instance) {
  throw new Error('Missing QR viewer configuration');
}

const viewerPath = `/${token}`;
const qrPath = `${viewerPath}/qr.png`;
const headers = {
  'Cache-Control': 'no-store, max-age=0',
  'Content-Security-Policy': "default-src 'none'; img-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'",
  'Referrer-Policy': 'no-referrer',
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
};

createServer(async (request, response) => {
  if (request.method !== 'GET') {
    response.writeHead(405, headers).end();
    return;
  }

  const pathname = new URL(request.url, `http://${request.headers.host}`).pathname.replace(/\/$/, '');

  if (pathname === viewerPath) {
    response.writeHead(200, { ...headers, 'Content-Type': 'text/html; charset=utf-8' });
    response.end(`<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>BotGenerator staging QR</title><style>
body{display:grid;min-height:100vh;margin:0;place-items:center;background:#111;color:#eee;font:16px sans-serif}main{text-align:center}img{width:min(80vw,500px);background:#fff}
</style></head><body><main><img id="qr" src="${qrPath}" alt="Current WhatsApp pairing QR"><p>Live staging QR. Scan without closing this page.</p></main>
<script>const qr=document.getElementById('qr');setInterval(()=>{qr.src='${qrPath}?t='+Date.now()},2000)</script></body></html>`);
    return;
  }

  if (pathname === qrPath) {
    try {
      const apiResponse = await fetch(`http://127.0.0.1:8108/instance/connect/${encodeURIComponent(instance)}`, {
        headers: { apikey: apiKey, Origin: 'http://127.0.0.1:8108' },
      });
      if (!apiResponse.ok) {
        throw new Error(`Evolution API returned ${apiResponse.status}`);
      }
      const payload = await apiResponse.json();
      const dataUrl = payload.base64 ?? payload.qrcode?.base64;
      if (typeof dataUrl !== 'string') {
        throw new Error('Evolution API did not return QR image');
      }
      response.writeHead(200, { ...headers, 'Content-Type': 'image/png' });
      response.end(Buffer.from(dataUrl.replace(/^data:image\/png;base64,/, ''), 'base64'));
    } catch (error) {
      console.error(error.message);
      response.writeHead(502, headers).end();
    }
    return;
  }

  response.writeHead(404, headers).end();
}).listen(port, bindAddress, () => console.log(`QR viewer listening on ${bindAddress}:${port}`));
