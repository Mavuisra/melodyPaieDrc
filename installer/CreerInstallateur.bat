@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

echo ============================================================
echo   Melody Paie RDC — Creation de l'installateur
echo ============================================================
echo.

cd /d "%~dp0.."

if not exist "publish\installer" mkdir "publish\installer" 2>nul

echo [1/3] Publication de l'application (win-x64, self-contained — runtime .NET inclus pour le client)...
echo      (clean + publish pour eviter un dossier publish obsole ou incomplet)
dotnet clean "MelodyPaieRDC.csproj" -c Release
if %ERRORLEVEL% neq 0 (
    echo ERREUR: dotnet clean a echoue.
    pause
    exit /b 1
)
rem Uniquement l'app WPF (evite NETSDK1198 si une solution publie aussi ZktecoPullWorker sans profil win-x64)
dotnet publish "MelodyPaieRDC.csproj" -p:PublishProfile=win-x64 -c Release
if %ERRORLEVEL% neq 0 (
    echo ERREUR: La publication a echoue. Fermez Melody Paie RDC si l'exe est verrouille, puis relancez.
    pause
    exit /b 1
)

echo.
echo [2/3] Icone installateur (PNG -^> ICO)...
if exist "Assets\Icon_MelodyPaie_Installer.png" (
    powershell -ExecutionPolicy Bypass -File "installer\PngToIco.ps1" -PngPath "Assets\Icon_MelodyPaie_Installer.png" -IcoPath "Assets\Icon_MelodyPaie_Installer.ico"
) else if exist "Assets\Icon_MelodyPaie.png" (
    powershell -ExecutionPolicy Bypass -File "installer\PngToIco.ps1" -PngPath "Assets\Icon_MelodyPaie.png" -IcoPath "Assets\Icon_MelodyPaie_Installer.ico"
) else (
    echo Avertissement : aucun PNG trouve pour l'icone — verifiez SetupIconFile dans le .iss
)

echo.
echo [3/3] Compilation Inno Setup (pages : bienvenue, licence, infos, taches)...
set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    echo.
    echo Inno Setup 6 est introuvable.
    echo Telechargement : https://jrsoftware.org/isinfo.php
    echo.
    echo Les fichiers publies sont dans : publish\win-x64\
    echo.
    pause
    exit /b 0
)

set "ISCC_EXTRA="
if "%SKIP_MDP_INSTALL%"=="1" set "ISCC_EXTRA=/DBypassMotDePasseInstallation"
"%ISCC%" %ISCC_EXTRA% "%~dp0MelodyPaieRDC.iss"
if %ERRORLEVEL% neq 0 (
    echo ERREUR: ISCC a echoue.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   Termine.
echo   Installateur : publish\installer\MelodyPaieRDC_Setup_1.0.exe
echo   (le numero depend de MyAppVersionShort dans MelodyPaieRDC.iss)
echo ============================================================
explorer "publish\installer" 2>nul
pause
endlocal
