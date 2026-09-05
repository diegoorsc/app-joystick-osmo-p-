namespace DjiPtz.WebControl;

/// <summary>
/// Pagina unica (HTML/CSS/JS embebidos, sin dependencias externas: el
/// telefono puede estar en un WiFi local sin salida a internet) que sirve de
/// control remoto tactil para Pan/Tilt/Zoom. Un joystick virtual (arrastrar
/// con el dedo) manda Pan/Tilt a /api/pantilt, y dos botones +/- mandan Zoom
/// a /api/zoom mientras se mantienen pulsados.
/// </summary>
internal static class WebUi
{
    public const string Html = """
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">
<title>DJI Pocket 3 - Control remoto</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
  html, body {
    margin: 0; padding: 0; height: 100%; background: #0b0d10; color: #e8eaed;
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    overflow: hidden; overscroll-behavior: none; user-select: none;
  }
  #app { display: flex; flex-direction: column; height: 100%; padding: 10px; gap: 10px; }
  h1 { font-size: 15px; font-weight: 600; margin: 4px 0; text-align: center; color: #9aa0a6; }
  #status {
    display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px;
    font-size: 12px; text-align: center; color: #9aa0a6;
  }
  #status b { display: block; font-size: 16px; color: #e8eaed; }
  #main { flex: 1; display: flex; align-items: center; justify-content: center; gap: 24px; min-height: 0; }
  #pad {
    position: relative; width: min(58vw, 300px); height: min(58vw, 300px);
    border-radius: 50%; background: radial-gradient(circle at 50% 50%, #1c2126 0%, #14171a 70%);
    border: 2px solid #2a2f34; touch-action: none;
  }
  #knob {
    position: absolute; width: 34%; height: 34%; left: 33%; top: 33%;
    border-radius: 50%; background: #3b82f6; box-shadow: 0 0 16px rgba(59,130,246,.6);
    transition: left .08s, top .08s;
  }
  #pad.active #knob { transition: none; }
  #zoomBar { display: flex; flex-direction: column; gap: 12px; }
  .zbtn, .actionBtn {
    touch-action: none; border: none; border-radius: 14px; background: #1c2126; color: #e8eaed;
    font-size: 22px; width: 64px; height: 64px; display: flex; align-items: center; justify-content: center;
    border: 1px solid #2a2f34;
  }
  .zbtn:active, .actionBtn:active { background: #3b82f6; }
  #actions { display: flex; gap: 10px; justify-content: center; flex-wrap: wrap; }
  .actionBtn { width: auto; height: 44px; padding: 0 14px; font-size: 13px; border-radius: 22px; }
  #inverts { display: flex; gap: 14px; justify-content: center; font-size: 12px; color: #9aa0a6; }
  #inverts label { display: flex; align-items: center; gap: 6px; }
  #conn { position: fixed; top: 8px; right: 10px; font-size: 11px; }
  #conn.ok { color: #34a853; }
  #conn.bad { color: #ea4335; }
</style>
</head>
<body>
<div id="app">
  <span id="conn">...</span>
  <h1>DJI Pocket 3 &middot; Control remoto</h1>
  <div id="status">
    <div>PAN<b id="valPan">-</b></div>
    <div>TILT<b id="valTilt">-</b></div>
    <div>ZOOM<b id="valZoom">-</b></div>
  </div>
  <div id="main">
    <div id="pad"><div id="knob"></div></div>
    <div id="zoomBar">
      <button class="zbtn" id="zoomIn">+</button>
      <button class="zbtn" id="zoomOut">-</button>
    </div>
  </div>
  <div id="actions">
    <button class="actionBtn" id="center">Center Gimbal</button>
    <button class="actionBtn" id="resetZoom">Reset Zoom</button>
  </div>
  <div id="inverts">
    <label><input type="checkbox" id="invPan">Invertir Pan</label>
    <label><input type="checkbox" id="invTilt">Invertir Tilt</label>
    <label><input type="checkbox" id="invZoom">Invertir Zoom</label>
  </div>
</div>
<script>
(function () {
  const conn = document.getElementById('conn');
  let lastOk = 0;

  async function post(path, body) {
    try {
      await fetch(path, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        keepalive: true,
      });
      lastOk = Date.now();
    } catch (e) { /* red WiFi inestable: se reintenta en el proximo tick */ }
  }

  // ---- Joystick de Pan/Tilt ----
  const pad = document.getElementById('pad');
  const knob = document.getElementById('knob');
  let dragging = false;
  let lastSent = 0;

  function setKnob(nx, ny) {
    const pct = (v) => 33 + v * 33;
    knob.style.left = pct(nx) + '%';
    knob.style.top = pct(-ny) + '%';
  }

  function handleMove(clientX, clientY) {
    const rect = pad.getBoundingClientRect();
    const cx = rect.left + rect.width / 2;
    const cy = rect.top + rect.height / 2;
    const radius = rect.width / 2;
    let dx = (clientX - cx) / radius;
    let dy = (clientY - cy) / radius;
    const mag = Math.hypot(dx, dy);
    if (mag > 1) { dx /= mag; dy /= mag; }
    const nx = dx, ny = -dy;
    setKnob(nx, ny);
    const now = Date.now();
    if (now - lastSent > 45) {
      lastSent = now;
      post('/api/pantilt', { x: nx, y: ny });
    }
  }

  function stopDrag() {
    if (!dragging) return;
    dragging = false;
    pad.classList.remove('active');
    setKnob(0, 0);
    post('/api/pantilt', { x: 0, y: 0 });
  }

  pad.addEventListener('pointerdown', (e) => {
    dragging = true;
    pad.classList.add('active');
    pad.setPointerCapture(e.pointerId);
    handleMove(e.clientX, e.clientY);
  });
  pad.addEventListener('pointermove', (e) => { if (dragging) handleMove(e.clientX, e.clientY); });
  pad.addEventListener('pointerup', stopDrag);
  pad.addEventListener('pointercancel', stopDrag);

  // ---- Zoom (mantener pulsado) ----
  function bindZoom(id, dir) {
    const btn = document.getElementById(id);
    let timer = null;
    const start = (e) => {
      e.preventDefault();
      post('/api/zoom', { dir: dir });
      timer = setInterval(() => post('/api/zoom', { dir: dir }), 150);
    };
    const stop = () => {
      if (timer) { clearInterval(timer); timer = null; }
      post('/api/zoom', { dir: 0 });
    };
    btn.addEventListener('pointerdown', start);
    btn.addEventListener('pointerup', stop);
    btn.addEventListener('pointercancel', stop);
    btn.addEventListener('pointerleave', stop);
  }
  bindZoom('zoomIn', 1);
  bindZoom('zoomOut', -1);

  // ---- Acciones ----
  document.getElementById('center').addEventListener('click', () => post('/api/center'));
  document.getElementById('resetZoom').addEventListener('click', () => post('/api/zoomreset'));

  // ---- Inversiones ----
  function bindInvert(id, axis) {
    document.getElementById(id).addEventListener('change', (e) => {
      post('/api/invert', { axis: axis, value: e.target.checked });
    });
  }
  bindInvert('invPan', 'pan');
  bindInvert('invTilt', 'tilt');
  bindInvert('invZoom', 'zoom');

  // ---- Estado en vivo ----
  async function poll() {
    try {
      const r = await fetch('/api/status');
      const s = await r.json();
      document.getElementById('valPan').textContent = s.pan;
      document.getElementById('valTilt').textContent = s.tilt;
      document.getElementById('valZoom').textContent = s.zoom;
      document.getElementById('invPan').checked = s.panInverted;
      document.getElementById('invTilt').checked = s.tiltInverted;
      document.getElementById('invZoom').checked = s.zoomInverted;
      lastOk = Date.now();
    } catch (e) { /* se refleja abajo por el timeout de conn */ }
  }
  setInterval(poll, 400);
  poll();

  setInterval(() => {
    const ok = Date.now() - lastOk < 2000;
    conn.textContent = ok ? 'conectado' : 'sin conexion';
    conn.className = ok ? 'ok' : 'bad';
  }, 500);

  document.addEventListener('touchmove', (e) => e.preventDefault(), { passive: false });
})();
</script>
</body>
</html>
""";
}
