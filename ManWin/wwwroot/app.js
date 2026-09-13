const metricInputs = [...document.querySelectorAll('[data-metric]')];
const saveButton = document.querySelector('#save-button');
const saveStatus = document.querySelector('#save-status');
let statusTimeout;

function selectedMetrics() {
  return metricInputs.filter(input => input.checked).map(input => input.dataset.metric);
}

function applySelection(metrics) {
  const selected = new Set(metrics);
  metricInputs.forEach(input => { input.checked = selected.has(input.dataset.metric); });
}

function showSaved() {
  saveStatus.textContent = 'Settings saved';
  saveStatus.classList.add('visible');
  clearTimeout(statusTimeout);
  statusTimeout = setTimeout(() => saveStatus.classList.remove('visible'), 1800);
}

saveButton.addEventListener('click', () => {
  const metrics = selectedMetrics();
  window.chrome?.webview?.postMessage(JSON.stringify({ type: 'save', metrics }));
  if (!window.chrome?.webview) showSaved();
});

document.querySelector('#close-button').addEventListener('click', () => {
  window.chrome?.webview?.postMessage(JSON.stringify({ type: 'close' }));
});

document.querySelector('#minimize-button').addEventListener('click', () => {
  window.chrome?.webview?.postMessage(JSON.stringify({ type: 'minimize' }));
});

document.querySelector('#titlebar').addEventListener('pointerdown', event => {
  if (event.button === 0 && !event.target.closest('button')) {
    window.chrome?.webview?.postMessage(JSON.stringify({ type: 'beginDrag' }));
  }
});

window.chrome?.webview?.addEventListener('message', event => {
  let message = event.data;
  if (typeof message === 'string') {
    try { message = JSON.parse(message); } catch { return; }
  }
  if (message.type === 'load' && Array.isArray(message.metrics)) applySelection(message.metrics);
  if (message.type === 'saved') showSaved();
  if (message.type === 'sensorUpdate' && Array.isArray(message.readings)) window.manwinSensorReadings = message.readings;
  if (message.type === 'sensorError') window.manwinSensorError = message.message;
});

// Ask the native host for its persisted settings only after the message handler
// is installed, so the initial selection cannot be lost during WebView startup.
window.chrome?.webview?.postMessage(JSON.stringify({ type: 'ready' }));
