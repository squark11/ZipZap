# Serwuje GOTOWY (zbudowany lokalnie) panel Angulara przez nginx — bez Node w obrazie.
# Zbuduj najpierw lokalnie:  npm run build   (→ dist/admin-panel/browser)
# potem: fly deploy (admin-panel/fly.toml wskazuje ten plik).
FROM nginx:1.27-alpine
COPY dist/admin-panel/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost/ >/dev/null 2>&1 || exit 1
