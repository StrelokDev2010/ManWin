const send = type => window.chrome?.webview?.postMessage({type});
const localeText = (spanish, english) => window.manwinLocale === 'en' ? english : spanish;
const connection = document.querySelector('#connection');
const version = document.querySelector('#rtss-version');
const message = document.querySelector('#rtss-message');
const toast = document.querySelector('#toast');
document.querySelectorAll('.preset-grid').forEach(item => item.remove());
document.querySelector('[data-setting="background"]')?.parentElement.remove();
document.querySelector('#background-color')?.parentElement.remove();
document.querySelector('#background-opacity')?.parentElement.remove();
document.querySelector('#restart-rtss')?.remove();
const advancedOptions = document.querySelector('.large-checks');
if (advancedOptions) advancedOptions.insertAdjacentHTML('beforeend', '<label><input type="checkbox" data-setting="power"> Consumo</label><label><input type="checkbox" data-setting="memoryPercent"> Memoria en porcentaje</label><label><input type="checkbox" data-setting="frametimeGraph"> Gráfica de frametime</label>');
if (advancedOptions?.closest('.card')) {
  const overlayCard = advancedOptions.closest('.card');
  const heading = overlayCard.querySelector('h3');
  const description = overlayCard.querySelector('p');
  if (heading) heading.textContent = 'Elementos del OSD';
  if (description) description.textContent = 'Activa las métricas y bloques visuales que quieres mostrar dentro del juego.';
}
const designCard = document.querySelector('.design-card');
const usageNav = document.createElement('button');
usageNav.className = 'nav';
usageNav.dataset.view = 'usage-view';
usageNav.textContent = 'Cómo usar';
document.querySelector('.sidebar nav')?.appendChild(usageNav);
const usageView = document.createElement('section');
usageView.id = 'usage-view';
usageView.className = 'view hidden';
usageView.innerHTML = '<div class="section-intro"><p class="eyebrow">PRIMEROS PASOS</p><h2>Cómo usar ManWin</h2><p class="muted">Configura RTSS una sola vez y después controla tu overlay desde aquí.</p></div><article class="card"><h3>Activar ManWin Clean</h3><p class="muted">Sigue estos pasos para que el overlay aparezca dentro de tus juegos.</p><ol class="setup-steps"><li>Abre RTSS y confirma que <b>On-Screen Display support</b> está activado.</li><li>En RTSS, entra a <b>Plugins</b> y verifica que <b>OverlayEditor.dll</b> esté activo.</li><li>Deja RTSS ejecutándose y abre tu juego.</li><li>Usa <b>Diseño OSD</b> para elegir las métricas que quieres mostrar.</li><li>Si el juego ya estaba abierto, reinícialo para que RTSS recargue el overlay.</li></ol><p class="muted profile-note">Las métricas se obtienen desde los proveedores configurados en RTSS, como Internal HAL o LibreHardwareMonitor.</p></article>';
document.querySelector('main')?.insertBefore(usageView, document.querySelector('#toast'));
if (designCard) designCard.innerHTML = '<h3>Métricas del overlay</h3><p class="muted">Activa o desactiva los bloques que quieres mostrar dentro del juego. Los cambios se aplican automáticamente al layout ManWin Clean.</p>';
if (designCard) designCard.innerHTML = `<h3>Cómo activar MangoHud Clean</h3><p class="muted">Sigue estos pasos una sola vez para usar nuestro overlay con RTSS.</p><ol class="setup-steps"><li>Abre RTSS y confirma que <b>On-Screen Display support</b> está activado.</li><li>En RTSS, entra a <b>Plugins</b> y verifica que <b>OverlayEditor.dll</b> esté activo.</li><li>Deja RTSS ejecutándose y abre tu juego.</li><li>En esta pantalla, activa o desactiva una métrica para guardar y aplicar MangoHud Clean.</li><li>Si el juego ya estaba abierto, reinícialo para que RTSS recargue el overlay.</li></ol><p class="muted profile-note">Las métricas se obtienen desde los proveedores configurados en RTSS, como Internal HAL o LibreHardwareMonitor.</p>`;
if (designCard) designCard.innerHTML = designCard.innerHTML.replaceAll('MangoHud Clean', 'ManWin Clean');
if (designCard) designCard.innerHTML = '<h3>Métricas del overlay</h3><p class="muted">Activa o desactiva los bloques que quieres mostrar dentro del juego. Los cambios se aplican automáticamente al layout ManWin Clean.</p>';
const savedOverlaySettings = JSON.parse(localStorage.getItem('mangoOverlaySettings') || '{}');
const backgroundColor = document.querySelector('#background-color');
const backgroundOpacity = document.querySelector('#background-opacity');
if (backgroundColor && savedOverlaySettings.backgroundColor) backgroundColor.value = savedOverlaySettings.backgroundColor;
if (backgroundOpacity && savedOverlaySettings.backgroundOpacity) backgroundOpacity.value = savedOverlaySettings.backgroundOpacity;
document.querySelectorAll('[data-setting]').forEach(input => {
  if (Object.prototype.hasOwnProperty.call(savedOverlaySettings, input.dataset.setting))
    input.checked = Boolean(savedOverlaySettings[input.dataset.setting]);
  input.addEventListener('change', () => {
    const settings = {};
    document.querySelectorAll('[data-setting]').forEach(item => settings[item.dataset.setting] = item.checked);
    localStorage.setItem('mangoOverlaySettings', JSON.stringify(settings));
  });
});
const initialOverlaySettings = {};
document.querySelectorAll('[data-setting]').forEach(item => initialOverlaySettings[item.dataset.setting] = item.checked);
window.chrome?.webview?.postMessage({type: 'setOverlay', ...initialOverlaySettings});

