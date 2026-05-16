@echo off
setlocal EnableExtensions
chcp 65001 >nul 2>&1

echo ============================================================
echo   Melody Paie RDC - Creation de l installateur
echo ============================================================
echo.

cd /d "%~dp0.."
if errorlevel 1 (
    echo ERREUR: impossible d acceder au dossier du projet.
    pause
    exit /b 1
)

if not exist "publish\installer" mkdir "publish\installer" 2>nul

echo [1/3] Publication win-x64 self-contained...
dotnet clean "MelodyPaieRDC.csproj" -c Release
if errorlevel 1 (
    echo ERREUR: dotnet clean a echoue.
    echo Verifiez que le SDK .NET 8 est installe et dans le PATH.
    pause
    exit /b 1
)

dotnet publish "MelodyPaieRDC.csproj" -p:PublishProfile=win-x64 -c Release
if errorlevel 1 (
    echo ERREUR: dotnet publish a echoue.
    echo Fermez Melody Paie RDC si l exe est verrouille, puis relancez.
    pause
    exit /b 1
)

echo.
echo [2/3] Icone installateur PNG vers ICO...
if exist "Assets\Icon_MelodyPaie_Installer.png" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "installer\PngToIco.ps1" -PngPath "Assets\Icon_MelodyPaie_Installer.png" -IcoPath "Assets\Icon_MelodyPaie_Installer.ico"
) else if exist "Assets\Icon_MelodyPaie.png" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "installer\PngToIco.ps1" -PngPath "Assets\Icon_MelodyPaie.png" -IcoPath "Assets\Icon_MelodyPaie_Installer.ico"
) else (
    echo Avertissement: aucun PNG pour l icone.
)

echo.
echo [3/3] Compilation Inno Setup...
set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"

if not defined ISCC (
    echo Inno Setup 6 introuvable: https://jrsoftware.org/isinfo.php
    echo Fichiers publies: publish\win-x64\
    pause
    exit /b 0
)

set "ISCC_EXTRA="
if "%SKIP_MDP_INSTALL%"=="1" set "ISCC_EXTRA=/DBypassMotDePasseInstallation"

"%ISCC%" %ISCC_EXTRA% "%~dp0MelodyPaieRDC.iss"
if errorlevel 1 (
    echo ERREUR: ISCC a echoue.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   Termine.
echo   Installateur: publish\installer\MelodyPaieRDC_Setup_1.0.exe
echo ============================================================
explorer "publish\installer" 2>nul
pause
endlocal
