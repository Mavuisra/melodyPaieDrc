@echo off
chcp 65001 >nul
echo ========================================
echo   Création du package Melody Paie RDC
echo ========================================
echo.

cd /d "%~dp0.."

echo [1/2] Publication de l'application...
dotnet publish -p:PublishProfile=win-x64
if %ERRORLEVEL% neq 0 (
    echo ERREUR: La publication a échoué.
    pause
    exit /b 1
)

echo.
echo [2/2] Création de l'archive ZIP...
set ZIPNAME=MelodyPaieRDC_Portable.zip
set PUBLISHDIR=publish\win-x64

if exist "%ZIPNAME%" del "%ZIPNAME%"

powershell -Command "Compress-Archive -Path '%PUBLISHDIR%' -DestinationPath 'publish\%ZIPNAME%' -Force"

if %ERRORLEVEL% neq 0 (
    echo ERREUR: Impossible de créer le ZIP.
    echo.
    echo Vous pouvez copier manuellement le dossier : %PUBLISHDIR%
    echo L'utilisateur double-clique sur MelodyPaieRDC.exe pour lancer l'application.
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Terminé !
echo.
echo   Package prêt à distribuer :
echo   publish\%ZIPNAME%
echo.
echo   Pour installer : décompresser le ZIP et lancer MelodyPaieRDC.exe
echo ========================================
explorer publish
pause
