# Twinpack website

Static Hugo site for twinpack.dev: a short marketing page, a live package search and package pages
with install instructions. It is served from the same host as the Twinpack API
(`https://twinpack.dev/index.php/...`) and talks to it from the browser. There is no login.

## Develop

Install Hugo (extended, 0.120 or newer), then

```bash
cd website
hugo server
```

and open http://localhost:1313/website/ (the site lives in a `/website/` folder, see Deployment). By default the site calls the API on its own origin, which does not
exist locally, so point it at production (read-only, anonymous):

```powershell
$env:HUGO_PARAMS_APIBASE = "https://twinpack.dev/index.php"; hugo server
```

```bash
HUGO_PARAMS_APIBASE=https://twinpack.dev/index.php hugo server
```

The cross-origin calls need the server changes in `Twinpack-Server` (CORS preflight and the
`Twinpack-Client` header). Until they are deployed, browsers will block the requests from localhost.

## Build

```bash
hugo --minify
```

The output is written to `public/`.

## Layout

```
website/
  hugo.toml              site config, links, params.apiBase
  content/               _index.md for home, packages/ and package/ (the pages are rendered by JS)
  layouts/               baseof, home, packages/list.html, package/list.html, partials
  assets/css/main.css    all styles (system fonts, no external requests)
  assets/js/             api.js + ui.js (shared), home.js, packages.js, package.js
  static/images/         logos and screenshots
```

Pages and URLs (all below `/website/` on twinpack.dev):

| URL | Purpose |
| --- | --- |
| `/` | Redirects to `/website/` (done by `index.php` in Twinpack-Server) |
| `/website/` | Marketing page, install steps, popular packages |
| `/website/packages/?q=&page=` | Catalog search |
| `/website/package/?d=<distributor>&n=<name>` | Package details, install commands, versions, dependencies |

## Safety notes

* The API returns text written by package publishers. All of it is rendered with `textContent`
  (never `innerHTML`) and links and icons only accept `http(s):` URLs.
* `baseof.html` sets a Content-Security-Policy: scripts and styles only from the site itself,
  requests only to the site and the API. Do not add inline scripts, inline styles or third-party
  scripts without changing it.
* The site only sends anonymous `GET` requests with credentials omitted. There are no cookies, so
  there is nothing for CSRF to attack. Abuse protection (rate limit, caching, no telemetry for the
  site) lives in the API, see `Twinpack-Server/twinpack/ratelimit.php`.

## Deployment

The site lives in its own folder, `website/`, next to the API on twinpack.dev. `index.php` redirects
a plain visit of `https://twinpack.dev/` to it (`TWINPACK_WEBSITE_PATH` in `config.php`, default
`/website/`). That keeps the site completely apart from `index.php`, `config.php`, `Icons/`,
`Repositories/` and `vsix/`. If you rename the folder, change `baseURL` in `hugo.toml`, `remote` and
`baseUrl` in `Templates/Twinpack/vars/website.groovy` and `TWINPACK_WEBSITE_PATH` together.

Jenkins job `Zeugwerk Twinpack/website` (multibranch, script path `.jenkins/website.groovy`, shared
library function `Templates/Twinpack/vars/website.groovy`). It works like the marketing site:

| Branch | Uploaded to (Hetzner) | URL |
| --- | --- | --- |
| `main` | `twinpack.dev/website/` | https://twinpack.dev/website/ (and `/` redirects there) |
| other | `twinpack.dev-branches/<branch>/` | https://zeugwerk.dev/twinpack.dev-branches/`<branch>`/ (noindex, uses the production API) |

The job runs `hugo --minify` in `website/` and uploads `website/public/**` without cleaning the
target. There is no `robots.txt` (it would only count at the domain root); the package pages are
marked noindex in their HTML instead.

One-time setup on the server and in Jenkins:

1. Create the multibranch job `Zeugwerk Twinpack/website` with script path `.jenkins/website.groovy`.
2. Deploy the updated `index.php` and `ratelimit.php` and copy the new settings from
   `Twinpack-Server/twinpack/config.php.template` into the real `config.php`
   (`TWINPACK_WEBSITE_PATH`, `TWINPACK_RATE_LIMIT_*`, `TWINPACK_TRUSTED_PROXY_HEADER`).
3. Optional, in an `.htaccess` inside `website/`: long caching for the fingerprinted assets.

   ```apache
   <FilesMatch "\.min\.[0-9a-f]{64}\.(css|js)$">
     Header set Cache-Control "public, max-age=31536000, immutable"
   </FilesMatch>
   ```