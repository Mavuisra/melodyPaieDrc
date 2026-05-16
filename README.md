# Melody Paie RDC

Application WPF de gestion de la paie pour la RDC (SQLite + Entity Framework Core).

## Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Compilation et exécution

```bash
cd MelodyPaieRDC
dotnet restore
dotnet build
dotnet run
```

Ou ouvrir `MelodyPaieRDC.sln` dans Visual Studio 2022 et lancer (F5).

Les éléments retirés du code actif (SDK ZKFinger, assets inutilisés, etc.) sont conservés dans `_archive/`.

## Structure du projet

- **Data** : `PaieDbContext` (connexion `Data/PaieRDC.db`), seed initial si base vide
- **Models** : entités EF (Employe, BulletinPaie, Contrat, Entreprise, etc.)
- **Views** : fenêtres WPF (MainWindow, EmployeWindow, BulletinView)
- **ViewModels** : MainViewModel, EmployeViewModel, SaisiePaieMoisViewModel, etc.
- **Services** : CalculPaieService, ExportPdfService, ZktecoPointageReader
- **Helpers** : TauxChangeHelper

## Fonctionnalités

- **Fenêtre principale** : liste des employés, bouton « Nouvel employé », « Rafraîchir »
- **Nouvel employé** : formulaire complet (matricule, nom, postnom, prénom, sexe, état civil, date de naissance, téléphone, adresse, département) avec validation et enregistrement en base
- Au premier lancement, si la base n’existe pas, elle est créée avec une structure minimale (une entreprise, un établissement, un département « Général ») pour pouvoir ajouter des employés immédiatement.
