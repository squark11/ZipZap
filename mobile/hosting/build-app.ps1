<#
.SYNOPSIS
  Buduje paczke aplikacji klienta (Flutter web) pod https://app.dowozka.pl (app.xn--dowzka-dxa.pl).
  Niczego nie wdraza: tworzy mobile/build/web oraz ZIP do wgrania na hosting.

.PARAMETER ApiBaseUrl
  Adres API. Domyslnie PRODUKCYJNE API - wtedy wymagany jest przelacznik -ConfirmProduction.
  Do testow lokalnych: http://localhost:5080/api (bez potwierdzenia).

.PARAMETER ConfirmProduction
  Swiadome potwierdzenie budowy na produkcyjne API (aplikacja bedzie dzialac na prawdziwych danych).

.PARAMETER SkipApiCheck
  Pomija kontrole (tylko odczyt) GET {ApiBaseUrl}/config/public - np. gdy API jest chwilowo niedostepne.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File mobile/hosting/build-app.ps1 -ConfirmProduction
#>
param(
    [string]$ApiBaseUrl = 'https://dowozka-api.onrender.com/api',
    [switch]$ConfirmProduction,
    [switch]$SkipApiCheck,
    [string]$Flutter = 'flutter'
)
$ErrorActionPreference = 'Stop'

$ProductionApiHosts = @('dowozka-api.onrender.com')
$CanonicalAppUrl = 'https://app.xn--dowzka-dxa.pl'

$uri = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$uri)) { throw "Nieprawidlowy adres API: $ApiBaseUrl" }
$isProduction = $ProductionApiHosts -contains $uri.Host.ToLowerInvariant()
if ($isProduction -and -not $ConfirmProduction) {
    throw "STOP: $ApiBaseUrl to PRODUKCYJNE API. Dodaj -ConfirmProduction, jesli swiadomie budujesz wersje produkcyjna."
}
if ($uri.Scheme -ne 'https' -and -not $uri.IsLoopback) {
    throw "API musi byc po HTTPS (app.dowozka.pl dziala po HTTPS - przegladarka zablokuje zadania http)."
}
if (-not $uri.AbsolutePath.TrimEnd('/').EndsWith('/api')) { Write-Warning "Adres API zwykle konczy sie na /api (np. https://host/api)." }

# Kontrola (tylko odczyt): czy API ma juz backend z tej wersji i ustawiony adres aplikacji (baza kodow QR).
if (-not $SkipApiCheck) {
    try { $cfg = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + '/config/public') -TimeoutSec 60 }
    catch { throw "Nie udalo sie odczytac $ApiBaseUrl/config/public ($($_.Exception.Message)). Uzyj -SkipApiCheck, jesli wiesz, co robisz." }
    if ($null -eq $cfg.PSObject.Properties['emailDelivery']) {
        throw "API nie ma jeszcze backendu z tej wersji (brak pola emailDelivery). Najpierw wdroz backend (scalenie do main), potem aplikacje."
    }
    if ($isProduction -and $cfg.customerAppUrl -ne $CanonicalAppUrl) {
        Write-Warning "Adres aplikacji klienta w API to '$($cfg.customerAppUrl)', a nie $CanonicalAppUrl - kody QR i landing nie poprowadza do app.dowozka.pl (PUBLICAPP__CUSTOMERAPPURL / panel)."
    }
    if ($cfg.emailDelivery -eq 'mock') {
        Write-Warning 'Poczta w API to atrapa - reset hasla i potwierdzanie adresu nie zadzialaja (EMAIL__HTTP__APIKEY).'
    }
}

$mobile = Split-Path -Parent $PSScriptRoot
$web = Join-Path $mobile 'build\web'

Push-Location $mobile
try {
    # Flutter pisze ostrzezenia na stderr; w PowerShell 5.1 z 'Stop' przerwaloby to build - o wyniku decyduje kod wyjscia.
    $ErrorActionPreference = 'Continue'
    & $Flutter build web --release --base-href / "--dart-define=API_BASE_URL=$ApiBaseUrl" --no-wasm-dry-run 2>&1 |
        ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($code -ne 0) { throw "flutter build web nie powiodl sie (kod $code)." }
}
finally { Pop-Location }

# .htaccess z repozytorium (SPA-fallback + Cache-Control: no-cache) - nadpisuje ewentualna kopie lokalnego,
# niesledzonego mobile/web/.htaccess, ktora Flutter przenosi do builda.
Copy-Item (Join-Path $PSScriptRoot 'app.htaccess') (Join-Path $web '.htaccess') -Force
Remove-Item (Join-Path $web '.last_build_id') -ErrorAction SilentlyContinue

$problems = @()
$index = [IO.File]::ReadAllText((Join-Path $web 'index.html'))
if ($index -notmatch '<base href="/">') { $problems += 'index.html: brak <base href="/"> (aplikacja musi byc w katalogu glownym subdomeny)' }
foreach ($f in 'index.html', 'sw.js', 'flutter_bootstrap.js', 'main.dart.js', 'manifest.json', '.htaccess') {
    if (-not (Test-Path -LiteralPath (Join-Path $web $f))) { $problems += "brak pliku $f" }
}
$ht = [IO.File]::ReadAllText((Join-Path $web '.htaccess'))
if ($ht -notmatch 'Cache-Control "no-cache"' -or $ht -notmatch 'RewriteRule \^ index\.html') { $problems += '.htaccess bez SPA-fallbacku albo bez Cache-Control' }
$main = [IO.File]::ReadAllText((Join-Path $web 'main.dart.js'))
if (-not $main.Contains($ApiBaseUrl)) { $problems += "main.dart.js nie zawiera adresu API $ApiBaseUrl" }
if ($problems.Count -gt 0) { throw ("Paczka NIEGOTOWA:`n - " + ($problems -join "`n - ")) }

# ZIP do wgrania menedzerem plikow hostingu (z .htaccess - klienci FTP czesto pomijaja pliki z kropka).
$zip = Join-Path $mobile 'build\dowozka-app.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Sciezki w ZIP z '/' (ZipFile.CreateFromDirectory w PowerShell 5.1 zapisuje '\' - na serwerze Linux powstalyby pliki
# o nazwach "assets\..." w katalogu glownym zamiast podkatalogow).
$root = (Resolve-Path -LiteralPath $web).Path.TrimEnd('\') + '\'
$stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $web -Recurse -File -Force | ForEach-Object {
        $entry = $_.FullName.Substring($root.Length).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally { $archive.Dispose(); $stream.Dispose() }
$check = [IO.Compression.ZipFile]::OpenRead($zip)
try { $names = @($check.Entries | ForEach-Object { $_.FullName }) } finally { $check.Dispose() }
if ($names -notcontains '.htaccess') { throw 'ZIP nie zawiera .htaccess' }
if (@($names | Where-Object { $_.Contains('\') }).Count -gt 0) { throw 'ZIP zawiera sciezki z "\"' }
if ($names -notcontains 'assets/FontManifest.json') { throw 'ZIP bez podkatalogu assets/' }

Write-Output ''
Write-Output "Paczka GOTOWA (API: $ApiBaseUrl$(if ($isProduction) { ' - PRODUKCJA' }))."
Write-Output "  katalog: $web"
Write-Output "  ZIP:     $zip ($($names.Count) plikow, w tym .htaccess)"
Write-Output 'Dalej (reczne, poza tym skryptem): rozpakuj ZIP w katalogu glownym subdomeny app.dowozka.pl i sprawdz'
Write-Output 'liste kontrolna w DEPLOY.md -> "app.dowozka.pl na produkcji".'
