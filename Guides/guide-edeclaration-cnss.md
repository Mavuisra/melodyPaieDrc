# Guide — Export CNSS pour edeclaration.cnss.cd

## Modèle officiel (12 colonnes)

Melody Paie RDC génère un fichier **Excel** aligné sur le modèle du portail CNSS :

| Colonne | Libellé portail | Source Melody |
|---------|-----------------|---------------|
| A | Matricule travailleur | Matricule interne |
| B | Num Immatriculation CNSS | N° CNSS sur la fiche employé |
| C | Noms | Nom |
| D | Post noms | Postnom |
| E | Prenoms | Prénom |
| F | Type travailleur (1 / 2) | Fiche employé (1 = Travailleur, 2 = Assimilé) |
| G | Commune ou Territoire affectation | Commune saisie ou site / département |
| H | Periode Cotisee (jj/mm/aaaa) | Dernier jour du mois de paie |
| I | Montant Cotise | Cotisation CNSS part salarié (bulletin) |
| J | Nbre De Jours de travail | Jours prestés (saisie paie) |
| K | Nbre De heure de travail | Total heures pointage du mois |
| L | Montant Brut Imposable | Base CNSS du bulletin |

## Prérequis

1. **Informations entreprise** : N° employeur CNSS, N° d’affiliation.
2. **Employés** : N° CNSS, commune d’affectation et type travailleur renseignés.
3. **Bulletins** générés pour la période.
4. **Pointage** (recommandé) pour les heures du mois.

## Deux exports CNSS distincts

| Bouton | Usage |
|--------|--------|
| **CNSS e-déclaration** | Liste des travailleurs (12 colonnes A–L : matricule, CNSS, cotisations, jours, heures, brut imposable). |
| **Feuille de paie CNSS** | Annexe « Détails de la feuille de paie » au format **Word (.docx)** : salaire de base, primes, gratifications, indemnités, etc. |

### Feuille de paie (annexe)

Colonnes : Matricule, Prénom, Post Nom, Nom, Salaire de Base, Indemnités de vie chère, Primes, Gratifications, Allocations de congés, Avantages en nature, Commissions, Autres indemnités.

Les montants sont répartis à partir des **lignes de gain** des bulletins (libellés contenant *prime*, *gratification*, *congé*, etc.).

Seuls les employés ayant **réellement presté ou reçu une rémunération** sur la période (heures, jours saisis, gains ou cotisation CNSS) figurent dans le document — pas le salaire contractuel par défaut.

**Important :** enregistrez le fichier Word en **PDF** avant de le charger sur le portail (comme indiqué sur le modèle CNSS).

## Export e-déclaration

1. Menu **Déclarations CNSS / IPR** → sélectionner la période.
2. Cliquer **CNSS e-déclaration** → fichier `.xlsx`.
3. Importer ou recopier sur [edeclaration.cnss.cd](https://edeclaration.cnss.cd).

## Personnalisation

**Centre de configuration → Exports et clôture paie** : colonnes, séparateur CSV, format Excel/CSV.

Si vous aviez un ancien profil (colonnes « N° ordre », « CNSS patronal », etc.), rouvrez la configuration des exports : le modèle officiel est réappliqué automatiquement.

## Support

Comparez votre export avec le modèle CNSS. En cas d’écart, vérifiez les fiches employés et les bulletins de la période.
