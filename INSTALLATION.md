# Installation de Melody Paie RDC

## Pour les utilisateurs finaux (installer le logiciel)

### Méthode simple (sans installateur .exe)

L’application est déjà publiée dans **`publish\win-x64\`** :
- Double-cliquez sur **`MelodyPaieRDC.exe`** pour lancer l’application
- Pour un raccourci bureau : clic droit sur l’exe → Envoyer vers → Bureau
- Pour distribuer : copiez tout le dossier `win-x64` ou exécutez `installer\CreerPackageDistribution.bat` pour créer un fichier ZIP

### Méthode avec installateur .exe (nécessite Inno Setup)

1. **Téléchargez** le fichier `MelodyPaieRDC_Setup_1.0.exe` (après création via CreerInstallateur.bat).
2. **Exécutez** le fichier (clic droit → Exécuter en tant qu’administrateur si demandé).
3. Suivez l’assistant d’installation :
   - Choisissez le dossier d’installation (par défaut : `C:\Program Files\Melody Paie RDC`)
   - Optionnel : cochez « Créer une icône sur le bureau »
   - Cliquez sur « Installer »
4. Une fois l’installation terminée, lancez **Melody Paie RDC** depuis le menu Démarrer ou l’icône du bureau.
5. **Première connexion** : utilisez `admin` / `admin` (à modifier ensuite dans Paramètres).

---

## Pour les développeurs (créer l’installateur)

### Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (gratuit)

### Étapes

1. **Ouvrir un terminal** dans le dossier du projet.

2. **Option A – Script automatique**
   ```batch
   installer\CreerInstallateur.bat
   ```

3. **Option B – Commandes manuelles**
   ```batch
   REM 1. Publier l'application
   dotnet publish -p:PublishProfile=win-x64

   REM 2. Créer l'installateur (avec Inno Setup installé)
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MelodyPaieRDC.iss
   ```

4. L’installateur sera généré dans : `publish\installer\MelodyPaieRDC_Setup_1.0.exe`

### Sans Inno Setup (recommandé si pas d’installateur)

Exécutez **`installer\CreerPackageDistribution.bat`** pour créer `publish\MelodyPaieRDC_Portable.zip`.

Sinon, distribuez le dossier `publish\win-x64\` :
- Copiez tout le contenu sur une clé USB ou partage réseau.
- L’utilisateur double-clique sur `MelodyPaieRDC.exe` pour lancer l’application.
- Créez un raccourci manuellement si besoin.

---

## Désinstallation

- **Windows** : Paramètres → Applications → Melody Paie RDC → Désinstaller
- Ou : Panneau de configuration → Programmes et fonctionnalités

## Données

La base de données (`PaieRDC.db`) et les sauvegardes sont stockées dans le dossier `Data` à côté de l’exécutable. Lors d’une désinstallation, ces fichiers ne sont pas supprimés automatiquement.
