# Publie une version : met a jour les fichiers, push main, tag vX.Y.Z -> declenche GitHub Actions (build + Release)
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Notes = "Mise a jour Melody Paie RDC."
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$tag = "v$($Version.Trim())"

Write-Host "=== Melody Paie RDC - Publication $tag ===" -ForegroundColor Cyan

& "$PSScriptRoot\Set-Version.ps1" -Version $Version -ReleaseNotes $Notes

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git introuvable"
}

$status = git status --porcelain
if ($status) {
    git add MelodyPaieRDC.csproj installer/MelodyPaieRDC.iss installer/updates/version.json
    git commit -m "chore(release): prepare $Version"
}

git push origin main

$existing = git tag -l $tag
if ($existing) {
    Write-Host "Tag $tag existe deja. Push force du tag pour relancer la CI." -ForegroundColor Yellow
    git tag -f $tag
} else {
    git tag $tag
}

git push origin $tag --force

Write-Host ""
Write-Host "Termine. GitHub Actions compile l installateur et cree la Release." -ForegroundColor Green
Write-Host "Suivi: https://github.com/Mavuisra/melodyPaieDrc/actions" -ForegroundColor Cyan
Write-Host "Clients: manifeste mis a jour automatiquement sur main apres le build." -ForegroundColor Cyan
