const metricInputs = [...document.querySelectorAll('[data-metric]')];
const saveButton = document.querySelector('#save-button');
const saveStatus = document.querySelector('#save-status');
const opacitySlider = document.querySelector('#osd-opacity');
const opacityValue = document.querySelector('#opacity-value');
const backgroundColorInput = document.querySelector('#osd-background-color');
const backgroundColorValue = document.querySelector('#background-color-value');
const sectionColorInputs = Object.fromEntries(['cpu', 'gpu', 'ram'].map(section => [section, {
  input: document.querySelector(`#osd-${section}-text-color`),
  value: document.querySelector(`#${section}-text-color-value`)
}]));
const layoutButtons = [...document.querySelectorAll('[data-layout]')];
let layoutMode = 'vertical';
const tabList = document.querySelector('[role="tablist"]');
const tabs = [...tabList.querySelectorAll('[role="tab"]')];
let statusTimeout;

function selectTab(tab, moveFocus = false) {
  tabs.forEach(button => {
    const isSelected = button === tab;
    button.setAttribute('aria-selected', String(isSelected));
    button.tabIndex = isSelected ? 0 : -1;
    document.getElementById(button.getAttribute('aria-controls')).hidden = !isSelected;
  });
  document.title = 'ManWin · ' + tab.textContent;
  if (moveFocus) tab.focus();
}

tabs.forEach(tab => tab.addEventListener('click', () => selectTab(tab)));
tabList.addEventListener('keydown', event => {
  const currentIndex = tabs.indexOf(document.activeElement);
  if (currentIndex < 0) return;
  let nextIndex;
  if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % tabs.length;
  else if (event.key === 'ArrowLeft') nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
  else if (event.key === 'Home') nextIndex = 0;
  else if (event.key === 'End') nextIndex = tabs.length - 1;
  else return;
  event.preventDefault();
  selectTab(tabs[nextIndex], true);
});

function selectedMetrics() {
  return metricInputs.filter(input => input.checked).map(input => input.dataset.metric);
}

function applySelection(metrics) {
  const selected = new Set(metrics);
  metricInputs.forEach(input => { input.checked = selected.has(input.dataset.metric); });
}

function setOpacityPercent(value, notifyNative = false) {
  const parsed = Number(value);
  const opacityPercent = Number.isFinite(parsed) ? Math.max(20, Math.min(100, Math.round(parsed))) : 70;
  opacitySlider.value = String(opacityPercent);
  opacityValue.textContent = opacityPercent + '%';
  if (notifyNative) {
    window.chrome?.webview?.postMessage(JSON.stringify({ type: 'osdOpacityChanged', opacityPercent }));
  }
}

opacitySlider.addEventListener('input', () => setOpacityPercent(opacitySlider.value, true));

function setBackgroundColor(value, notifyNative = false) {
  const backgroundColorHex = /^#[0-9a-f]{6}$/i.test(value) ? value.toUpperCase() : '#10121A';
  backgroundColorInput.value = backgroundColorHex.toLowerCase();
  backgroundColorValue.textContent = backgroundColorHex;
  if (notifyNative) {
    window.chrome?.webview?.postMessage(JSON.stringify({ type: 'osdBackgroundColorChanged', backgroundColorHex }));
  }
}

backgroundColorInput.addEventListener('input', () => setBackgroundColor(backgroundColorInput.value, true));

function setSectionTextColor(section, value, notifyNative = false) {
  const control = sectionColorInputs[section];
  if (!control) return;
  const colorHex = /^#[0-9a-f]{6}$/i.test(value) ? value.toUpperCase() : '#FFFFFF';
  control.input.value = colorHex.toLowerCase();
  control.value.textContent = colorHex;
  if (notifyNative) {
    window.chrome?.webview?.postMessage(JSON.stringify({ type: 'osdSectionTextColorChanged', section, colorHex }));
  }
}

Object.entries(sectionColorInputs).forEach(([section, control]) => {
  control.input.addEventListener('input', () => setSectionTextColor(section, control.input.value, true));
});

function setLayoutMode(value, notifyNative = false) {
  layoutMode = value === 'horizontal' ? 'horizontal' : 'vertical';
  layoutButtons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.layout === layoutMode)));
  if (notifyNative) {
    window.chrome?.webview?.postMessage(JSON.stringify({ type: 'osdLayoutChanged', layoutMode }));
  }
}

layoutButtons.forEach(button => button.addEventListener('click', () => setLayoutMode(button.dataset.layout, true)));

function showSaved() {
  saveStatus.textContent = 'Settings saved';
  saveStatus.classList.add('visible');
  clearTimeout(statusTimeout);
  statusTimeout = setTimeout(() => saveStatus.classList.remove('visible'), 1800);
}

saveButton.addEventListener('click', () => {
  const metrics = selectedMetrics();
  const opacityPercent = Number(opacitySlider.value);
  const backgroundColorHex = backgroundColorInput.value.toUpperCase();
  const sectionTextColors = Object.fromEntries(Object.entries(sectionColorInputs).map(([section, control]) => [
    `${section}TextColorHex`, control.input.value.toUpperCase()
  ]));
  window.chrome?.webview?.postMessage(JSON.stringify({ type: 'save', metrics, opacityPercent, backgroundColorHex, ...sectionTextColors, layoutMode }));
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
  if (message.type === 'load' && Number.isFinite(message.opacityPercent)) setOpacityPercent(message.opacityPercent);
  if (message.type === 'load' && typeof message.backgroundColorHex === 'string') setBackgroundColor(message.backgroundColorHex);
  if (message.type === 'load' && typeof message.layoutMode === 'string') setLayoutMode(message.layoutMode);
  if (message.type === 'load') {
    for (const section of Object.keys(sectionColorInputs)) {
      if (typeof message[`${section}TextColorHex`] === 'string') setSectionTextColor(section, message[`${section}TextColorHex`]);
    }
  }
  if (message.type === 'saved') showSaved();
  if (message.type === 'sensorUpdate' && Array.isArray(message.readings)) window.manwinSensorReadings = message.readings;
  if (message.type === 'sensorError') window.manwinSensorError = message.message;
});

// Ask the native host for its persisted settings only after the message handler
// is installed, so the initial selection cannot be lost during WebView startup.
window.chrome?.webview?.postMessage(JSON.stringify({ type: 'ready' }));
