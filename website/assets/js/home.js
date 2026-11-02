/* Home page: "popular packages" strip. */
(function () {
  'use strict';

  var TP = window.TP;
  var list = document.getElementById('popular');
  var status = document.getElementById('popular-status');
  if (!TP || !list) return;

  TP.skeletons(list, 6);

  // The catalog has no popularity sort, so take the first page of 100 and sort it here.
  TP.api('catalog', { per_page: 100, page: 1 }).then(function (res) {
    var packages = (Array.isArray(res.data) ? res.data : []).slice();
    packages.sort(function (a, b) { return (b.downloads || 0) - (a.downloads || 0); });
    TP.clear(list);
    if (!packages.length) {
      list.classList.add('hidden');
      status.textContent = 'No packages found.';
      status.classList.remove('hidden');
      return;
    }
    packages.slice(0, 6).forEach(function (p) { list.appendChild(TP.packageCard(p)); });
  }).catch(function (err) {
    list.classList.add('hidden');
    status.textContent = TP.errorMessage(err);
    status.className = 'status status--error';
  });
})();