function showToast(text){ toast.textContent = text; setTimeout(()=>toast.textContent='',2500); }
function update(data){
  if(data.type === 'status'){
    connection.textContent = data.connected ? localeText('RTSS conectado', 'RTSS connected') : localeText('RTSS desconectado', 'RTSS disconnected');
    connection.className = `pill ${data.connected ? 'online' : 'offline'}`;
    version.textContent = data.connected ? `v${data.version}` : '—';
    message.textContent = data.message;
  }
  if(data.type === 'profileResult'){
    showToast(data.message);
  }
  if(data.type === 'layoutResult'){
    showToast(data.message);
  }
  if(data.type === 'rtssRestartResult'){
    showToast(data.message);
  }
  if(data.type === 'applications'){
    const value = (obj, lower) => obj?.[lower] ?? obj?.[lower[0].toUpperCase() + lower.slice(1)];
    const apps = data.applications ?? [];
    const app = apps.filter(item => Number(value(item, 'fps')) > 0)
      .sort((a, b) => Number(value(b, 'fps')) - Number(value(a, 'fps')))[0] ?? apps[0];
    document.querySelector('#app-name').textContent = value(app, 'name') ?? localeText('Ninguna', 'None');
    document.querySelector('#app-stats').textContent = app
      ? `${Number(value(app, 'fps')).toFixed(1)} FPS · ${Number(value(app, 'frameTimeMs')).toFixed(2)} ms · ${value(app, 'api')} · PID ${value(app, 'processId')}`
      : localeText('Abre un juego detectado por RTSS.', 'Open a game detected by RTSS.');
    renderProfiles(apps, value);
  }
}
window.chrome?.webview?.addEventListener('message', e => { update(e.data); if(e.data.type==='error') showToast(e.data.message); });
document.querySelectorAll('[data-action="show"]').forEach(button => button.onclick = () => { send('show'); showToast('Mensaje enviado a RTSS'); });
document.querySelectorAll('[data-action="hide"]').forEach(button => button.onclick = () => { send('hide'); showToast('OSD ocultado'); });
document.querySelectorAll('[data-setting]').forEach(input => input.addEventListener('change', () => {
  const settings = {};
  document.querySelectorAll('[data-setting]').forEach(item => settings[item.dataset.setting] = item.checked);
  settings.backgroundColor = backgroundColor?.value || '#1A1F2B';
  settings.backgroundOpacity = Number(backgroundOpacity?.value || 85);
  localStorage.setItem('mangoOverlaySettings', JSON.stringify(settings));
  window.chrome?.webview?.postMessage({type: 'setOverlay', ...settings});
  showToast('Diseño OSD actualizado');
}));
const updateBackground = () => {
  const settings = {};
  document.querySelectorAll('[data-setting]').forEach(item => settings[item.dataset.setting] = item.checked);
  settings.backgroundColor = backgroundColor?.value || '#1A1F2B';
  settings.backgroundOpacity = Number(backgroundOpacity?.value || 85);
  localStorage.setItem('mangoOverlaySettings', JSON.stringify(settings));
  window.chrome?.webview?.postMessage({type: 'setOverlay', ...settings});
};
backgroundColor?.addEventListener('change', updateBackground);
backgroundOpacity?.addEventListener('input', updateBackground);
const openRtss = document.querySelector('#open-rtss');
if (openRtss) openRtss.onclick = () => {
  window.chrome?.webview?.postMessage({type: 'openRtss'});
  showToast('Abriendo configuración de RTSS');
};
document.querySelectorAll('[data-layout]').forEach(button => button.addEventListener('click', () => {
  window.chrome?.webview?.postMessage({type: 'setLayout', layout: button.dataset.layout});
}));
document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => {
  document.querySelectorAll('[data-view]').forEach(item => item.classList.toggle('active', item === button));
  document.querySelectorAll('.view').forEach(view => view.classList.toggle('hidden', view.id !== button.dataset.view));
  document.querySelector('#page-title').textContent = button.textContent;
}));

function renderProfiles(apps, value){
  const list = document.querySelector('#profile-list');
  if(!list) return;
  list.innerHTML = apps.length ? apps.map((app, index) => {
    const name = value(app, 'name');
    const fps = Number(value(app, 'fps'));
    return `<button class="profile-item" data-profile-index="${index}"><span>${name}</span><small>${fps.toFixed(1)} FPS · ${value(app, 'api')}</small></button>`;
  }).join('') : `<p class="muted">${localeText('No hay aplicaciones detectadas.', 'No applications detected.')}</p>`;
  list.querySelectorAll('[data-profile-index]').forEach(button => button.onclick = () => {
    document.querySelector('#profile-name').value = button.querySelector('span').textContent;
  });
}

document.querySelector('#save-profile').onclick = () => {
  const name = document.querySelector('#profile-name').value.trim();
  if(!name){ showToast(localeText('Selecciona un ejecutable', 'Select an executable')); return; }
  const profiles = JSON.parse(localStorage.getItem('mangoProfiles') || '{}');
  profiles[name] = { fpsLimit: document.querySelector('#profile-fps').value || '0' };
  localStorage.setItem('mangoProfiles', JSON.stringify(profiles));
  window.chrome?.webview?.postMessage({ type: 'saveProfile', executable: name, fpsLimit: Number(profiles[name].fpsLimit) || 0 });
};
send('status');
