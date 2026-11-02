/* Copy buttons and tabs. Works on static markup and on nodes created later by page scripts. */
(function () {
  'use strict';

  var TP = (window.TP = window.TP || {});

  // ---- copy to clipboard -------------------------------------------------------------------

  function copyText(text) {
    if (navigator.clipboard && window.isSecureContext) return navigator.clipboard.writeText(text);
    return new Promise(function (resolve, reject) {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.className = 'hidden';
      document.body.appendChild(ta);
      ta.select();
      try { document.execCommand('copy') ? resolve() : reject(); } catch (e) { reject(e); }
      document.body.removeChild(ta);
    });
  }

  // <div class="code"><pre>...</pre><button class="code__copy" data-copy>Copy</button></div>
  document.addEventListener('click', function (ev) {
    var btn = ev.target.closest ? ev.target.closest('[data-copy]') : null;
    if (!btn) return;
    var pre = btn.parentNode.querySelector('pre');
    if (!pre) return;
    copyText(pre.textContent).then(function () {
      btn.textContent = 'Copied';
    }, function () {
      btn.textContent = 'Press Ctrl+C';
    }).then(function () {
      setTimeout(function () { btn.textContent = 'Copy'; }, 1500);
    });
  });

  TP.codeBlock = function (text, label) {
    var block = TP.el('div', { class: 'code' }, [
      TP.el('pre', {}, [TP.el('code', { text: text })]),
      TP.el('button', { type: 'button', class: 'code__copy', 'data-copy': true, 'aria-label': 'Copy to clipboard', text: 'Copy' })
    ]);
    if (!label) return block;
    var wrap = TP.el('div', {}, [TP.el('p', { class: 'code__label', text: label }), block]);
    return wrap;
  };

  // ---- tabs --------------------------------------------------------------------------------

  // <div data-tabs><div class="tabs__list" role="tablist"><button class="tabs__tab" role="tab" data-tab="a">..</button></div>
  //   <div class="tabs__panel" role="tabpanel" data-panel="a">..</div></div>
  TP.initTabs = function (root) {
    var tabs = [].slice.call(root.querySelectorAll('[data-tab]'));
    var panels = [].slice.call(root.querySelectorAll('[data-panel]'));
    if (!tabs.length) return;

    function select(tab, focus) {
      tabs.forEach(function (t) {
        var on = t === tab;
        t.setAttribute('aria-selected', on ? 'true' : 'false');
        t.setAttribute('tabindex', on ? '0' : '-1');
      });
      panels.forEach(function (p) {
        p.classList.toggle('hidden', p.getAttribute('data-panel') !== tab.getAttribute('data-tab'));
      });
      if (focus) tab.focus();
    }

    tabs.forEach(function (tab, i) {
      tab.addEventListener('click', function () { select(tab, false); });
      tab.addEventListener('keydown', function (ev) {
        var next = ev.key === 'ArrowRight' ? i + 1 : ev.key === 'ArrowLeft' ? i - 1 : null;
        if (next === null) return;
        ev.preventDefault();
        select(tabs[(next + tabs.length) % tabs.length], true);
      });
    });
    select(tabs[0], false);
  };

  document.addEventListener('DOMContentLoaded', function () {
    [].forEach.call(document.querySelectorAll('[data-tabs]'), TP.initTabs);
  });
})();
