# Dépannage — client bloqué à la connexion Melody Paie

## Ce n’est PAS le mot de passe de l’installateur

| Mot de passe | Usage |
|--------------|--------|
| **Impact2026** | Uniquement au lancement de `MelodyPaieRDC_Setup_xxx.exe` |
| **Login Melody** | Écran « Connexion » de l’application (admin / autre compte) |

---

## Étape 1 — Vérifications rapides (téléphone ou AnyDesk)

1. **Fermer complètement** Melody (Gestionnaire des tâches → fin de `MelodyPaieRDC.exe` s’il tourne encore).
2. **Identifiant** : souvent `admin` (minuscules, pas d’espace).
3. **Essayer** mot de passe : `admin` (si jamais changé à l’installation).
4. Vérifier **Verrou maj** / clavier AZERTY.
5. Message affiché : *« Identifiant ou mot de passe incorrect »* → mot de passe oublié (suite ci-dessous).

---

## Étape 2 — Si un autre compte Admin fonctionne encore

1. Se connecter avec ce compte.
2. **Paramètres** → **Gestion des utilisateurs**.
3. Sélectionner l’utilisateur bloqué → **Changer mot de passe**.
4. Nouveau mot de passe : **8 caractères minimum**, **lettre + chiffre** (ex. `Melody2026`).
5. Communiquer le nouveau mot de passe au client par téléphone (pas par e-mail non chiffré si possible).

---

## Étape 3 — Personne ne peut se connecter (cas client)

**Sur le PC du client** (prise en main à distance : AnyDesk, TeamViewer, RustDesk…).

### A. Outil fourni par Impact (recommandé)

1. Copier sur le PC client le fichier :  
   `installer\support\publish\MelodyResetMotDePasse.exe`
2. **Fermer Melody Paie** complètement.
3. Ouvrir **Invite de commandes** ou **PowerShell** dans le dossier de l’exe.
4. Lister les comptes :
   ```
   MelodyResetMotDePasse.exe --list
   ```
5. Réinitialiser (exemple compte `admin`) :
   ```
   MelodyResetMotDePasse.exe --login admin --password Melody2026
   ```
6. Dire au client de se connecter avec ce **identifiant** et **mot de passe**, puis de le changer dans l’application.

### B. Base de données (emplacement)

Fichier local :  
`%LocalAppData%\MelodyPaieRDC\Data\PaieRDC.db`  
(ex. `C:\Users\NomClient\AppData\Local\MelodyPaieRDC\Data\PaieRDC.db`)

Ne pas supprimer ce fichier : toute la paie est dedans. Toujours faire une **copie de sauvegarde** avant intervention.

---

## Étape 4 — Après déblocage

- Paramètres → Gestion des utilisateurs → définir un **mot de passe personnel** fort.
- Ne plus utiliser `admin` / `admin` en production.
- Vérifier **Paramètres → Sauvegarde** : une sauvegarde récente existe.

---

## Contacter Impact

Préparer pour le support :

- Capture d’écran de l’écran de connexion.
- Identifiant saisi (sans le mot de passe).
- Version Melody : **Aide** / **Paramètres** → mise à jour, ou nom du Setup installé (ex. 1.0.5).
- Accès distant possible oui/non.
