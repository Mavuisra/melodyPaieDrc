param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ReleaseNotes = "Mise a jour Melody Paie RDC."
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not [version]::TryParse($Version.Trim(), [ref]$null)) {
    throw "Version invalide: $Version (ex. 1.0.1)"
}

$v = [version]$Version.Trim()
$short = if ($v.Build -eq 0 -and $v.Revision -eq 0) { "$($v.Major).$($v.Minor)" } else { $Version.Trim() }
$tag = "v$($Version.Trim())"
$setupFile = "MelodyPaieRDC_Setup_$short.exe"
$repo = "Mavuisra/melodyPaieDrc"
$downloadUrl = "https://github.com/$repo/releases/download/$tag/$setupFile"

# csproj
$csproj = Join-Path $root "MelodyPaieRDC.csproj"
$c = Get-Content $csproj -Raw
$c = $c -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
$c = $c -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$($v.Major).$($v.Minor).$($v.Build).$([Math]::Max(0, $v.Revision))</AssemblyVersion>"
$c = $c -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$($v.Major).$($v.Minor).$($v.Build).$([Math]::Max(0, $v.Revision))</FileVersion>"
Set-Content -Path $csproj -Value $c -Encoding UTF8

# Inno Setup
$iss = Join-Path $root "installer\MelodyPaieRDC.iss"
$i = Get-Content $iss -Raw
$i = $i -replace '#define MyAppVersion "[^"]+"', "#define MyAppVersion `"$Version`""
$i = $i -replace '#define MyAppVersionShort "[^"]+"', "#define MyAppVersionShort `"$short`""
Set-Content -Path $iss -Value $i -Encoding UTF8

# version.json (URL finale apres release CI ; SHA256 complete par Actions)
$manifest = @{
    version      = $Version.Trim()
    downloadUrl  = $downloadUrl
    fileName     = $setupFile
    publishedAt  = (Get-Date -Format "yyyy-MM-dd")
    releaseNotes = $ReleaseNotes.Trim()
    sha256       = ""
}
$manifestPath = Join-Path $root "installer\updates\version.json"
$manifest | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "Version $Version (short=$short) -> $setupFile"
Write-Host "Tag attendu: $tag"
