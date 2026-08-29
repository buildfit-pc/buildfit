const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const select = (selector) => Array.from(document.querySelectorAll(selector));

function animateIntro() {
  if (reducedMotion || !window.anime) return;
  window.anime.timeline({ easing: 'easeOutExpo' })
    .add({ targets: '.hero-copy > p:first-child', opacity: [0, 1], translateY: [14, 0], duration: 520 })
    .add({ targets: '.hero-copy h1', opacity: [0, 1], translateY: [26, 0], duration: 720 }, '-=280')
    .add({ targets: '.hero-copy > p:last-child', opacity: [0, 1], translateY: [18, 0], duration: 560 }, '-=440')
    .add({ targets: '.hero-stats > *', opacity: [0, 1], translateY: [22, 0], scale: [.96, 1], delay: window.anime.stagger(80), duration: 560 }, '-=400');
}

function setupGsap() {
  if (reducedMotion || !window.gsap) return;
  const { gsap } = window;
  if (window.ScrollTrigger) gsap.registerPlugin(window.ScrollTrigger);
  gsap.from('.control-panel', { opacity: 0, x: -18, duration: .7, ease: 'power3.out', delay: .15 });
  select('.build-card').forEach((card) => {
    gsap.from(card, { opacity: 0, y: 28, duration: .7, ease: 'power3.out', scrollTrigger: { trigger: card, start: 'top 88%', once: true } });
    card.addEventListener('pointermove', (event) => {
      if (event.pointerType === 'touch') return;
      const bounds = card.getBoundingClientRect();
      gsap.to(card, { rotateX: ((event.clientY - bounds.top) / bounds.height - .5) * -2.5, rotateY: ((event.clientX - bounds.left) / bounds.width - .5) * 2.5, transformPerspective: 900, duration: .35, overwrite: 'auto' });
    });
    card.addEventListener('pointerleave', () => gsap.to(card, { rotateX: 0, rotateY: 0, duration: .55, ease: 'elastic.out(1, .5)' }));
  });
}

async function mountSpringOrb() {
  const headerActions = document.querySelector('.app-header .justify-between');
  if (!headerActions || reducedMotion || document.querySelector('#spring-orb')) return;
  const target = document.createElement('div');
  target.id = 'spring-orb';
  target.className = 'spring-orb';
  target.setAttribute('aria-hidden', 'true');
  headerActions.insertBefore(target, headerActions.lastElementChild);
  try {
    const [React, ReactDom, spring] = await Promise.all([
      import('https://esm.sh/react@18.3.1'),
      import('https://esm.sh/react-dom@18.3.1/client'),
      import('https://esm.sh/@react-spring/web@9.7.5?external=react,react-dom')
    ]);
    const Orb = () => {
      const styles = spring.useSpring({ from: { scale: .7, opacity: .35 }, to: async (next) => { while (true) { await next({ scale: 1.18, opacity: 1 }); await next({ scale: .78, opacity: .48 }); } }, config: { tension: 110, friction: 12 } });
      return React.createElement(spring.animated.div, { style: styles });
    };
    ReactDom.createRoot(target).render(React.createElement(Orb));
  } catch { target.remove(); }
}

function refresh() {
  if (reducedMotion || !window.gsap) return;
  window.gsap.fromTo('.build-card', { opacity: 0, y: 10 }, { opacity: 1, y: 0, duration: .35, stagger: .06, overwrite: 'auto' });
  if (window.ScrollTrigger) window.ScrollTrigger.refresh();
}

window.BuildFitMotion = { init: () => { animateIntro(); setupGsap(); mountSpringOrb(); }, refresh };
