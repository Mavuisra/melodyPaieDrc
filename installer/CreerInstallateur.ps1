# Melody Paie RDC - Build installateur (PowerShell, plus fiable que .bat sur certains postes)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Melody Paie RDC - Creation de l installateur" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERREUR: dotnet introuvable. Installez le SDK .NET 8." -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path "publish\installer" | Out-Null

Write-Host "[1/3] dotnet clean + publish..." -ForegroundColor Yellow
dotnet clean "MelodyPaieRDC.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish "MelodyPaieRDC.csproj" -p:PublishProfile=win-x64 -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[2/3] Icone ICO..." -ForegroundColor Yellow
$png = if (Test-Path "Assets\Icon_MelodyPaie_Installer.png") { "Assets\Icon_MelodyPaie_Installer.png" }
       elseif (Test-Path "Assets\Icon_MelodyPaie.png") { "Assets\Icon_MelodyPaie.png" }
       else { $null }
if ($png) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File "installer\PngToIco.ps1" -PngPath $png -IcoPath "Assets\Icon_MelodyPaie_Installer.ico"
}

Write-Host "[3/3] Inno Setup..." -ForegroundColor Yellow
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup 6 introuvable. Sortie: publish\win-x64\" -ForegroundColor Yellow
    exit 0
}

$extra = if ($env:SKIP_MDP_INSTALL -eq "1") { "/DBypassMotDePasseInstallation" } else { "" }
& $iscc $extra "$PSScriptRoot\MelodyPaieRDC.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$setup = Get-ChildItem "publish\installer\MelodyPaieRDC_Setup_*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Host ""
Write-Host "Termine: $($setup.FullName)" -ForegroundColor Green
if ($setup) { explorer.exe "/select,`"$($setup.FullName)`"" }
