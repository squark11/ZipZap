# Deploy Dowózka.pl

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
