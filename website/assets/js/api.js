/* Twinpack API client and small DOM helpers shared by all pages.
 *
 * Everything the server returns is untrusted (package names, descriptions, URLs are chosen by
 * publishers), so nothing here ever uses innerHTML: text goes through textContent and URLs
 * through safeUrl().
 */
(function () {
  'use strict';

  var TP = (window.TP = window.TP || {});
  var body = document.body;
  var API = (body.getAttribute('data-api-base') || '/index.php').replace(/\/+$/, '');
  var PACKAGE_URL = body.getAttribute('data-package-url') || '/package/';

  // ---- API ---------------------------------------------------------------------------------

  function ApiError(kind, message, retryAfter) {
    this.name = 'ApiError';
    this.kind = kind; // 'network' | 'rate' | 'server' | 'api'
    this.message = message;
    this.retryAfter = retryAfter || 0;
  }
  ApiError.prototype = Object.create(Error.prototype);
  TP.ApiError = ApiError;

  function query(params) {
    return Object.keys(params || {})
      .filter(function (k) { return params[k] !== undefined && params[k] !== null && params[k] !== ''; })
      .map(function (k) { return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]); })
      .join('&');
  }

  function totalFrom(res) {
    // The server sends paging info as JSON in the Link header: {"total":N,"self":...,"next":...}
    var link = res.headers.get('Link');
    if (!link) return null;
    try {
      var parsed = JSON.parse(link);
      return typeof parsed.total === 'number' ? parsed.total : null;
    } catch (e) {
      return null;
    }
  }

  // GET <api>/<endpoint>?params. Resolves {data, total}. Rejects with ApiError.
  TP.api = function (endpoint, params, signal) {
    // `client=web` tells the server this is the website (no usage telemetry, own rate limit).
    // It is a query parameter and not a header on purpose: a custom header makes browsers send a
    // CORS preflight, which would break local development against the production API.
    var qs = query(Object.assign({}, params, { client: 'web' }));
    var url = API + '/' + endpoint + '?' + qs;
    return fetch(url, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      credentials: 'omit',
      signal: signal
    }).then(function (res) {
      if (res.status === 429) {
        var wait = parseInt(res.headers.get('Retry-After'), 10) || 30;
        throw new ApiError('rate', 'Too many requests. Please try again in ' + wait + ' seconds.', wait);
      }
      if (!res.ok) throw new ApiError('server', 'The package server answered with an error (' + res.status + ').');
      return res.text().then(function (text) {
        var data;
        try { data = JSON.parse(text); } catch (e) {
          throw new ApiError('server', 'The package server sent an unexpected response.');
        }
        // The API reports failures with HTTP 200 and {"_meta":{"message":...}}.
        if (data && data._meta && data._meta.message) throw new ApiError('api', String(data._meta.message));
        return { data: data, total: totalFrom(res) };
      });
    }, function (err) {
      if (err && err.name === 'AbortError') throw err;
      throw new ApiError('network', 'Could not reach the package server.');
    });
  };

  TP.errorMessage = function (err) {
    if (err instanceof ApiError) return err.message;
    return 'Something went wrong.';
  };

  // ---- helpers -----------------------------------------------------------------------------

  // Only http(s) and same-site relative URLs. Blocks javascript:, data: and friends.
  TP.safeUrl = function (value) {
    if (typeof value !== 'string' || !value) return '';
    try {
      var u = new URL(value, window.location.href);
      return u.protocol === 'https:' || u.protocol === 'http:' ? u.href : '';
    } catch (e) {
      return '';
    }
  };

  // el('a', {class: 'x', href: url, text: 'hi'}, [children]). `href`/`src` are sanitised, on* is refused.
  TP.el = function (tag, props, children) {
    var node = document.createElement(tag);
    Object.keys(props || {}).forEach(function (k) {
      var v = props[k];
      if (v === undefined || v === null || v === false) return;
      if (k === 'class') node.className = v;
      else if (k === 'text') node.textContent = String(v);
      else if (k === 'href' || k === 'src') {
        var safe = TP.safeUrl(v);
        if (safe) node.setAttribute(k, safe);
      } else if (k.slice(0, 2) !== 'on') node.setAttribute(k, v === true ? '' : String(v));
    });
    [].concat(children || []).forEach(function (c) {
      if (c === null || c === undefined || c === false) return;
      node.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
    });
    if (tag === 'a' && node.getAttribute('target') === '_blank') node.setAttribute('rel', 'noopener noreferrer');
    return node;
  };

  TP.clear = function (node) {
    while (node.firstChild) node.removeChild(node.firstChild);
    return node;
  };

  TP.number = function (n) {
    return (Number(n) || 0).toLocaleString('en');
  };

  TP.date = function (value) {
    if (!value) return '';
    var d = new Date(String(value).replace(' ', 'T'));
    return isNaN(d) ? '' : d.toLocaleDateString('en', { year: 'numeric', month: 'short', day: 'numeric' });
  };

  TP.debounce = function (fn, ms) {
    var t;
    return function () {
      var args = arguments;
      clearTimeout(t);
      t = setTimeout(function () { fn.apply(null, args); }, ms);
    };
  };

  TP.packageHref = function (distributor, name) {
    return PACKAGE_URL + '?' + query({ d: distributor, n: name });
  };

  function icon(p) {
    var src = TP.safeUrl(p['icon-url']);
    var label = (p['display-name'] || p.name || '?').charAt(0).toUpperCase();
    var placeholder = TP.el('span', { class: 'pkg__icon pkg__icon--ph', 'aria-hidden': 'true', text: label });
    if (!src) return placeholder;
    var img = TP.el('img', { class: 'pkg__icon', src: src, alt: '', width: 48, height: 48, loading: 'lazy', referrerpolicy: 'no-referrer' });
    img.addEventListener('error', function () { if (img.parentNode) img.parentNode.replaceChild(placeholder, img); });
    return img;
  }
  TP.packageIcon = icon;

  // One catalog entry as a link card.
  TP.packageCard = function (p) {
    var meta = [TP.number(p.downloads) + ' downloads'];
    if (p.modified) meta.push('updated ' + TP.date(p.modified));
    return TP.el('a', { class: 'pkg', href: TP.packageHref(p['distributor-name'], p.name) }, [
      icon(p),
      TP.el('div', { class: 'pkg__body' }, [
        TP.el('h3', { class: 'pkg__name', text: p['display-name'] || p.name }),
        TP.el('p', { class: 'pkg__by', text: 'by ' + (p['distributor-name'] || 'unknown') }),
        TP.el('p', { class: 'pkg__desc', text: p.description || 'No description.' }),
        TP.el('p', { class: 'pkg__meta', text: meta.join(' \u00b7 ') })
      ])
    ]);
  };

  TP.skeletons = function (container, n) {
    TP.clear(container);
    for (var i = 0; i < n; i++) container.appendChild(TP.el('div', { class: 'skeleton', 'aria-hidden': 'true' }));
  };
})();
