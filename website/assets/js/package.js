/* /package/?d=<distributor>&n=<name> : package details, install commands, versions, dependencies. */
(function () {
  'use strict';

  var TP = window.TP;
  var PER_PAGE = 25;
  var $ = function (id) { return document.getElementById(id); };
  var stateEl = $('state');
  if (!TP || !stateEl) return;

  var params = new URLSearchParams(window.location.search);
  var name = (params.get('n') || '').trim();
  var distributor = (params.get('d') || '').trim();

  var pkg = null;
  var filters = { branch: '', configuration: '', target: '' };
  var page = 1;
  var pinnedCode = null; // <code> element showing the version-pinned command
  var pinnedVersion = null;

  function fail(text) {
    stateEl.textContent = text;
    stateEl.className = 'status status--error';
    $('package').classList.add('hidden');
  }

  // Quote a value for the command line only when it needs it.
  function quote(v) {
    return /^[A-Za-z0-9._\-\/]+$/.test(v) ? v : '"' + String(v).replace(/"/g, '\\"') + '"';
  }

  function addCommand(v) {
    var cmd = 'twinpack add --package ' + quote(pkg.name);
    if (!v) return cmd;
    cmd += ' --version ' + quote(v.version);
    if (v.branch) cmd += ' --branch ' + quote(v.branch);
    if (v.target) cmd += ' --target ' + quote(v.target);
    if (v.configuration) cmd += ' --configuration ' + quote(v.configuration);
    return cmd;
  }

  function configEntry(v) {
    var entry = { 'distributor-name': pkg['distributor-name'], name: pkg.name };
    if (v) {
      entry.version = v.version;
      if (v.branch) entry.branch = v.branch;
      if (v.target) entry.target = v.target;
      if (v.configuration) entry.configuration = v.configuration;
    }
    return JSON.stringify(entry, null, 2);
  }

  // ---- header and facts ----------------------------------------------------------------------

  function renderHead() {
    var head = TP.clear($('head'));
    head.appendChild(TP.packageIcon(pkg));
    head.appendChild(TP.el('div', {}, [
      TP.el('h1', { text: pkg['display-name'] || pkg.name }),
      TP.el('p', { class: 'muted', text: 'by ' + (pkg['distributor-name'] || 'unknown') }),
      TP.el('p', { text: pkg.description || '' })
    ]));
    document.title = (pkg['display-name'] || pkg.name) + ' | Twinpack';
  }

  function chips(list) {
    return TP.el('div', { class: 'chips' }, (list || []).map(function (x) { return TP.el('span', { class: 'chip', text: x }); }));
  }

  function renderFacts(latest) {
    var dl = TP.clear($('facts'));
    function row(label, value) {
      if (!value || (value.nodeType === undefined && value === '')) return;
      dl.appendChild(TP.el('dt', { text: label }));
      dl.appendChild(TP.el('dd', {}, [typeof value === 'string' ? value : value]));
    }
    row('Latest version', latest ? latest.version : '');
    row('Publisher', pkg['distributor-name']);
    row('Authors', pkg.authors);
    row('License', pkg.license);
    var url = TP.safeUrl(pkg['project-url']);
    if (url) row('Project', TP.el('a', { href: url, target: '_blank', text: pkg['project-url'] }));
    row('Downloads', TP.number(pkg.downloads));
    if (pkg.framework) row('Framework', pkg.framework);
    if ((pkg.branches || []).length) row('Branches', chips(pkg.branches));
    if ((pkg.configurations || []).length) row('Configurations', chips(pkg.configurations));
    if ((pkg.targets || []).length) row('Targets', chips(pkg.targets));
  }

  // ---- install box ---------------------------------------------------------------------------

  function renderInstall(latest) {
    var box = TP.clear($('install'));
    var wrap = TP.el('div', { class: 'install', 'data-tabs': true });

    var list = TP.el('div', { class: 'tabs__list', role: 'tablist' }, [
      TP.el('button', { class: 'tabs__tab', role: 'tab', type: 'button', 'data-tab': 'cli', text: 'Command line' }),
      TP.el('button', { class: 'tabs__tab', role: 'tab', type: 'button', 'data-tab': 'config', text: 'config.json' })
    ]);

    var cliPanel = TP.el('div', { class: 'tabs__panel', role: 'tabpanel', 'data-panel': 'cli' });
    cliPanel.appendChild(TP.codeBlock(addCommand(null), 'Latest version'));
    if (latest) {
      var pinned = TP.codeBlock(addCommand(latest), 'Pinned to a version (choose another one in the table below)');
      pinnedCode = pinned.querySelector('code');
      cliPanel.appendChild(pinned);
    }
    cliPanel.appendChild(TP.codeBlock('twinpack restore', 'Restore everything from config.json, e.g. on a build server'));

    var cfgPanel = TP.el('div', { class: 'tabs__panel', role: 'tabpanel', 'data-panel': 'config' });
    cfgPanel.appendChild(TP.el('p', { text: 'Add this entry to the "packages" list of your PLC in .Zeugwerk/config.json:' }));
    var cfg = TP.codeBlock(configEntry(latest));
    cfgPanel.appendChild(cfg);
    pinnedVersion = { cfg: cfg.querySelector('code') };

    wrap.appendChild(list);
    wrap.appendChild(cliPanel);
    wrap.appendChild(cfgPanel);
    box.appendChild(wrap);
    TP.initTabs(wrap);
  }

  function pin(v) {
    if (pinnedCode) pinnedCode.textContent = addCommand(v);
    if (pinnedVersion) pinnedVersion.cfg.textContent = configEntry(v);
    $('install-panel').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  // ---- dependencies --------------------------------------------------------------------------

  function renderDeps(latest) {
    var deps = (latest && latest.dependencies) || [];
    if (!deps.length) return;
    $('deps-panel').classList.remove('hidden');
    $('deps-intro').textContent = 'Version ' + latest.version + ' needs the following libraries. Twinpack installs them for you.';
    var ul = TP.clear($('deps'));
    deps.forEach(function (d) {
      var label = (d.name || '?') + (d.version && d.version !== '*' ? ' ' + d.version : '');
      var item = TP.el('li');
      // Only dependencies that live on Twinpack have a package page.
      if (d['package-id']) item.appendChild(TP.el('a', { href: TP.packageHref(d['distributor-name'], d.name), text: label }));
      else item.appendChild(document.createTextNode(label));
      if (d['distributor-name']) item.appendChild(TP.el('span', { class: 'muted', text: ' by ' + d['distributor-name'] }));
      ul.appendChild(item);
    });
  }

  // ---- versions table ------------------------------------------------------------------------

  function renderFilters() {
    var host = TP.clear($('filters'));
    [['branch', 'Branch', pkg.branches], ['configuration', 'Configuration', pkg.configurations], ['target', 'Target', pkg.targets]].forEach(function (f) {
      var options = f[2] || [];
      if (options.length < 2) return;
      var select = TP.el('select', { id: 'f-' + f[0] }, [TP.el('option', { value: '', text: 'All' })].concat(options.map(function (o) {
        return TP.el('option', { value: o, text: o });
      })));
      select.addEventListener('change', function () {
        filters[f[0]] = select.value;
        page = 1;
        loadVersions();
      });
      host.appendChild(TP.el('label', {}, [f[1], select]));
    });
  }

  function loadVersions() {
    var table = $('versions');
    var info = $('versions-state');
    info.className = 'status';
    info.textContent = 'Loading versions...';
    table.classList.add('hidden');
    $('versions-pager').classList.add('hidden');

    TP.api('package-versions', {
      name: pkg.name,
      'distributor-name': pkg['distributor-name'],
      branch: filters.branch,
      configuration: filters.configuration,
      target: filters.target,
      page: page,
      per_page: PER_PAGE
    }).then(function (res) {
      var rows = Array.isArray(res.data) ? res.data : [];
      var known = res.total !== null;
      var pages = known ? Math.max(1, Math.ceil(res.total / PER_PAGE)) : null;
      var hasMore = known ? page < pages : rows.length === PER_PAGE;
      $('versions-prev').disabled = page <= 1;
      $('versions-next').disabled = !hasMore;
      $('versions-page').textContent = 'Page ' + page + (known ? ' of ' + pages : '');
      $('versions-pager').classList.toggle('hidden', page <= 1 && !hasMore);
      if (!rows.length) {
        info.textContent = 'No versions found.';
        return;
      }
      info.className = 'status hidden';
      var body = TP.clear($('versions-body'));
      rows.forEach(function (v) {
        var pinBtn = TP.el('button', { type: 'button', class: 'btn btn--ghost btn--sm', text: 'Use' });
        pinBtn.addEventListener('click', function () { pin(v); });
        body.appendChild(TP.el('tr', {}, [
          TP.el('td', { text: v.version }),
          TP.el('td', { text: v.branch }),
          TP.el('td', { text: v.target }),
          TP.el('td', { text: v.configuration }),
          TP.el('td', { class: 'notes', text: v.notes || '' }),
          TP.el('td', {}, [pinBtn])
        ]));
      });
      table.classList.remove('hidden');
    }).catch(function (err) {
      info.className = 'status status--error';
      info.textContent = TP.errorMessage(err);
    });
  }

  $('versions-prev').addEventListener('click', function () { page -= 1; loadVersions(); });
  $('versions-next').addEventListener('click', function () { page += 1; loadVersions(); });

  // ---- start ---------------------------------------------------------------------------------

  if (!name) {
    fail('No package selected.');
    return;
  }

  // package needs a distributor-name parameter but looks the package up by name only.
  TP.api('package', { name: name, 'distributor-name': distributor || name }).then(function (res) {
    if (!res.data || !res.data['package-id']) {
      fail('This package does not exist or is not public.');
      return;
    }
    pkg = res.data;

    // package-resolve returns the best version (main branch first) including its dependencies.
    // It answers {} when there is none, and failing here must not hide the rest of the page.
    return TP.api('package-resolve', { name: pkg.name, 'distributor-name': pkg['distributor-name'] }).then(function (r) {
      return r.data && r.data['package-version-id'] ? r.data : null;
    }, function () { return null; }).then(function (latest) {
      renderHead();
      renderFacts(latest);
      renderInstall(latest);
      renderDeps(latest);
      renderFilters();
      loadVersions();
      stateEl.className = 'status hidden';
      $('package').classList.remove('hidden');
    });
  }).catch(function (err) {
    fail(TP.errorMessage(err));
  });
})();
