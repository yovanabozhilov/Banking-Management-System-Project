(function () {
  const COOKIE = 'theme';
  const COOKIE_OPTS = 'path=/; max-age=' + (60 * 60 * 24 * 365) + '; samesite=lax';

  function getCookie(name) {
    return document.cookie.split('; ').find(x => x.startsWith(name + '='))?.split('=')[1];
  }
  function setCookie(name, value) {
    document.cookie = name + '=' + encodeURIComponent(value) + '; ' + COOKIE_OPTS;
  }
  const prefersDark = () => window.matchMedia('(prefers-color-scheme: dark)').matches;

  function applyTheme(mode) {
    let actual = mode;
    if (mode === 'system') actual = prefersDark() ? 'dark' : 'light';
    document.documentElement.setAttribute('data-bs-theme', actual);
    document.documentElement.classList.toggle('dark', actual === 'dark');
  }

  function initTheme() {
    const saved = getCookie(COOKIE);
    const mode = saved ? decodeURIComponent(saved) : 'system';
    applyTheme(mode);

    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    mq.addEventListener?.('change', () => {
      const current = decodeURIComponent(getCookie(COOKIE) || 'system');
      if (current === 'system') applyTheme('system');
    });

    document.querySelectorAll('[data-theme-option]').forEach(r => {
      r.addEventListener('change', () => {
        const selected = r.value;
        setCookie(COOKIE, selected);
        applyTheme(selected);
      });
    });
  }

  function setTheme(mode) {
    setCookie(COOKIE, mode);
    applyTheme(mode);
  }

  window.Theme = { initTheme, setTheme };
})();

document.addEventListener('click', async (e) => {
  const btn = e.target.closest('[data-copy-iban]');
  if (!btn || btn.classList.contains('copy-id-btn')) return;
  const iban = btn.getAttribute('data-copy-iban');
  if (!iban) return;

  try {
    await navigator.clipboard.writeText(iban);
    const original = btn.textContent;
    btn.textContent = 'Copied!';
    setTimeout(() => (btn.textContent = original), 1200);
  } catch {
    alert('Copy failed');
  }
});

document.addEventListener('click', async (e) => {
  const btn = e.target.closest('.copy-id-btn');
  if (!btn) return;

  const id = btn.getAttribute('data-copy-id');
  const iban = btn.getAttribute('data-copy-iban');
  const text = id || iban;
  const icon = btn.querySelector('i');

  if (!text) return;

  try {
    await navigator.clipboard.writeText(text);
    if (icon) {
      const prev = icon.className;
      icon.className = 'fas fa-check';
      setTimeout(() => { icon.className = prev; }, 1200);
    }
  } catch {
    if (icon) {
      const prev = icon.className;
      icon.className = 'fas fa-exclamation-triangle';
      setTimeout(() => { icon.className = prev; }, 1200);
    } else {
      alert('Copy failed');
    }
  }
});
