(function () {
  const select = document.querySelector('#language-select');
  const saved = localStorage.getItem('manwinLanguage') || 'es';
  const text = (selector, value) => { const node = document.querySelector(selector); if (node) node.textContent = value; };
  const html = (selector, value) => { const node = document.querySelector(selector); if (node) node.innerHTML = value; };
  const fieldLabel = (selector, value) => { const node = document.querySelector(selector); if (!node) return; const input = node.querySelector('input'); node.textContent = value; if (input) node.appendChild(input); };

  const translations = {
    es: {
      language: 'Idioma', version: 'Versión 0.1', dashboard: 'Dashboard', profiles: 'Perfiles', design: 'Diseño OSD', usage: 'Cómo usar',
      eyebrow: 'PERFORMANCE OVERLAY', system: 'ESTADO DEL SISTEMA', hero: 'Controla tu OSD desde una interfaz limpia.',
      heroText: 'ManWin usa RTSS para dibujar el overlay dentro del juego.', rtss: 'RTSS', activeApp: 'Aplicación activa', none: 'Ninguna',
      openGame: 'Abre un juego detectado por RTSS.', detected: 'Aplicaciones detectadas', selected: 'Perfil seleccionado', executable: 'Ejecutable',
      fpsLimit: 'Límite FPS', noLimit: 'Sin límite', save: 'Guardar perfil', localProfiles: 'Los perfiles se guardan localmente.',
      elements: 'Elementos del OSD', elementsText: 'Activa las métricas y bloques visuales que quieres mostrar dentro del juego.',
      gpu: 'GPU', cpu: 'CPU', ram: 'RAM', vram: 'VRAM', fps: 'FPS', frametime: 'Frametime', clocks: 'Frecuencias', power: 'Consumo',
      memoryPercent: 'Memoria en porcentaje', graph: 'Gráfica de frametime', metrics: 'Métricas del overlay',
      metricsText: 'Activa o desactiva los bloques que quieres mostrar dentro del juego. Los cambios se aplican automáticamente al layout ManWin Clean.',
      customization: 'PERSONALIZACIÓN', selectMetrics: 'Selecciona las métricas que se publican dentro del juego.',
      firstSteps: 'PRIMEROS PASOS', howUse: 'Cómo usar ManWin', howText: 'Configura RTSS una sola vez y después controla tu overlay desde aquí.',
      activate: 'Activar ManWin Clean', activateText: 'Sigue estos pasos para que el overlay aparezca dentro de tus juegos.',
      step1: 'Abre RTSS y confirma que On-Screen Display support está activado.',
      step2: 'En RTSS, entra a Plugins y verifica que OverlayEditor.dll esté activo.',
      step3: 'Deja RTSS ejecutándose y abre tu juego.', step4: 'Usa Diseño OSD para elegir las métricas que quieres mostrar.',
      step5: 'Si el juego ya estaba abierto, reinícialo para que RTSS recargue el overlay.',
      providers: 'Las métricas se obtienen desde los proveedores configurados en RTSS, como Internal HAL o LibreHardwareMonitor.'
    },
    en: {
      language: 'Language', version: 'Version 0.1', dashboard: 'Dashboard', profiles: 'Profiles', design: 'OSD Design', usage: 'How to use',
      eyebrow: 'PERFORMANCE OVERLAY', system: 'SYSTEM STATUS', hero: 'Control your OSD from a clean interface.',
      heroText: 'ManWin uses RTSS to draw the overlay inside the game.', rtss: 'RTSS', activeApp: 'Active application', none: 'None',
      openGame: 'Open a game detected by RTSS.', detected: 'Detected applications', selected: 'Selected profile', executable: 'Executable',
      fpsLimit: 'FPS limit', noLimit: 'No limit', save: 'Save profile', localProfiles: 'Profiles are saved locally.',
      elements: 'OSD elements', elementsText: 'Enable the metrics and visual blocks you want to show in-game.',
      gpu: 'GPU', cpu: 'CPU', ram: 'RAM', vram: 'VRAM', fps: 'FPS', frametime: 'Frametime', clocks: 'Clocks', power: 'Power',
      memoryPercent: 'Memory percentage', graph: 'Frametime graph', metrics: 'Overlay metrics',
      metricsText: 'Enable or disable the blocks you want to show in-game. Changes are applied automatically to the ManWin Clean layout.',
      customization: 'CUSTOMIZATION', selectMetrics: 'Select the metrics published inside the game.',
      firstSteps: 'GETTING STARTED', howUse: 'How to use ManWin', howText: 'Configure RTSS once, then control your overlay from here.',
      activate: 'Enable ManWin Clean', activateText: 'Follow these steps to display the overlay in your games.',
      step1: 'Open RTSS and confirm that On-Screen Display support is enabled.',
      step2: 'In RTSS, open Plugins and verify that OverlayEditor.dll is active.',
      step3: 'Leave RTSS running and open your game.', step4: 'Use OSD Design to choose the metrics you want to display.',
      step5: 'If the game was already open, restart it so RTSS reloads the overlay.',
      providers: 'Metrics come from the providers configured in RTSS, such as Internal HAL or LibreHardwareMonitor.'
    }
  };

  function apply(locale) {
    const t = translations[locale];
    window.manwinLocale = locale;
    if (select) select.value = locale;
    text('.language-picker label', t.language);
    html('.side-note', `${t.version}<br><span>RTSS backend</span>`);
    text('[data-view="dashboard-view"]', t.dashboard); text('[data-view="profiles-view"]', t.profiles);
    text('[data-view="design-view"]', t.design); text('[data-view="usage-view"]', t.usage);
    text('#page-title', document.querySelector('.nav.active')?.textContent || t.dashboard);
    text('header .eyebrow', t.eyebrow); text('.hero .eyebrow', t.system); text('.hero h2', t.hero); text('.hero .muted', t.heroText);
    text('.grid article:nth-child(1) h3', t.rtss); text('.grid article:nth-child(2) h3', t.activeApp);
    text('#app-name', t.none); text('#app-stats', t.openGame);
    text('#profiles-view .section-intro .eyebrow', t.profiles); text('#profiles-view .section-intro h2', t.profiles);
    text('#profiles-view .section-intro .muted', locale === 'en' ? 'Save independent preferences for each executable detected by RTSS.' : 'Guarda preferencias independientes para cada ejecutable detectado por RTSS.');
    text('#profiles-view .card:nth-child(1) h3', t.detected); text('#profiles-view .card:nth-child(2) h3', t.selected);
    fieldLabel('#profiles-view .field-label:first-of-type', t.executable); fieldLabel('#profiles-view .field-label:nth-of-type(2)', t.fpsLimit);
    document.querySelector('#profile-name')?.setAttribute('placeholder', 'vkcube.exe'); document.querySelector('#profile-fps')?.setAttribute('placeholder', t.noLimit);
    text('#save-profile', t.save); text('#profiles-view .profile-note', t.localProfiles);
    text('.overlay-config h3', t.elements); text('.overlay-config > p', t.elementsText);
    const labels = { fps: t.fps, frametime: t.frametime, cpu: t.cpu, gpu: t.gpu, ram: t.ram, vram: t.vram, clocks: t.clocks, power: t.power, memoryPercent: t.memoryPercent, frametimeGraph: t.graph };
    document.querySelectorAll('[data-setting]').forEach(input => { const label = labels[input.dataset.setting]; if (label) input.parentElement.lastChild.nodeValue = ` ${label}`; });
    text('#design-view .section-intro .eyebrow', t.customization); text('#design-view .section-intro h2', t.design); text('#design-view .section-intro .muted', t.selectMetrics);
    text('.design-card h3', t.metrics); text('.design-card p', t.metricsText);
    const usage = document.querySelector('#usage-view');
    if (usage) usage.innerHTML = `<div class="section-intro"><p class="eyebrow">${t.firstSteps}</p><h2>${t.howUse}</h2><p class="muted">${t.howText}</p></div><article class="card"><h3>${t.activate}</h3><p class="muted">${t.activateText}</p><ol class="setup-steps"><li>${t.step1}</li><li>${t.step2}</li><li>${t.step3}</li><li>${t.step4}</li><li>${t.step5}</li></ol><p class="muted profile-note">${t.providers}</p></article>`;
  }

  window.manwinTranslations = translations;
  select?.addEventListener('change', () => { localStorage.setItem('manwinLanguage', select.value); apply(select.value); });
  document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => setTimeout(() => apply(window.manwinLocale || saved), 0)));
  apply(saved);
})();
