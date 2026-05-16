# Installateur Melody Paie RDC (Inno Setup 6)

## Prérequis

1. **.NET SDK 8** (pour `dotnet publish`)
2. **Inno Setup 6** : https://jrsoftware.org/isinfo.php  
   - Chemins détectés : `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` ou `C:\Program Files\Inno Setup 6\ISCC.exe`

## Publication automatique (GitHub Release + clients)

### En une commande (recommandé)

```powershell
cd chemin\vers\MelodyPaieRDC
.\installer\PublierRelease.ps1 -Version "1.0.1" -Notes "Corrections et nouveautes"
```

1. Met à jour `MelodyPaieRDC.csproj`, `MelodyPaieRDC.iss`, `installer/updates/version.json`
2. Commit + push sur `main`
3. Crée le tag `v1.0.1` et le pousse
4. **GitHub Actions** compile l’installateur, crée la Release, joint le `.exe`, calcule le SHA256 et pousse le manifeste final

Suivi : https://github.com/Mavuisra/melodyPaieDrc/actions

### Depuis GitHub (sans PC local)

**Actions** → **Release Melody Paie RDC** → **Run workflow** → version `1.0.1` + notes.

---

## Générer l’installateur (local uniquement)

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

## Mises à jour automatiques (téléchargement par l'utilisateur)

L'application vérifie un fichier **`version.json`** sur votre serveur (URL configurable).

### 1. Héberger les fichiers

| Fichier | Exemple d'URL |
|---------|----------------|
| Manifeste | `https://votre-domaine.com/melodypaie-rdc/updates/version.json` |
| Installateur | `https://votre-domaine.com/melodypaie-rdc/releases/MelodyPaieRDC_Setup_1.1.exe` |

Modèle fourni : `installer/updates/version.json`.

```json
{
  "version": "1.1.0",
  "downloadUrl": "https://votre-domaine.com/.../MelodyPaieRDC_Setup_1.1.exe",
  "fileName": "MelodyPaieRDC_Setup_1.1.exe",
  "publishedAt": "2026-05-16",
  "releaseNotes": "Corrections et améliorations…",
  "sha256": "OPTIONNEL_HEX_SHA256"
}
```

Alignez **`AppUpdatesURL`** dans `MelodyPaieRDC.iss` avec l'URL du manifeste.

### 2. Publier une nouvelle version

1. Incrémenter `Version` dans `MelodyPaieRDC.csproj` et `MyAppVersion` dans `MelodyPaieRDC.iss`.
2. Lancer `installer\CreerInstallateur.bat`.
3. Uploader le `.exe` généré dans `publish\installer\`.
4. Mettre à jour `version.json` sur le serveur (`version`, `downloadUrl`, notes, `sha256` recommandé).

### 3. Côté client

- Menu **Paramètres** → **Administration** → **Vérifier les mises à jour**.
- Configuration locale : `%LocalAppData%\MelodyPaieRDC\Data\updates-config.json` (`manifestUrl`, `verifierAuDemarrage`).
- Téléchargement dans `%LocalAppData%\MelodyPaieRDC\Data\Updates\`, puis **Installer et quitter** (Inno Setup remplace l'application ; la base SQLite est conservée).

## Publication portable (ZIP)

Sans Inno Setup, utilisez **`installer\CreerPackageDistribution.bat`** → `publish\MelodyPaieRDC_Portable.zip`.

## Dépannage

- **Échec de copie de l’EXE** pendant `dotnet publish` : fermez Melody Paie RDC.
- **ISCC introuvable** : installez Inno Setup 6 ou ajoutez `ISCC.exe` au `PATH` et adaptez le `.bat`.
- **Caractères accentués** dans l’assistant : enregistrer `MelodyPaieRDC.iss` en **UTF-8 avec BOM** si des libellés français en `[Code]` posent problème (les textes externes sont dans `Textes\*.txt` en UTF-8).
