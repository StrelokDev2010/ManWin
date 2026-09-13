(() => {
  const carousel = document.querySelector('#screenshots-carousel');
  if (!carousel) return;

  const slides = [...carousel.querySelectorAll('.carousel-slide')];
  const dots = [...carousel.querySelectorAll('.carousel-dots button')];
  const current = carousel.querySelector('.carousel-current');
  let activeIndex = 0;

  function showSlide(index) {
    activeIndex = (index + slides.length) % slides.length;
    const relation = document.documentElement.lang === 'en' ? 'of' : 'de';

    slides.forEach((slide, slideIndex) => {
      const isActive = slideIndex === activeIndex;
      slide.hidden = !isActive;
      slide.setAttribute('aria-label', `${slideIndex + 1} ${relation} ${slides.length}`);
    });

    dots.forEach((dot, dotIndex) => {
      dot.setAttribute('aria-current', String(dotIndex === activeIndex));
    });

    current.textContent = String(activeIndex + 1).padStart(2, '0');
    const currentLabel = document.documentElement.lang === 'en' ? 'Screenshot' : 'Captura';
    carousel.querySelector('.carousel-count').setAttribute('aria-label', `${currentLabel} ${activeIndex + 1} ${relation} ${slides.length}`);
  }

  carousel.querySelector('.carousel-prev').addEventListener('click', () => showSlide(activeIndex - 1));
  carousel.querySelector('.carousel-next').addEventListener('click', () => showSlide(activeIndex + 1));
  dots.forEach((dot, index) => dot.addEventListener('click', () => showSlide(index)));
  document.addEventListener('manwin:languagechange', () => showSlide(activeIndex));

  carousel.addEventListener('keydown', event => {
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      showSlide(activeIndex - 1);
    } else if (event.key === 'ArrowRight') {
      event.preventDefault();
      showSlide(activeIndex + 1);
    }
  });

  let touchStartX = null;
  carousel.addEventListener('touchstart', event => {
    touchStartX = event.changedTouches[0].screenX;
  }, { passive: true });
  carousel.addEventListener('touchend', event => {
    if (touchStartX === null) return;
    const deltaX = event.changedTouches[0].screenX - touchStartX;
    if (Math.abs(deltaX) > 45) showSlide(activeIndex + (deltaX < 0 ? 1 : -1));
    touchStartX = null;
  }, { passive: true });

  showSlide(activeIndex);
})();
