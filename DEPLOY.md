# Deploy Dowózka.pl

## Podgląd `app.dowózka.pl` na testowym API — 2026-10-01, przygotowane lokalnie, NIEWDROŻONE
Host `app.dowózka.pl` (`app.xn--dowzka-dxa.pl`) utworzył właściciel. Podgląd = ta sama aplikacja Flutter web, ale
zbudowana na **osobne testowe API i osobną bazę** (nigdy produkcyjne `dowozka-api.onrender.com` — skrypt odmawia), ze
znacznikiem „PODGLĄD" w rogu każdego ekranu.

**Tablica gotowości** (✓ tak · ✗ nie · — nie dotyczy):

| Element | Gotowe w kodzie | Sprawdzone lokalnie | Wdrożone | Sprawdzone na żywo |
|---|---|---|---|---|
| DNS `app.xn--dowzka-dxa.pl` → `185.135.90.143` (lh.pl) | — | — | ✓ (właściciel) | ✓ odczyt 2026-09-30 |
| Certyfikat TLS (Let's Encrypt, `app` + `www.app`, do 29.12.2026) | — | — | ✓ | ✓ łańcuch zaufany |
| Przekierowanie HTTP → HTTPS | — | — | ✗ (HTTP zwraca 403) | ✗ |
| Pliki aplikacji w katalogu głównym subdomeny | ✓ `build-preview.ps1` → `build/web` + ZIP | ✓ ZIP rozpakowany w Linuksie = 87/87 plików | ✗ (`/` zwraca 403 — pusty katalog) | ✗ |
| `.htaccess` (SPA-fallback + `Cache-Control: no-cache`) | ✓ `mobile/hosting/app.htaccess` (w paczce) | ✓ Apache 2.4 | ✗ | ✗ |
| Trasy `/s/{slug}`, `?src=`, odświeżenie, `/login?from=`, `/forgot-password` → `index.html` | ✓ | ✓ Apache 2.4 | ✗ | ✗ |
| Service worker (zakres `/`, nowa wersja po wdrożeniu) | ✓ | ✓ Edge headless + Apache | ✗ | ✗ |
| Osobne testowe API + baza | ✓ szablon `deploy/preview/preview.env.example` | ✓ próba 2026-10-02: API w trybie publicznym na osobnej bazie i roli | ✗ | ✗ |
| Konto administratora podglądu (seed pomijany w trybie publicznym) | ✓ `deploy/preview/bootstrap-admin.ps1` | ✓ próba: konto założone, logowanie OK, domyślnego admina brak | ✗ | ✗ |
| CORS tylko z `app.dowózka.pl` | ✓ | ✓ próba: punycode wpuszczony, obce originy bez nagłówka | ✗ | ✗ |
| Blokada produkcyjnego API w paczce | ✓ | ✓ skrypt odmawia | — | — |
| Znacznik „PODGLĄD" | ✓ `APP_ENV=preview` | ✓ | ✗ | ✗ |
| Telefony (iPhone Safari, Android Chrome) | — | ✗ nie testowane | ✗ | ✗ |

**Kroki (właściciel; niczego z tego nie wykonano):**
1. **Testowe API** — osobna usługa Render (np. `dowozka-api-preview`, ten sam Dockerfile) z gałęzi wybranej przez
   właściciela (wymaga wypchnięcia gałęzi — decyzja właściciela) i **osobna baza** (Neon: nowa gałąź/baza, nie
   produkcyjna). Kolejność:
   a. utwórz bazę podglądu (hasło inne niż domyślne);
   b. **konto administratora** — tryb publiczny celowo pomija seed, więc świeża baza nie ma żadnego konta. Uruchom raz,
      lokalnie: `powershell -ExecutionPolicy Bypass -File deploy/preview/bootstrap-admin.ps1 -ConnectionString "<baza
      podglądu>" -AdminEmail <e-mail> -ConfirmDatabase <nazwa bazy>` (pyta o hasło, min. 12 znaków; API startuje tylko
      na `127.0.0.1`, wykonuje migracje, zakłada konto, sprawdza logowanie i się zamyka). NIE na bazie produkcyjnej;
   c. w usłudze ustaw zmienne z `deploy/preview/preview.env.example` (`PILOT__PUBLIC=true`, własne
      `CONNECTIONSTRINGS__POSTGRES` i `JWT__SIGNINGKEY`, `CORS__ALLOWEDORIGINS__0=https://app.dowózka.pl`,
      `PUBLICAPP__CUSTOMERAPPURL=https://app.dowózka.pl`, `PILOT__PAYMENTMODE=test`), health check `/health/ready`;
   d. po starcie: panel nie jest częścią podglądu — dane demo załóż wywołaniem `POST /api/admin/seed/pilot` (admin).
   Poczty nie ustawiaj — podgląd ma pocztę-atrapę (aplikacja pokaże ostrzeżenie; reset hasła i potwierdzanie adresu
   testujemy lokalnie na Mailpit, bo testowego panelu nie ma). Dwa ostrzeżenia w logu startu (poczta-atrapa,
   `Identity:PublicUrl`) są w podglądzie oczekiwane.
2. **Paczka:** `powershell -ExecutionPolicy Bypass -File mobile/hosting/build-preview.ps1 -ApiBaseUrl https://<testowe-api>/api`
   — buduje z `--base-href /`, kopiuje `mobile/hosting/app.htaccess` jako `build/web/.htaccess`, sprawdza paczkę
   (base href, pliki, `.htaccess`, adres API w `main.dart.js`, brak produkcyjnego API) i tworzy
   `mobile/build/dowozka-app-preview.zip` (ścieżki z „/", razem z `.htaccess`).
3. **Wgranie:** menedżer plików lh.pl → katalog główny subdomeny `app` → wgraj ZIP → „Rozpakuj" → usuń ZIP. Włącz
   pokazywanie plików ukrytych i sprawdź, że jest `.htaccess` (przy FTP: włącz wysyłanie plików ukrytych).
4. **HTTPS:** w panelu lh.pl włącz wymuszanie SSL dla `app.dowózka.pl` (ustawienie hostingu — nie w `.htaccess`).
5. **Sprawdzenie na żywo:** `curl -I https://app.xn--dowzka-dxa.pl/s/<slug>` → `200 text/html`, `cache-control: no-cache`;
   `curl -I https://app.xn--dowzka-dxa.pl/main.dart.js` → `200`, `no-cache`; `curl -I http://app.xn--dowzka-dxa.pl/` →
   `301` na `https://`; potem telefony: skan QR z testowego sklepu → oferta → koszyk → odświeżenie → logowanie.

## Odzyskiwanie hasła — 2026-10-01, lokalnie, NIEWDROŻONE
- **Jeden wspólny adres dla wszystkich kont** (klient aplikacji Flutter, sklep, kierowca, admin):
  `{IDENTITY__PUBLICURL}/reset-password#token=…` → strona w panelu (bez logowania, działa też w telefonie).
  Aplikacja: „Nie pamiętasz hasła?" → e-mail → link otwiera stronę w przeglądarce → po zmianie hasła powrót do aplikacji.
  Panel: link przy polu hasła → ten sam formularz. Projekt ekranów: Figma „Dowózka.pl — Odzyskiwanie hasła (UX/UI)".
- **Produkcja wymaga:** `IDENTITY__PUBLICURL=https://panel.xn--dowzka-dxa.pl` **i wdrożenia panelu z tej gałęzi**
  (obecny panel na `panel.dowózka.pl` działa po HTTPS, ale nie ma jeszcze strony resetu — link z e-maila pokazałby
  logowanie). `panel.dowozka.pl` (bez „ó") ma inny IP i nieprawidłowy certyfikat — nie używać. Aktualnej wartości w
  Render nie widać z repo: panel → Konfiguracja → Status i sekrety → „Link resetu hasła".
- **Potwierdzenie adresu e-mail** (lokalnie, NIEWDROŻONE): link z e-maila po rejestracji (klient aplikacji i właściciel
  sklepu) to `{IDENTITY__PUBLICURL}/verify-email#token=…` (już wysłane maile z `?token=` nadal działają) → publiczna
  strona w panelu, która woła istniejące `POST /api/identity/email/verify`. Stany: potwierdzono / już potwierdzony /
  wygasł / nieprawidłowy / błąd sieci z ponowieniem. Kody API: `validation.verify_token_invalid|expired|used` (400).
  Awaria poczty nie psuje rejestracji (zadanie w outboxie, patrz „Trwałość"). **Ponowna wysyłka z interfejsu**
  (istniejące `POST /api/identity/email/resend-verification`, zalogowany; maks. 3 linki/h na konto →
  `validation.verify_resend_limit`): panel — pasek „Potwierdź adres e-mail" u góry po zalogowaniu; aplikacja — karta
  na ekranie „Konto". Stan potwierdzenia: `GET /api/identity/me` → `isEmailVerified` (z bazy). Strona wygasłego linku
  kieruje do logowania (sesja panelu żyje tylko w karcie, więc link z e-maila zawsze otwiera się bez sesji).
  Wymaga tego samego wdrożenia panelu co reset hasła.
- **Trwałość e-maili z linkami (2026-10-02):** prośba o reset i link potwierdzający to **zadania w outboxie modułu
  Identity** (`PasswordResetEmailRequested`, `VerificationEmailRequested`) — zapisane w bazie w tej samej transakcji co
  operacja, więc przetrwają restart API; przy awarii poczty outbox ponawia bez limitu (10 s → maks. co 30 min), a
  zadania czekające widać w panelu „Zdarzenia". Ładunek zadania nie zawiera tokenu (token powstaje przy wysyłce; po
  nieudanej wysyłce jest usuwany). Gwarancja „co najmniej raz": w rzadkim przypadku awarii tuż po wysyłce użytkownik
  może dostać dwa e-maile (oba linki działają do pierwszego użycia). Brak nowej migracji. Przy włączonym RabbitMQ
  (`RabbitMq:Host`; dziś nieużywany) ponowienia idą wg polityki brokera — ograniczona liczba prób, potem kolejka martwych.
- **Poczta:** bez `EMAIL__HTTP__APIKEY` i SMTP poczta jest **atrapą** — API loguje ostrzeżenie w trybie publicznym, panel
  i aplikacja pokazują ostrzeżenie na formularzu, „Status" pokazuje atrapę. Render blokuje SMTP → HTTP API (Resend/Brevo).
- **Bezpieczeństwo:** ta sama odpowiedź i ten sam czas dla istniejącego i nieistniejącego konta (endpoint zapisuje
  tylko zadanie w outboxie; konto, token i wysyłka — w tle); maks. 3 linki/h na konto + limit 10/min na IP; token 256 bitów, w bazie tylko hash, ważny 1 h,
  jednorazowy (zużycie atomowe); po zmianie hasła wszystkie sesje i pozostałe linki są unieważniane; token we
  fragmencie `#` (nie trafia do logów serwera WWW ani nagłówka Referer), od razu usuwany z paska adresu; logi zawierają
  tylko zamaskowany adres i kategorię błędu; stany linku: nieprawidłowy / wygasł / użyty (kody
  `validation.reset_token_*`).
- **Test lokalny na atrapowym serwerze pocztowym (Mailpit):**
  `docker run -d --name zz-mailpit -p 127.0.0.1:1025:1025 -p 127.0.0.1:8025:8025 axllent/mailpit`, API z
  `--Email:Smtp:Host=127.0.0.1 --Email:Smtp:Port=1025 --Email:Smtp:UseSsl=false --Email:Smtp:FromEmail=no-reply@dowozka.test`
  (SMTP bez TLS dozwolony wyłącznie dla localhost), wiadomości: `http://127.0.0.1:8025`.

## S2 — kod QR sklepu → aplikacja webowa (bez instalacji) — 2026-09-29, lokalnie, NIEWDROŻONE
**Architektura:** istniejąca aplikacja Flutter zbudowana jako web (PWA) — ten sam kod co na Androidzie, bez nowego
frontendu. Adresy bez `#` (path URL strategy); stały link do oferty sklepu: **`https://<host aplikacji>/s/<slug-sklepu>?src=qr`**
(każda lokalizacja = osobny sklep = osobny slug i osobny kod). `src` służy tylko do liczenia wejść (licznik w API), po
zliczeniu znika z adresu. Kod QR generuje panel (Start sklepu / Sklepy → „Kod QR", PNG i SVG do druku, opcjonalna
etykieta miejsca, np. `qr-kasa`). Landing `dowózka.pl` kieruje „Zamów online" i swój QR do aplikacji web, nie do APK.

**Kanoniczny adres aplikacji: `https://app.dowózka.pl`** (decyzja właściciela, 2026-09-29) — w DNS, certyfikacie,
kodach QR i nagłówku `Origin` występuje jako punycode **`app.xn--dowzka-dxa.pl`** (to ten sam adres). Aplikacja działa
**tylko w katalogu głównym** tej subdomeny (build z `--base-href /`, `sw.js` w `/`, zakres service workera `/`): API
odrzuca adres aplikacji ze ścieżką (np. `https://dowózka.pl/app`), panel nie wygeneruje z takim adresem kodu QR, a landing
nie pokaże linku. Zmiana hosta po wydruku unieważnia wydrukowane kody (chyba że stary host przekierowuje `/s/*` na nowy).
Konfiguracja jest przygotowana w kodzie; **DNS, hosting, Render i adres produkcyjny NIE są zmienione**.

**Uruchomienie QR w pilotażu (kolejność):**
1. **DNS** — rekord dla `app.xn--dowzka-dxa.pl` u dostawcy hostingu aplikacji. Jeśli będzie to lh.pl (jak `dowózka.pl`
   i `panel.dowózka.pl`): rekord `A` → `185.135.90.143` i katalog w `public_html/<folder>/` przypisany do subdomeny
   `app` jako jej **katalog główny** (nie podkatalog landingu). Azure SWA odrzuca IDN (tylko przez Cloudflare, patrz niżej).
2. **TLS** — certyfikat (Let's Encrypt w panelu lh.pl) wystawiony dokładnie na `app.xn--dowzka-dxa.pl`. Sprawdź, że
   przeglądarka nie pokazuje ostrzeżenia (dla `panel.dowózka.pl` certyfikat NIE był wystawiony — ten sam błąd zablokuje
   aplikację i kody QR). Przekierowanie HTTP→HTTPS włącz opcją hostingu („wymuś SSL"), nie edycją `.htaccess` aplikacji.
3. **Build aplikacji:** `cd mobile; flutter build web --release --base-href / --dart-define=API_BASE_URL=https://dowozka-api.onrender.com/api`
4. **`.htaccess` (SPA-fallback + nagłówki pamięci podręcznej)** — wzorzec w repozytorium: **`mobile/hosting/app.htaccess`**.
   Po buildzie **skopiuj go ręcznie jako `mobile/build/web/.htaccess`**:
   `Copy-Item mobile/hosting/app.htaccess mobile/build/web/.htaccess -Force`.
   - Flutter kopiuje do `build/web/` wszystko z `mobile/web/`, także pliki z kropką — ale `mobile/web/.htaccess` jest
     tylko lokalnym, nieśledzonym plikiem właściciela (w czystym klonie go nie ma), a obecna wersja nie ma nagłówka
     `Cache-Control`. Wzorzec = te same reguły przepisywania + `Header set Cache-Control "no-cache"`.
   - Dlaczego nagłówek jest konieczny (sprawdzone 2026-09-30 na Apache 2.4 + Edge): pliki Fluttera nie mają wersji w
     nazwach; bez `Cache-Control` przeglądarka uznaje `main.dart.js` heurystycznie za świeży i po wdrożeniu kolejny skan
     QR dostaje **nowy `index.html` ze starym `main.dart.js`** (z pamięci podręcznej, z pominięciem service workera).
     Z nagłówkiem `no-cache` każdy plik jest rewalidowany (tani 304) i po wdrożeniu od razu ładuje się nowa wersja.
   - Sprawdzone lokalnie na Apache 2.4: `/s/<slug>`, `/s/<slug>?src=qr-kasa`, `/s/<slug>/`, `/login?from=…` i odświeżenie →
     `index.html` (200); istniejące pliki (`sw.js`, `main.dart.js`, `manifest.json`, ikony) serwowane bez przepisywania,
     `/.htaccess` i listing katalogów → 403. Uwaga: brakujący plik w `/assets/` też dostaje `index.html` (typowe dla SPA).
   - Azure SWA (`staticwebapp.config.json`) i Netlify (`_redirects`) w `mobile/web/` to niekanoniczne alternatywy — bez
     nagłówków `Cache-Control`; przy zmianie hosta trzeba je dodać analogicznie.
5. **Upload** zawartości `mobile/build/web/` do katalogu głównego subdomeny (FTP jak dla landingu/panelu). Wiele klientów
   FTP ukrywa pliki z kropką — sprawdź, że `.htaccess` jest na serwerze (`.last_build_id` można pominąć).
6. **API (Render)** — po zatwierdzeniu przez właściciela: `PUBLICAPP__CUSTOMERAPPURL=https://app.dowózka.pl` (API zapisuje
   i zwraca `https://app.xn--dowzka-dxa.pl`); w trybie hartowanym także `CORS__ALLOWEDORIGINS__<n>=https://app.dowózka.pl`
   (API dopisuje wersję punycode — przeglądarka wysyła `Origin` w punycode). W panelu: Konfiguracja → „Aplikacja klienta
   i kody QR" — ten sam adres (panel pokazuje, skąd pochodzi aktywna wartość).
7. **Landing:** w `public_html/dowozka.pl/config.js` ustaw `appUrl: "https://app.dowózka.pl"` — dopiero gdy aplikacja
   działa (plik jest w `web-landing/config.js`; puste = sekcja „Zamów online" pokazuje „wkrótce", bez QR). Plik APK na
   serwerze (`/pobierz/dowozka.apk`) nie jest już linkowany — usunięcie decyzją właściciela.
8. **Sprawdzenie przed drukiem** (na prawdziwych telefonach): iPhone — **Safari** (App Store nie jest potrzebny), Android —
   Chrome: skan kodu z panelu → oferta sklepu → dodanie do koszyka → odświeżenie → logowanie/rejestracja → zamówienie
   (W1: konto testera). Opcjonalnie „Dodaj do ekranu głównego" (instrukcja w aplikacji, ikona w nagłówku sklepu).
9. Dopiero potem: panel → sklep → „Miejsce kodu" (np. `kasa`) → „Pobierz SVG (do druku)" / PNG. Pobranie rejestruje
   etykietę miejsca (`qr-kasa`) — tylko takie etykiety są liczone osobno (patrz „Licznik wejść" niżej).

**Lista kontrolna przed pierwszym drukiem QR** (wszystko musi być ✓):
- **DNS:** `nslookup app.xn--dowzka-dxa.pl` zwraca adres hostingu aplikacji; subdomena wskazuje katalog z zawartością
  `build/web` jako katalog główny.
- **TLS:** `https://app.dowózka.pl/` otwiera się bez ostrzeżenia w Safari (iPhone) i Chrome (Android); certyfikat obejmuje
  `app.xn--dowzka-dxa.pl`; `http://` przekierowuje na `https://`.
- **Hosting:** `curl -I https://app.xn--dowzka-dxa.pl/s/<slug>` → `200`, `text/html`, `cache-control: no-cache`;
  `curl -I https://app.xn--dowzka-dxa.pl/main.dart.js` → `200`, JavaScript, `no-cache`; `/sw.js` → JavaScript.
- **CORS/API:** `GET https://dowozka-api.onrender.com/api/config/public` zwraca `"customerAppUrl":"https://app.xn--dowzka-dxa.pl"`;
  aplikacja z `https://app.dowózka.pl` ładuje ofertę sklepu (bez błędów CORS w konsoli). Tryb Development pilotażu
  przyjmuje każdy origin; w trybie hartowanym origin musi być na liście `CORS__ALLOWEDORIGINS__*`.
- **Integracje (jeśli włączone):** logowanie Google — `https://app.xn--dowzka-dxa.pl` w „Authorized JavaScript origins";
  captcha — host `app.xn--dowzka-dxa.pl` na liście domen widżetu.
- **Migracje:** `Catalog_StoreEntryStats` i `Catalog_QrSourcesAndEntryRetention` zastosowane (automatycznie przy starcie
  API po scaleniu — dopiero po zgodzie właściciela i kopii bazy).
- **Kod:** zeskanowany telefonem kod z panelu otwiera `https://app.xn--dowzka-dxa.pl/s/<slug>?src=qr-<miejsce>` (aparat
  może pokazać adres w punycode — to ten sam adres), a licznik w panelu rośnie w zarejestrowanym miejscu.

**Licznik wejść — limit i retencja** (bez danych osobowych; źródło niczego nie odblokowuje):
- Stałe kubełki: `qr`, `landing`, `landing-qr`, `direct`, `qr-other`, `other` + maks. **20 zarejestrowanych etykiet
  miejsc** (`qr-…`) na sklep. Etykietę rejestruje tylko obsługa sklepu/admin (panel robi to przy pobraniu/kopiowaniu/
  otwarciu kodu; `POST /api/catalog/stores/{id}/qr-sources`, limit pilnowany blokadą wiersza sklepu — odporny na
  równoległe żądania). Nieznana etykieta `qr-…` → `qr-other`, każde inne źródło → `other`. Publiczny licznik nie może więc
  założyć nowych wierszy: najwyżej 26 wierszy na sklep dziennie.
- Retencja: dzienne liczniki **90 dni** (panel pokazuje maks. 90 dni wstecz), starsze są raz dziennie zwijane w sumy
  miesięczne `catalog.store_entry_monthly` (jedno polecenie SQL: usunięcie + dopisanie, bez podwójnego liczenia przy kilku
  instancjach). 90 dni wystarcza do porównania miejsc kodów w pilotażu (kwartał), a sumy miesięczne zachowują trend
  sezonowy bez rosnącej tabeli dziennej (maks. ~2 340 wierszy dziennych na sklep).

## ✅ Strona `dowózka.pl` na hostingu lh.pl + aplikacja jako APK — 2026-09-15 (stan wdrożony; QR→APK zastąpione w S2)
> Właściciel ma domenę **dowózka.pl** (IDN, punycode `xn--dowzka-dxa.pl`) i hosting **lh.pl** — domena już wskazuje na lh.pl (A `185.135.90.143`), serwuje HTTP/HTTPS (SSL Let's Encrypt lh.pl). Architektura:
- **`dowózka.pl` = strona marketingowo-biznesowa** (`web-landing/index.html`), statyczna, hostowana na lh.pl w `public_html/dowozka.pl/`. Główne CTA B2B „Dołącz jako sklep" + sekcja pobrania aplikacji. **Aplikacja Flutter NIE jest serwowana na desktopie** — jest tylko mobilna (dystrybucja przez APK).
- **Aplikacja klienta = Android APK** do pobrania: `https://dowózka.pl/pobierz/dowozka.apk` (na stronie przycisk + kod QR; iOS „wkrótce"). APK celuje w API na Render (`--dart-define=API_BASE_URL=…onrender.com/api`). Podpisany kluczem debug (sideload/pilot; do Google Play trzeba własnego keystore).
- **Panel admina** nadal na Azure SWA (`thankful-river-…azurestaticapps.net`); docelowo subdomena `panel.dowózka.pl` na lh.pl. Landing linkuje na razie do panelu na Azure.
- **Backend** → Render, **baza** → Neon (bez zmian).
- **Deploy strony (FTP na lh.pl):** dane FTP w gitignorowanym `.env` (blok `FTP:` — Serwer `serwer325339.lh.pl`, user, hasło). Upload przez `curl -T` lub `.NET FtpWebRequest`; katalog `public_html/dowozka.pl/`. APK → `public_html/dowozka.pl/pobierz/`.
- **Build APK (środowisko):** JDK 17 (Microsoft OpenJDK, winget), Android SDK w `C:\Users\kacpe\AppData\Local\Android\Sdk` (cmdline-tools + platform-tools + platforms 34/35/36 + build-tools 35/36 + NDK r28c), licencje zaakceptowane plikami w `Sdk\licenses\`. `flutter config --android-sdk … --jdk-dir …`. ⚠ **Workaround:** AGP 9.1.0 nie akceptuje `getDefaultProguardFile('proguard-android.txt')` w pluginie `flutter_inappwebview_android` 1.1.3 (ciągnie go `cloudflare_turnstile`) — trzeba podmienić na `proguard-android-optimize.txt` w cache pub (`%LOCALAPPDATA%\Pub\Cache\hosted\pub.dev\flutter_inappwebview_android-1.1.3\android\build.gradle`), **bez BOM**. Docelowo: aktualizacja pluginu/turnstile albo pin AGP.

## ⏸️ Frontendy na Azure Static Web Apps (Free) — 2026-09-15 (zapas)
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
