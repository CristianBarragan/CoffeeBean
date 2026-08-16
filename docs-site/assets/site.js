(() => {
  const header = document.querySelector('.site-header-inner');
  if (!header) return;

  const nav = header.querySelector('nav');
  const toggle = header.querySelector('.site-menu-toggle');
  if (toggle && nav) {
    toggle.addEventListener('click', () => {
      const open = header.classList.toggle('nav-open');
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      toggle.textContent = open ? 'Close' : 'Menu';
    });

    nav.querySelectorAll('a').forEach(link => {
      link.addEventListener('click', () => {
        header.classList.remove('nav-open');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.textContent = 'Menu';
      });
    });
  }

  const current = window.location.pathname.replace(/\\/g, '/');
  nav?.querySelectorAll('a[data-nav]').forEach(link => {
    const target = new URL(link.href, window.location.href).pathname.replace(/\\/g, '/');
    const active = current === target || (target.endsWith('/index.html') && current === target.slice(0, -10) + '/');
    link.classList.toggle('is-active', active);
    if (active) link.setAttribute('aria-current', 'page');
  });
})();
