<#
.SYNOPSIS
  Jednorazowo zaklada konto administratora w NOWEJ bazie podgladu (i wykonuje na niej migracje), uruchamiajac API
  lokalnie w trybie zwyklym (nasluch tylko na 127.0.0.1). Tryb publiczny (PILOT__PUBLIC=true) celowo pomija seed
  administratora, wiec swieza baza podgladu nie mialaby zadnego konta.

  NIE uruchamiaj na bazie produkcyjnej: API przy starcie wykonuje migracje na wskazanej bazie.

.PARAMETER ConnectionString
  Polaczenie z baza PODGLADU (ta sama wartosc, ktora trafi do CONNECTIONSTRINGS__POSTGRES uslugi podgladu).

.PARAMETER AdminEmail
  E-mail administratora podgladu.

.PARAMETER ConfirmDatabase
  Nazwa bazy wpisana recznie - potwierdzenie, ze to baza podgladu, a nie produkcyjna.

.NOTES
  Haslo: zmienna srodowiskowa ZZ_BOOTSTRAP_ADMIN_PASSWORD albo pytanie na ekranie (nie trafia do historii polecen).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy/preview/bootstrap-admin.ps1 -ConnectionString "<baza podgladu>" -AdminEmail admin@example.com -ConfirmDatabase dowozka_preview
#>
param(
    [Parameter(Mandatory = $true)][string]$ConnectionString,
    [Parameter(Mandatory = $true)][string]$AdminEmail,
    [Parameter(Mandatory = $true)][string]$ConfirmDatabase,
    [int]$Port = 5099,
    [string]$Dotnet = 'dotnet',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'

$csb = New-Object System.Data.Common.DbConnectionStringBuilder
$csb.set_ConnectionString($ConnectionString)
$dbHost = "$($csb['Host'])"; $dbName = "$($csb['Database'])"
if (-not $dbHost -or -not $dbName) { throw 'Connection string musi zawierac Host i Database.' }
if ($dbName -ne $ConfirmDatabase) {
    throw "Potwierdzenie nie zgadza sie: baza w polaczeniu to '$dbName', a -ConfirmDatabase to '$ConfirmDatabase'. Przerwano."
}
if ($AdminEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { throw "Nieprawidlowy e-mail administratora: $AdminEmail" }

$password = $env:ZZ_BOOTSTRAP_ADMIN_PASSWORD
if (-not $password) {
    $secure = Read-Host -AsSecureString "Haslo administratora podgladu (min. 12 znakow)"
    $password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
if ($password.Length -lt 12 -or $password -eq 'Admin123!') { throw 'Haslo za slabe: min. 12 znakow i nie domyslne.' }

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$base = "http://127.0.0.1:$Port"
Write-Output "Baza podgladu: host=$dbHost, baza=$dbName. Start API lokalnie na $base (migracje + konto $AdminEmail)..."

# Sekrety tylko w zmiennych srodowiskowych procesu potomnego (nie w wierszu polecen).
$envBackup = @{}
$vars = @{
    'ASPNETCORE_ENVIRONMENT'      = 'Development'
    'ASPNETCORE_URLS'             = $base
    'ConnectionStrings__Postgres' = $ConnectionString
    'Seed__AdminEmail'            = $AdminEmail
    'Seed__AdminPassword'         = $password
    'Pilot__Public'               = 'false'
}
foreach ($k in $vars.Keys) { $envBackup[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $vars[$k]) }

$log = Join-Path $env:TEMP "zz-bootstrap-admin-$Port.log"
$dotnetArgs = @('run', '--project', 'backend/src/ZipZap.Api', '-c', 'Release', '--no-launch-profile')
if ($NoBuild) { $dotnetArgs += '--no-build' }
$proc = Start-Process -FilePath $Dotnet -ArgumentList $dotnetArgs -WorkingDirectory $repo -RedirectStandardOutput $log `
    -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($i = 0; $i -lt 90 -and -not $proc.HasExited; $i++) {
        try { Invoke-RestMethod "$base/health/ready" -TimeoutSec 3 | Out-Null; $ready = $true; break } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ready) { throw "API nie zglosilo gotowosci (migracje?). Log: $log" }

    try {
        $login = Invoke-RestMethod -Method Post -Uri "$base/api/identity/login" -ContentType 'application/json' `
            -Body (@{ email = $AdminEmail; password = $password } | ConvertTo-Json)
        if ($login.user.roles -notcontains 'Admin') { throw "Konto $AdminEmail istnieje, ale nie ma roli Admin." }
        Write-Output "OK: administrator $AdminEmail istnieje w bazie '$dbName' i loguje sie podanym haslem."
        Write-Output 'Dalej: ustaw zmienne z deploy/preview/preview.env.example w usludze podgladu (PILOT__PUBLIC=true) i ja uruchom.'
    }
    catch {
        throw "Logowanie administratora nie powiodlo sie. Jesli w tej bazie byl juz administrator, seed niczego nie zmienil - uzyj istniejacego konta. ($($_.Exception.Message))"
    }
}
finally {
    if (-not $proc.HasExited) {
        # dotnet run uruchamia proces potomny API - zatrzymaj takze jego (nasluch na porcie).
        Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
            ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    foreach ($k in $vars.Keys) { [Environment]::SetEnvironmentVariable($k, $envBackup[$k]) }
    $password = $null
}
