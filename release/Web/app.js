const localeText = (spanish, english) => window.manwinLocale === 'en' ? english : spanish;
const toast = document.querySelector('#toast');
const dashboard = document.querySelector('#dashboard-view');

if (dashboard) dashboard.innerHTML = `
  <section class="hero dashboard-hero"><div><p class="eyebrow" id="dashboard-eyebrow">ESTADO DEL SISTEMA</p><h2 id="dashboard-title">ManWin está listo</h2><p class="muted" id="dashboard-description">Supervisa el rendimiento y configura tu overlay desde aquí.</p></div><div class="orb">FPS</div></section>
  <article class="card process-picker-card">
    <div class="card-head"><div><h3 id="process-picker-title">Juego objetivo</h3><p id="process-picker-description" class="muted">Selecciona un juego para mantener fijo el proceso medido por PresentMon.</p></div><span id="process-mode" class="tag">AUTOMÁTICO</span></div>
    <div class="process-controls">
      <select id="game-process-select" class="text-input"><option value="">Buscando juegos abiertos…</option></select>
      <button id="connect-game" class="primary icon-only" title="Conectar"><i>link</i></button>
      <button id="refresh-games" class="secondary icon-only" title="Actualizar"><i>refresh</i></button>
    </div>
    <p id="process-status" class="muted">El proceso se selecciona según la ventana activa.</p>
  </article>
  <section class="hardware-grid"><article class="card sensor-card"><span class="eyebrow">CPU</span><strong id="cpu-load-value">—</strong><span id="cpu-temp-value" class="muted">—</span></article><article class="card sensor-card"><span class="eyebrow">GPU</span><strong id="gpu-load-value">—</strong><span id="gpu-temp-value" class="muted">—</span></article><article class="card sensor-card"><span class="eyebrow" id="memory-label">MEMORIA</span><strong id="memory-value">—</strong><span class="muted">LibreHardwareMonitor</span></article></section>
  <article id="pawnio-warning" class="card pawnio-warning hidden"><h3 id="pawnio-title">Temperatura CPU no disponible</h3><p id="pawnio-text" class="muted">Puedes instalar PawnIO para permitir el acceso a sensores de bajo nivel.</p><button id="install-pawnio" class="primary">Instalar PawnIO</button></article>
  <section class="dashboard-actions"><button id="dashboard-design-action" class="secondary" data-view="design-view">Diseño OSD</button><button id="dashboard-usage-action" class="secondary" data-view="usage-view">Cómo usar</button></section>`;

const usageView = document.createElement('section');
usageView.id = 'usage-view';
usageView.className = 'view hidden';
usageView.innerHTML = '<div class="section-intro"><p class="eyebrow" id="usage-eyebrow">PRIMEROS PASOS</p><h2 id="usage-title">Cómo usar ManWin</h2><p class="muted" id="usage-description">Configura PawnIO una sola vez y después controla tu overlay desde aquí.</p></div><article class="card"><h3 id="usage-card-title">Activar el overlay de ManWin</h3><p id="usage-card-text" class="muted">Sigue estos pasos para mostrar el overlay dentro de tus juegos.</p><ol class="setup-steps"><li>Abre ManWin como administrador.</li><li>Si falta la temperatura del CPU, instala PawnIO desde el Dashboard.</li><li>Abre tu juego en modo ventana o borderless.</li><li>El overlay aparecerá automáticamente arriba a la izquierda.</li></ol><p id="usage-note" class="muted profile-note">LibreHardwareMonitor proporciona las métricas de hardware. PresentMon proporciona FPS y frametime.</p></article>';
document.querySelector('main')?.insertBefore(usageView, document.querySelector('#toast'));

function showToast(text) { toast.textContent = text; setTimeout(() => toast.textContent = '', 3000); }

function renderProcesses(data) {
  const select = document.querySelector('#game-process-select');
  if (!select) return;
  if (Array.isArray(data.processes)) {
    const previous = String(data.selectedProcessId || select.value || '');
    const items = data.processes;
    select.innerHTML = items.length
      ? items.map(process => `<option value="${process.id}">${process.name} — ${process.title || `PID ${process.id}`}</option>`).join('')
      : `<option value="">${localeText('No hay juegos con ventana abierta', 'No open windowed games')}</option>`;
    if (items.some(process => String(process.id) === previous)) select.value = previous;
  }
  const manual = data.mode === 'manual';
  const mode = document.querySelector('#process-mode');
  const status = document.querySelector('#process-status');
  if (mode) mode.textContent = manual ? localeText('FIJO', 'LOCKED') : localeText('SIN CONECTAR', 'NOT CONNECTED');
  if (status) status.textContent = manual
    ? localeText(`Midiendo ${data.selectedProcessName || 'juego'} (PID ${data.selectedProcessId || '—'}).`, `Measuring ${data.selectedProcessName || 'game'} (PID ${data.selectedProcessId || '—'}).`)
    : localeText('Selecciona un juego y pulsa el botón de enlace.', 'Select a game and press the link button.');
}

