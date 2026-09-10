# Deploy ZipZap

## Lokalny dev
```bash
docker compose up -d postgres          # + rabbitmq jeśli potrzebny
dotnet run --project backend/src/ZipZap.Api --launch-profile http   # API :5080
cd mobile && flutter run -d web-server --web-port 8098              # aplikacja
```
API stosuje migracje i seed przy starcie. Płatność (dev): checkout → strona
`/api/payments/mock/pay` → „Zapłać (sukces)".

## Pełny stack w Dockerze
```bash
# Postgres + RabbitMQ + API
docker compose up -d --build

# PWA klienta (profil "web"; build Fluttera jest wolny)
WEB_API_BASE_URL=http://localhost:5080/api docker compose --profile web up -d --build web
# → http://localhost:8090
```

## Sam web (PWA) w Dockerze
```bash
cd mobile
docker build -f web.Dockerfile --build-arg API_BASE_URL=https://api.twojadomena/api -t zipzap-web .
docker run -p 8090:80 zipzap-web      # nginx serwuje build/web (SPA, wasm, sw.js)
```
> Obraz build używa `ghcr.io/cirruslabs/flutter:3.47.2`. Jeśli tag nie istnieje w
> rejestrze, podmień na `:stable`. Warstwa serwująca (nginx) zweryfikowana lokalnie:
> index/manifest/sw.js/canvaskit.wasm (MIME `application/wasm`)/SPA‑fallback = OK.

## Darmowy hosting (pilotaż)
- **PWA (web):** `flutter build web --release --dart-define=API_BASE_URL=https://<api>/api`,
  potem wrzuć `mobile/build/web` na **Cloudflare Pages / Netlify / Vercel** (HTTPS gratis).
  Na telefonie: „Dodaj do ekranu głównego" → instaluje się jak aplikacja (ikona ZipZap).
- **Backend:** Fly.io / Render / Railway (Docker z `backend/Dockerfile`).
- **DB:** Neon / Supabase / Railway (Postgres). Ustaw `ConnectionStrings__Postgres`.
- **Sekrety prod:** `ASPNETCORE_ENVIRONMENT=Production` (guard odrzuca dev‑owe sekrety),
  `Jwt__SigningKey`, `Payments__*`. Endpointy `mock/pay|complete` są tylko w Development.

## Natywne aplikacje (Android/iOS)
Store‑buildy (appbundle/ipa, podpis, TestFlight/Play) → patrz **R10** w `ROADMAP.md`.
Do pilotażu wystarcza PWA (instalowalna na Androidzie i iOS przez „Dodaj do ekranu").
