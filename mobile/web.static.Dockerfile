# Serwuje GOTOWY (zbudowany lokalnie) build/web przez nginx — bez Fluttera w obrazie.
# Zbuduj najpierw lokalnie:
#   flutter build web --release --dart-define=API_BASE_URL=https://dowozka-api.fly.dev/api
# potem: fly deploy (mobile/fly.toml wskazuje ten plik). Szybkie i niezależne od obrazu Fluttera.
FROM nginx:1.27-alpine
COPY build/web /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost/ >/dev/null 2>&1 || exit 1
