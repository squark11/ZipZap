# Deploy Dowózka.pl

## ✅ Frontendy na Azure Static Web Apps (Free) — 2026-09-15 (stan bieżący)
> Frontendy przeniesione z Netlify na **Azure SWA Free** (darmowy na stałe, darmowy SSL, globalny CDN). Backend nadal **Render**, baza **Neon** — bez zmian. CORS backendu = `AllowAnyOrigin`, więc nowe originy działają bez zmian.
- **PWA (klient):** https://yellow-sea-04acc220f.6.azurestaticapps.net — SWA `dowozka-pwa`, RG `dowozka-rg`, region `eastus2`, sku `Free`.
- **Panel admina:** https://thankful-river-02050190f.5.azurestaticapps.net — SWA `dowozka-admin`.
- **SPA-fallback:** `staticwebapp.config.json` (`navigationFallback` → `/index.html`) w `mobile/web/` i `admin-panel/public/` (kopiowane do buildu). Odpowiednik `_redirects` z Netlify.
- **Redeploy** (Azure CLI zalogowane jako właściciel; SWA CLI przez `npx`, token pobierany z Azure — nie trzymać w repo):
```powershell
$az="C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
# PWA
cd mobile; flutter build web --release --dart-define=API_BASE_URL=https://dowozka-api.onrender.com/api
$env:SWA_CLI_DEPLOYMENT_TOKEN=(& $az staticwebapp secrets list -n dowozka-pwa -g dowozka-rg --query properties.apiKey -o tsv)
npx -y @azure/static-web-apps-cli deploy build/web --env production
# Panel
cd ..\admin-panel; npm run build
$env:SWA_CLI_DEPLOYMENT_TOKEN=(& $az staticwebapp secrets list -n dowozka-admin -g dowozka-rg --query properties.apiKey -o tsv)
npx -y @azure/static-web-apps-cli deploy dist/admin-panel/browser --env production
```
- **Domena `dowózka.pl` (IDN) — ograniczenie Azure:** SWA **nie przyjmuje domen IDN** (punycode `xn--dowzka-dxa.pl` → REST 51005 „Name is not valid"), także żadnej subdomeny pod nią. Rozwiązanie: **Cloudflare (Free) przed Azure** — DNS+TLS na Cloudflare, proxy do SWA z **Host Header Override** na nazwę `*.azurestaticapps.net` (bez override SWA zwraca **404** dla obcego `Host` — zweryfikowane), SSL/TLS = **Full**. Rekordy (wszystkie Proxied): apex `@` + `www` CNAME → `yellow-sea-04acc220f.6.azurestaticapps.net`; `panel` CNAME → `thankful-river-02050190f.5.azurestaticapps.net`. NS domeny zmienić u seohost na nameservery Cloudflare.

## ⏸️ Poprzednio: Render + Netlify + Neon (Netlify jako fallback)
> Migracja z Fly.io (trial się skończył, 2026-09-15). Backend → **Render**, statyczne frontendy → **Netlify**, baza → **Neon** (bez zmian).
- **API:** https://dowozka-api.onrender.com (Render, service `srv-dakj0oou01pc73faip10`, Docker z `backend/Dockerfile`, region frankfurt) — baza **Neon** Postgres. Health: `/health`, `/health/ready`. ⚠ **Free tier usypia po ~15 min bezczynności** → pierwsze żądanie po przerwie ~30–60 s (cold start). Przed pokazem rozgrzej: `GET /health`.
- **PWA (klient):** https://dowozka.netlify.app (Netlify site `5a6b2bcf-…`) — instalowalna „Dodaj do ekranu głównego".
- **Panel admina:** https://dowozka-admin.netlify.app (Netlify site `89f6b855-…`) — role: administrator serwisu / administrator sklepu / dostawca; rejestracja sklepu/dostawcy w web.
- **Tryb:** `ASPNETCORE_ENVIRONMENT=Development` na czas pilotażu (mock płatności + seed admina + CORS działają tylko w Development; twardy guard produkcyjny odrzuca `Payments:Provider=mock`). Hardening produkcyjny przy podpięciu realnego P24 przed wizytą.
- **Sekrety:** env-vary usługi Render (NIE w repo). Realne wartości + wygenerowany `Jwt__SigningKey` i hasło admina: gitignorowany `deploy/prod.local.env`. Tokeny hostingu (`RENDER_API_KEY`, `NETLIFY_AUTH_TOKEN`): gitignorowany `.env`.
- Frontendy: API base wpięty przy buildzie — PWA `--dart-define=API_BASE_URL=…onrender.com/api`, panel heurystyką hosta w `admin-panel/src/app/api.ts`. Dockerfile backendu binduje `$PORT` (Render) z fallbackiem 8080.

**Redeploy backendu (Render):** push na `main` → Render auto-deploy (autoDeploy=yes). Ręcznie: dashboard usługi → „Manual Deploy", albo API `POST /v1/services/{id}/deploys`.
**Redeploy PWA (Netlify):**
```bash
cd mobile
flutter build web --release --dart-define=API_BASE_URL=https://dowozka-api.onrender.com/api
npx netlify-cli deploy --prod --dir=build/web --site=5a6b2bcf-7ebd-42cc-8998-a8c9d6d0f9bc   # NETLIFY_AUTH_TOKEN w env
```
**Redeploy panelu (Netlify):**
```bash
cd admin-panel
npm run build
npx netlify-cli deploy --prod --dir=dist/admin-panel/browser --site=89f6b855-e894-409c-8ded-109c455b5f87
```
**Seed sklepów demo** (Admin): `POST https://dowozka-api.onrender.com/api/admin/seed/pilot` (idempotentny; odświeża URL-e logo/zdjęć na aktualny host).
> Netlify: nowe zespoły mają domyślnie **„Team protection" (SSO)** — wyłączone per-site i na koncie (`sso_login=false`), inaczej strona przekierowuje na login Netlify.
> Pliki `_redirects` (`mobile/web/`, `admin-panel/public/`) dają SPA-fallback na Netlify. Configi Fly (`*/fly.toml`, `web.static.Dockerfile`) zostają jako legacy.

## Twoje konta i sekrety
Wszystkie konta/wartości do uzupełnienia są w **[`.env.example`](.env.example)** —
skopiuj do `.env` i wpisz swoje (`.env` jest w `.gitignore`, nie trafi do repo):
```bash
cp .env.example .env    # potem uzupełnij wartości CHANGE_ME / puste
```
Grupy: baza (Postgres/Neon), `Jwt__SigningKey`, konto admina (`Seed__*`), płatności
(`Payments__*` — mock teraz, P24 po podpięciu adaptera), Google OAuth (`Google__ClientId`),
e-mail SMTP (opcjonalnie), `WEB_API_BASE_URL` dla PWA, oraz tokeny hostingu/CI (Cloudflare/
Fly.io/Neon — trzymaj jako **sekrety CI**, nie w repo). **Nigdy nie commituj prawdziwych sekretów.**

> W panelu administratora (zakładka **Konfiguracja**, w budowie) część ustawień
> nie-sekretnych (godziny fal dostaw, prowizja, waluta) będzie edytowalna z UI; sekrety
> zostają w env/CI.

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
  Na telefonie: „Dodaj do ekranu głównego" → instaluje się jak aplikacja (ikona Dowózka.pl).
- **Backend:** Fly.io / Render / Railway (Docker z `backend/Dockerfile`).
- **DB:** Neon / Supabase / Railway (Postgres). Ustaw `ConnectionStrings__Postgres`.
- **Sekrety prod:** `ASPNETCORE_ENVIRONMENT=Production` (guard odrzuca dev‑owe sekrety),
  `Jwt__SigningKey`, `Payments__*`. Endpointy `mock/pay|complete` są tylko w Development.

## Instalacja na telefonach (P8)

### Ścieżka pilotażu — PWA (gotowe teraz, bez builda natywnego)
1. Zbuduj i wystaw web (jak wyżej): `flutter build web --release --dart-define=API_BASE_URL=https://<api>/api` → hosting HTTPS (Cloudflare Pages / Netlify).
2. Tester otwiera adres w przeglądarce → menu → **„Dodaj do ekranu głównego"** (Android Chrome / iOS Safari). Aplikacja instaluje się z ikoną **Dowózka.pl** i działa pełnoekranowo (manifest `standalone`, offline‑powłoka `sw.js`).
   - Ikony PWA (`web/icons/Icon-192/512*.png`, `favicon.png`) są już **wygenerowane z nowego logo** (teal wózek). `theme_color`/`name` = Dowózka.pl.

### Android natywnie (APK/AAB) — na maszynie z Android SDK
> Ten komputer **nie ma toolchainu Androida** (`flutter doctor` → brak Android Studio/SDK), więc APK budujesz na maszynie deweloperskiej z zainstalowanym Android SDK.
1. **Ikony launchera** (`android/.../res/mipmap-*/ic_launcher.png`) — już wygenerowane z nowego logo (48–192 px). `AndroidManifest` `android:label="Dowózka.pl"`. `applicationId` pozostaje `pl.zipzap.zipzap` (identyfikator techniczny; zmiana zerwałaby klienta Google OAuth Android + SHA‑1).
2. **Klucz podpisu (release)** — utwórz raz:
   ```bash
   keytool -genkey -v -keystore dowozka-release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias dowozka
   ```
   Dodaj `android/key.properties` (poza repo!) i `signingConfigs.release` w `android/app/build.gradle.kts` (zamień tymczasowe podpisywanie kluczem debug).
3. **Build**:
   ```bash
   flutter build apk --release --dart-define=API_BASE_URL=https://<api>/api      # APK do bezpośredniej instalacji
   flutter build appbundle --release --dart-define=API_BASE_URL=https://<api>/api # AAB do Google Play
   ```
4. **Dystrybucja testerom**: albo **plik APK** bezpośrednio (tester włącza „instalacja z nieznanych źródeł"), albo **Google Play → Testy wewnętrzne** (AAB) — link do instalacji dla listy testerów.
5. **Google Sign‑In**: do konsoli OAuth dodaj SHA‑1 klucza podpisującego (debug do testów, a **klucz Play App Signing** do dystrybucji z Play) dla pakietu `pl.zipzap.zipzap`.

### iOS — poza pilotażem
Wymaga konta Apple Developer + TestFlight; robimy po pilotażu Androida.
