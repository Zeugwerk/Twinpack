/* /packages/ : live catalog search with paging. State lives in the URL (?q=&page=). */
(function () {
  'use strict';

  var TP = window.TP;
  var PER_PAGE = 24;
  var form = document.getElementById('search-form');
  var input = document.getElementById('q');
  var results = document.getElementById('results');
  var summary = document.getElementById('summary');
  var pager = document.getElementById('pager');
  var prev = document.getElementById('prev');
  var next = document.getElementById('next');
  var pageInfo = document.getElementById('page-info');
  if (!TP || !form) return;

  var state = { q: '', page: 1 };
  var inflight = null;

  function readUrl() {
    var p = new URLSearchParams(window.location.search);
    state.q = (p.get('q') || '').slice(0, 64);
    state.page = Math.max(1, parseInt(p.get('page'), 10) || 1);
    input.value = state.q;
  }

  function writeUrl(push) {
    var p = new URLSearchParams();
    if (state.q) p.set('q', state.q);
    if (state.page > 1) p.set('page', state.page);
    var qs = p.toString();
    var url = window.location.pathname + (qs ? '?' + qs : '');
    window.history[push ? 'pushState' : 'replaceState'](null, '', url);
  }

  function showStatus(text, isError) {
    summary.textContent = text;
    summary.className = 'status' + (isError ? ' status--error' : '');
  }

  function load() {
    if (inflight) inflight.abort();
    inflight = new AbortController();
    results.setAttribute('aria-busy', 'true');
    pager.classList.add('hidden');
    TP.skeletons(results, 6);
    showStatus('Searching...', false);

    TP.api('catalog', { search: state.q, page: state.page, per_page: PER_PAGE }, inflight.signal).then(function (res) {
      var packages = Array.isArray(res.data) ? res.data : [];
      // The total comes from the Link response header. If the browser cannot read it, a full page
      // is the best hint that there is more.
      var known = res.total !== null;
      var pages = known ? Math.max(1, Math.ceil(res.total / PER_PAGE)) : null;
      var hasMore = known ? state.page < pages : packages.length === PER_PAGE;
      TP.clear(results);
      results.setAttribute('aria-busy', 'false');

      prev.disabled = state.page <= 1;
      next.disabled = !hasMore;
      pageInfo.textContent = 'Page ' + state.page + (known ? ' of ' + pages : '');
      pager.classList.toggle('hidden', state.page <= 1 && !hasMore);

      if (!packages.length) {
        showStatus(state.q ? 'No packages found for "' + state.q + '".' : 'No packages found.', false);
        return;
      }
      packages.forEach(function (p) { results.appendChild(TP.packageCard(p)); });
      var count = known ? TP.number(res.total) : (hasMore || state.page > 1 ? 'Showing ' + packages.length : String(packages.length));
      var noun = known && res.total === 1 ? ' package' : ' packages';
      showStatus(count + noun + (state.q ? ' for "' + state.q + '"' : ''), false);
    }).catch(function (err) {
      if (err && err.name === 'AbortError') return;
      TP.clear(results);
      results.setAttribute('aria-busy', 'false');
      showStatus(TP.errorMessage(err), true);
    });
  }

  var typeAhead = TP.debounce(function () {
    var q = input.value.trim().slice(0, 64);
    if (q === state.q) return;
    state.q = q;
    state.page = 1;
    writeUrl(false);
    load();
  }, 300);

  input.addEventListener('input', typeAhead);
  form.addEventListener('submit', function (ev) {
    ev.preventDefault();
    state.q = input.value.trim().slice(0, 64);
    state.page = 1;
    writeUrl(true);
    load();
  });
  prev.addEventListener('click', function () { state.page -= 1; writeUrl(true); load(); window.scrollTo(0, 0); });
  next.addEventListener('click', function () { state.page += 1; writeUrl(true); load(); window.scrollTo(0, 0); });
  window.addEventListener('popstate', function () { readUrl(); load(); });

  readUrl();
  load();
})();
