# ZipZap PWA — multi-stage: build Fluttera → serwowanie przez nginx.
# Build:  docker build -f web.Dockerfile --build-arg API_BASE_URL=https://api.twojadomena/api -t zipzap-web .
# Run:    docker run -p 8090:80 zipzap-web   →  http://localhost:8090

# --- 1) build ---
# Uwaga: cirruslabs nie publikuje tagu 3.47.2 — używamy :stable. Alternatywa do
# web.static.Dockerfile (który serwuje build/web zbudowany lokalnie, bez Fluttera).
FROM ghcr.io/cirruslabs/flutter:stable AS build
WORKDIR /app

# Cache zależności — najpierw manifesty.
COPY pubspec.yaml pubspec.lock ./
RUN flutter pub get

COPY . .

# URL API podawany przy buildzie (domyślnie dev localhost).
ARG API_BASE_URL=""
RUN if [ -n "$API_BASE_URL" ]; then \
      flutter build web --release --dart-define=API_BASE_URL="$API_BASE_URL"; \
    else \
      flutter build web --release; \
    fi

# --- 2) serwowanie ---
FROM nginx:1.27-alpine AS serve
COPY --from=build /app/build/web /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost/ >/dev/null 2>&1 || exit 1
