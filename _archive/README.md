# Archive Melody Paie RDC

Éléments retirés du projet actif le **2026-05-16** (non utilisés au build ni au runtime).
Conservés ici pour référence ou réintégration future.

| Dossier / fichier | Raison de l'archivage |
|-------------------|------------------------|
| `ThirdParty/` | SDK ZKFinger (empreintes) — documentaire uniquement ; pointeuses via `ZktecoPullWorker` + NuGet |
| `ViewModels/CalculPaieViewModel.cs` | Stub jamais instancié ; logique dans `CalculPaieService` |
| `Assets/*.png`, `Icon_MelodyPaie.ico` | Ressources embarquées sans référence UI |
| `Data/ScriptSQL_Complet.sql` | Schéma de référence ; runtime via `SchemaSqliteApplicator` |
| `Helpers/InverseEqualityToVisibilityConverter.cs` | Converter XAML déclaré mais non lié |

Les artefacts de build (`bin/`, `obj/`, `publish/`, `temp_build_*`) ne sont pas archivés ici : ils sont ignorés par Git (voir `.gitignore` à la racine).
