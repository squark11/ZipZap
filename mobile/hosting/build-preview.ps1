<#
.SYNOPSIS
  Buduje paczke PODGLADU aplikacji klienta (Flutter web) pod https://app.dowozka.pl (app.xn--dowzka-dxa.pl),
  skierowana na OSOBNE, TESTOWE API. Niczego nie wdraza: tworzy mobile/build/web oraz ZIP do wgrania.

.PARAMETER ApiBaseUrl
  Adres testowego API, np. https://dowozka-api-preview.onrender.com/api. Produkcyjne API jest zablokowane.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File mobile/hosting/build-preview.ps1 -ApiBaseUrl https://dowozka-api-preview.onrender.com/api
#>
param(
    [Parameter(Mandatory = $true)][string]$ApiBaseUrl,
    [string]$Flutter = 'flutter'
)
$ErrorActionPreference = 'Stop'

# Hosty produkcyjnego API - podglad NIGDY nie moze na nie wskazywac (dane i konta prawdziwych klientow).
$ProductionApiHosts = @('dowozka-api.onrender.com')

$uri = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$uri)) { throw "Nieprawidlowy adres API: $ApiBaseUrl" }
if ($ProductionApiHosts -contains $uri.Host.ToLowerInvariant()) {
    throw "STOP: $ApiBaseUrl to PRODUKCYJNE API. Podglad app.dowozka.pl musi uzywac osobnego testowego API i bazy."
}
if ($uri.Scheme -ne 'https' -and -not $uri.IsLoopback) {
    throw "API podgladu musi byc po HTTPS (app.dowozka.pl dziala po HTTPS - przegladarka zablokuje zadania http)."
}
if (-not $uri.AbsolutePath.TrimEnd('/').EndsWith('/api')) { Write-Warning "Adres API zwykle konczy sie na /api (np. https://host/api)." }

$mobile = Split-Path -Parent $PSScriptRoot
$web = Join-Path $mobile 'build\web'

Push-Location $mobile
try {
    # Flutter pisze ostrzezenia na stderr; w PowerShell 5.1 z 'Stop' przerwaloby to build - o wyniku decyduje kod wyjscia.
    $ErrorActionPreference = 'Continue'
    & $Flutter build web --release --base-href / "--dart-define=API_BASE_URL=$ApiBaseUrl" '--dart-define=APP_ENV=preview' --no-wasm-dry-run 2>&1 |
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
foreach ($h in $ProductionApiHosts) { if ($main.Contains($h)) { $problems += "main.dart.js zawiera produkcyjne API ($h)" } }
if ($problems.Count -gt 0) { throw ("Paczka NIEGOTOWA:`n - " + ($problems -join "`n - ")) }

# ZIP do wgrania menedzerem plikow hostingu (z .htaccess - klienci FTP czesto pomijaja pliki z kropka).
$zip = Join-Path $mobile 'build\dowozka-app-preview.zip'
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
Write-Output "Paczka podgladu GOTOWA (API: $ApiBaseUrl, znacznik PODGLAD w aplikacji)."
Write-Output "  katalog: $web"
Write-Output "  ZIP:     $zip ($($names.Count) plikow, w tym .htaccess)"
Write-Output 'Dalej (reczne, poza tym skryptem): wgraj ZAWARTOSC build/web (albo rozpakuj ZIP) do katalogu glownego subdomeny'
Write-Output 'app.dowozka.pl i sprawdz liste kontrolna w DEPLOY.md -> "Podglad app.dowozka.pl".'
