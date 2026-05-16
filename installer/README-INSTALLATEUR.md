# Installateur Melody Paie RDC (Inno Setup 6)

## Prérequis

1. **.NET SDK 8** (pour `dotnet publish`)
2. **Inno Setup 6** : https://jrsoftware.org/isinfo.php  
   - Chemins détectés : `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` ou `C:\Program Files\Inno Setup 6\ISCC.exe`

## Générer l’installateur

Depuis l’explorateur, double-cliquez sur **`CreerInstallateur.bat`**, ou en ligne de commande :

```bat
cd chemin\vers\MelodyPaieRDC
installer\CreerInstallateur.bat
```

Étapes exécutées :

1. `dotnet publish` avec le profil **`win-x64`** (**self-contained** — le runtime .NET est inclus pour les postes clients). Le projet `ZktecoPullWorker` est compilé via une cible MSBuild (sans `ProjectReference` vers un exécutable, pour éviter **NETSDK1150**).
2. Génération / mise à jour de l’ICO installateur à partir du PNG (si présent)
3. Compilation du script **`installer\MelodyPaieRDC.iss`**

**Sortie :** `publish\installer\MelodyPaieRDC_Setup_1.0.exe` (le numéro suit `#define MyAppVersionShort` dans le `.iss`).

## Mot de passe technique (installation réservée au fournisseur)

L’installateur **demande un mot de passe** avant tout écran d’assistant. Seule une personne ayant ce mot de passe (Impact Entreprises) peut déployer ce `.exe` : le client qui reçoit uniquement l’application déjà installée ne voit pas cette étape.

1. **Avant de compiler pour la livraison**, définissez un mot de passe fort : dans `MelodyPaieRDC.iss`, remplacez `InstallateurMotDePasseTechnique` (`#define`), **ou** utilisez la ligne de commande :  
   `ISCC /DInstallateurMotDePasseTechnique=VotreSecret "installer\MelodyPaieRDC.iss"`  
   Ne communiquez **jamais** ce mot de passe au client final avec l’installateur.
2. **Build interne sans mot de passe** (développement uniquement, ne pas distribuer) :  
   `ISCC /DBypassMotDePasseInstallation "installer\MelodyPaieRDC.iss"`  
   ou `set SKIP_MDP_INSTALL=1` puis lancer `installer\CreerInstallateur.bat`

**Limite** : le mot de passe est contenu dans le `.exe` comme toute protection d’installateur classique ; une personne très motivée pourrait l’extraire. L’objectif est d’empêcher la **revente facile** du package d’installation, pas une preuve cryptographique absolue.

## Personnalisation du processus d’installation

| Fichier | Rôle |
|--------|------|
| `installer\MelodyPaieRDC.iss` | Script principal : pages, tâches, icônes, version, code Pascal (contrôles) |
| `installer\Textes\Bienvenue.txt` | Texte affiché sur la page **Information** (avant le choix du dossier) |
| `installer\Textes\Licence_utilisation.txt` | **Contrat / conditions** — l’utilisateur doit accepter pour continuer |
| `installer\Textes\ApresInstallation.txt` | Texte après la fin de l’installation (conseils, sauvegardes, etc.) |

Dans le `.iss`, à adapter avant une release :

- `#define MyAppVersion` / `MyAppVersionShort`
- `AppPublisherURL`, `AppSupportURL`, `AppUpdatesURL`
- `AppCopyright`, `AppPublisher`
- Section **`[Tasks]`** : raccourci Bureau, lancement automatique, etc.
- Section **`[Code]`** : règles supplémentaires (ex. version minimale de Windows)

## Publication portable (ZIP)

Sans Inno Setup, utilisez **`installer\CreerPackageDistribution.bat`** → `publish\MelodyPaieRDC_Portable.zip`.

## Dépannage

- **Échec de copie de l’EXE** pendant `dotnet publish` : fermez Melody Paie RDC.
- **ISCC introuvable** : installez Inno Setup 6 ou ajoutez `ISCC.exe` au `PATH` et adaptez le `.bat`.
- **Caractères accentués** dans l’assistant : enregistrer `MelodyPaieRDC.iss` en **UTF-8 avec BOM** si des libellés français en `[Code]` posent problème (les textes externes sont dans `Textes\*.txt` en UTF-8).
