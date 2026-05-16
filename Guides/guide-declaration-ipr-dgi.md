# Guide — Export déclaration IPR (DGI)

## Objectif

Produire un fichier **CSV / Excel configurable** avec les bases imposables et IPR nets retenus, pour la déclaration mensuelle de **retenue à la source (IPR)** auprès de la **DGI**.

> Adaptez les colonnes au formulaire ou fichier attendu par votre centre des impôts / portail DGI lorsque celui-ci est disponible.

## Prérequis

1. **NIF** de l’entreprise renseigné.
2. Barème **IPR** configuré (Paramètres → Paramètres IPR).
3. Bulletins de paie validés pour la période.

## Étapes

1. Menu **Déclarations CNSS / IPR** → période.
2. **DGI IPR (CSV)** ou **Excel**.
3. Contrôler les totaux : base imposable, IPR net, effectif.
4. Transmettre selon la procédure DGI (guichet, portail, expert-comptable).

## Colonnes par défaut (modifiables)

| Colonne | Source Melody |
|---------|----------------|
| Matricule | Employé |
| Nom complet | Employé |
| Base imposable | Bulletin |
| IPR net | Bulletin |
| Rémunération brute | Bulletin |

## Personnalisation

Profil **DGI_IPR** dans **Exports et clôture paie** : colonnes, séparateur, lignes d’en-tête (NIF, période AAAAMM).

## Conservation

Conservez l’export et les bulletins PDF **au moins 10 ans** (obligations comptables et fiscales courantes en RDC — vérifiez avec votre conseil).