function update(data) {
  if (data.type === 'hardware') {
    const format = (value, suffix = '') => value === null || value === undefined ? '—' : `${Number(value).toFixed(0)}${suffix}`;
    document.querySelector('#cpu-load-value').textContent = format(data.cpuLoad, '%');
    document.querySelector('#cpu-temp-value').textContent = `${format(data.cpuTemperature, ' °C')} · ${format(data.cpuClock, ' MHz')}`;
    document.querySelector('#gpu-load-value').textContent = format(data.gpuLoad, '%');
    document.querySelector('#gpu-temp-value').textContent = `${format(data.gpuTemperature, ' °C')} · ${format(data.gpuClock, ' MHz')}`;
    document.querySelector('#memory-value').textContent = data.memoryUsed == null ? '—' : `${Number(data.memoryUsed).toFixed(1)} / ${data.memoryTotal == null ? '—' : Number(data.memoryTotal).toFixed(1)} GB`;
    const warning = document.querySelector('#pawnio-warning');
    warning?.classList.toggle('hidden', Boolean(data.pawnIoInstalled || data.cpuTemperature != null));
    renderProcesses({ mode: data.processMode, selectedProcessId: data.selectedProcessId, selectedProcessName: data.selectedProcessName });
  }
  if (data.type === 'processList') renderProcesses(data);
  if (data.type === 'pawnIoResult') showToast(data.message);
  if (data.type === 'error') showToast(data.message);
}

window.chrome?.webview?.addEventListener('message', event => update(event.data));
document.querySelector('#install-pawnio')?.addEventListener('click', () => window.chrome?.webview?.postMessage({ type: 'installPawnIo' }));
document.querySelector('#connect-game')?.addEventListener('click', () => {
  const processId = Number(document.querySelector('#game-process-select')?.value);
  if (processId > 0) window.chrome?.webview?.postMessage({ type: 'selectProcess', processId });
});
document.querySelector('#refresh-games')?.addEventListener('click', () => window.chrome?.webview?.postMessage({ type: 'refreshProcesses' }));
document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => {
  const target = button.dataset.view;
  document.querySelectorAll('.view').forEach(view => view.classList.toggle('hidden', view.id !== target));
  document.querySelectorAll('[data-view]').forEach(item => item.classList.toggle('active', item === button));
  const title = document.querySelector('#page-title');
  if (title) title.textContent = target === 'design-view' ? localeText('Diseño OSD', 'OSD Design') : target === 'usage-view' ? localeText('Cómo usar', 'How to use') : 'Dashboard';
}));
document.querySelectorAll('[data-setting]').forEach(input => input.addEventListener('change', () => {
  const settings = readOverlaySettings();
  localStorage.setItem('manwinOverlaySettings', JSON.stringify(settings));
  window.chrome?.webview?.postMessage({ type: 'overlaySettings', ...settings });
}));
const savedSettings = JSON.parse(localStorage.getItem('manwinOverlaySettings') || '{}');
document.querySelectorAll('[data-setting]').forEach(input => { if (Object.prototype.hasOwnProperty.call(savedSettings, input.dataset.setting)) input.checked = Boolean(savedSettings[input.dataset.setting]); });

// Keep the design panel focused on metrics currently supported by the native OSD.
['vram', 'power', 'memoryPercent', 'frametimeGraph', 'clocks'].forEach(setting => {
  document.querySelector(`[data-setting="${setting}"]`)?.closest('label')?.remove();
});
const ramSetting = document.querySelector('[data-setting="ram"]')?.closest('label');
if (ramSetting && !document.querySelector('[data-setting="fps"]')) {
  ramSetting.insertAdjacentHTML('afterend', '<label><input type="checkbox" data-setting="fps" checked> FPS y frametime</label>');
  const fpsInput = document.querySelector('[data-setting="fps"]');
  if (Object.prototype.hasOwnProperty.call(savedSettings, 'fps')) fpsInput.checked = Boolean(savedSettings.fps);
  fpsInput.addEventListener('change', () => {
    const settings = readOverlaySettings();
    localStorage.setItem('manwinOverlaySettings', JSON.stringify(settings));
    window.chrome?.webview?.postMessage({ type: 'overlaySettings', ...settings });
  });
}
const designView = document.querySelector('#design-view');
if (designView && !document.querySelector('#overlay-orientation')) {
  designView.insertAdjacentHTML('beforeend', '<article class="card overlay-layout-card"><h3>Orientación del overlay</h3><p class="muted">Elige cómo se acomodan las métricas dentro del juego.</p><label class="field-label" for="overlay-orientation">Distribución<select id="overlay-orientation" class="text-input"><option value="horizontal">Horizontal</option><option value="vertical">Vertical</option></select></label></article>');
  const orientation = document.querySelector('#overlay-orientation');
  orientation.value = savedSettings.vertical ? 'vertical' : 'horizontal';
  orientation.addEventListener('change', () => {
    const settings = readOverlaySettings();
    localStorage.setItem('manwinOverlaySettings', JSON.stringify(settings));
    window.chrome?.webview?.postMessage({ type: 'overlaySettings', ...settings });
  });
}

function readOverlaySettings() {
  const settings = {};
  document.querySelectorAll('[data-setting]').forEach(input => settings[input.dataset.setting] = Boolean(input.checked));
  settings.vertical = document.querySelector('#overlay-orientation')?.value === 'vertical';
  return settings;
}
window.chrome?.webview?.postMessage({ type: 'overlaySettings', ...readOverlaySettings() });
window.chrome?.webview?.postMessage({ type: 'ready' });
