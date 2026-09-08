(function () {
  const select = document.querySelector('#language-select');
  const saved = localStorage.getItem('manwinLanguage') || 'es';
  const text = (selector, value) => { const node = document.querySelector(selector); if (node) node.textContent = value; };
  const translations = {
    es: { language: 'Idioma', dashboard: 'Dashboard', design: 'Diseño OSD', usage: 'Cómo usar', eyebrow: 'PERFORMANCE OVERLAY', system: 'ESTADO DEL SISTEMA', ready: 'ManWin está listo', description: 'Supervisa el rendimiento y configura tu overlay desde aquí.', memory: 'MEMORIA', usageEyebrow: 'PRIMEROS PASOS', usageTitle: 'Cómo usar ManWin', usageDescription: 'Configura PawnIO una sola vez y después controla tu overlay desde aquí.', usageCard: 'Activar el overlay de ManWin', usageText: 'Sigue estos pasos para mostrar el overlay dentro de tus juegos.', usageNote: 'LibreHardwareMonitor proporciona las métricas de hardware. PresentMon proporciona FPS y frametime.', install: 'Instalar PawnIO', cpuWarning: 'Temperatura CPU no disponible', pawnText: 'Puedes instalar PawnIO para permitir el acceso a sensores de bajo nivel.' },
    en: { language: 'Language', dashboard: 'Dashboard', design: 'OSD Design', usage: 'How to use', eyebrow: 'PERFORMANCE OVERLAY', system: 'SYSTEM STATUS', ready: 'ManWin is ready', description: 'Monitor performance and configure your overlay from here.', memory: 'MEMORY', usageEyebrow: 'GETTING STARTED', usageTitle: 'How to use ManWin', usageDescription: 'Install PawnIO once, then control your overlay from here.', usageCard: 'Enable the ManWin overlay', usageText: 'Follow these steps to display the overlay in your games.', usageNote: 'LibreHardwareMonitor provides hardware metrics. PresentMon provides FPS and frametime.', install: 'Install PawnIO', cpuWarning: 'CPU temperature unavailable', pawnText: 'Install PawnIO to allow access to low-level hardware sensors.' }
  };
  function apply(locale) {
    const t = translations[locale];
    window.manwinLocale = locale;
    if (select) select.value = locale;
    text('.language-picker label', t.language); text('[data-view="dashboard-view"]', t.dashboard); text('[data-view="design-view"]', t.design); text('[data-view="usage-view"]', t.usage);
    text('#page-title', document.querySelector('.nav.active')?.textContent || t.dashboard); text('header .eyebrow', t.eyebrow); text('#dashboard-eyebrow', t.system); text('#dashboard-title', t.ready); text('#dashboard-description', t.description); text('#memory-label', t.memory); text('#dashboard-design-action', t.design); text('#dashboard-usage-action', t.usage);
    text('#usage-eyebrow', t.usageEyebrow); text('#usage-title', t.usageTitle); text('#usage-description', t.usageDescription); text('#usage-card-title', t.usageCard); text('#usage-card-text', t.usageText); text('#usage-note', t.usageNote); text('#pawnio-title', t.cpuWarning); text('#pawnio-text', t.pawnText); text('#install-pawnio', t.install);
    text('#process-picker-title', locale === 'en' ? 'Target game' : 'Juego objetivo');
    text('#process-picker-description', locale === 'en' ? 'Select a game to keep the process measured by PresentMon locked.' : 'Selecciona un juego para mantener fijo el proceso medido por PresentMon.');
  }
  select?.addEventListener('change', () => { localStorage.setItem('manwinLanguage', select.value); apply(select.value); });
  document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => setTimeout(() => apply(window.manwinLocale || saved), 0)));
  apply(saved);
})();
