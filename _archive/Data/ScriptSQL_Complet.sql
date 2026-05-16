-- =============================================================================
-- Melody Paie RDC - Script SQL complet (SQLite)
-- Schéma équivalent à Entity Framework Core (EnsureCreated)
-- =============================================================================

-- Suppression des tables (ordre inverse des dépendances) - optionnel
-- DROP TABLE IF EXISTS BulletinsDetails;
-- DROP TABLE IF EXISTS BulletinsPaie;
-- DROP TABLE IF EXISTS ParametresIpr;
-- DROP TABLE IF EXISTS TauxSociaux;
-- DROP TABLE IF EXISTS GrillesIpr;
-- DROP TABLE IF EXISTS PeriodesPaie;
-- DROP TABLE IF EXISTS AbsencesConges;
-- DROP TABLE IF EXISTS AffectationsPrimesIndemnites;
-- DROP TABLE IF EXISTS PretsAvances;
-- DROP TABLE IF EXISTS AyantsDroit;
-- DROP TABLE IF EXISTS Contrats;
-- DROP TABLE IF EXISTS Employes;
-- DROP TABLE IF EXISTS PrimesIndemnites;
-- DROP TABLE IF EXISTS CategoriesProfessionnelles;
-- DROP TABLE IF EXISTS Departements;
-- DROP TABLE IF EXISTS Etablissements;
-- DROP TABLE IF EXISTS Entreprises;

-- -----------------------------------------------------------------------------
-- 1. Entreprises
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Entreprises" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "RaisonSociale" TEXT NOT NULL,
    "Nrc" TEXT,
    "IdNat" TEXT,
    "NumCnss" TEXT,
    "NumInpp" TEXT,
    "Adresse" TEXT,
    "Logo" TEXT,
    "CouleurPrincipale" TEXT,
    "CouleurSecondaire" TEXT
);

-- -----------------------------------------------------------------------------
-- 2. Etablissements
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Etablissements" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EntrepriseId" INTEGER NOT NULL,
    "NomSite" TEXT NOT NULL,
    CONSTRAINT "FK_Etablissements_Entreprises_EntrepriseId" 
        FOREIGN KEY ("EntrepriseId") REFERENCES "Entreprises" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_Etablissements_EntrepriseId" ON "Etablissements" ("EntrepriseId");

-- -----------------------------------------------------------------------------
-- 3. Departements
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Departements" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EtablissementId" INTEGER NOT NULL,
    "NomDepartement" TEXT NOT NULL,
    CONSTRAINT "FK_Departements_Etablissements_EtablissementId" 
        FOREIGN KEY ("EtablissementId") REFERENCES "Etablissements" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_Departements_EtablissementId" ON "Departements" ("EtablissementId");

-- -----------------------------------------------------------------------------
-- 4. CategoriesProfessionnelles
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "CategoriesProfessionnelles" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Libelle" TEXT NOT NULL,
    "SmigApplique" REAL NOT NULL
);

-- -----------------------------------------------------------------------------
-- 5. PrimesIndemnites (référentiel primes / indemnités)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "PrimesIndemnites" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Libelle" TEXT NOT NULL,
    "EstImposable" INTEGER NOT NULL,
    "EstCotisable" INTEGER NOT NULL
);

-- -----------------------------------------------------------------------------
-- 6. Employes
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Employes" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Matricule" TEXT NOT NULL,
    "Nom" TEXT NOT NULL,
    "Postnom" TEXT,
    "Prenom" TEXT,
    "Sexe" TEXT,
    "EtatCivil" TEXT,
    "DateNaissance" TEXT,
    "Telephone" TEXT,
    "Adresse" TEXT,
    "DepartementId" INTEGER NOT NULL,
    CONSTRAINT "FK_Employes_Departements_DepartementId" 
        FOREIGN KEY ("DepartementId") REFERENCES "Departements" ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Employes_Matricule" ON "Employes" ("Matricule");
CREATE INDEX IF NOT EXISTS "IX_Employes_DepartementId" ON "Employes" ("DepartementId");

-- -----------------------------------------------------------------------------
-- 7. Contrats
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Contrats" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EmployeId" INTEGER NOT NULL,
    "TypeContrat" TEXT NOT NULL,
    "DateDebut" TEXT NOT NULL,
    "DateFin" TEXT,
    "SalaireBase" REAL NOT NULL,
    "DeviseBase" TEXT NOT NULL,
    "CategorieProfessionnelleId" INTEGER NOT NULL,
    CONSTRAINT "FK_Contrats_Employes_EmployeId" 
        FOREIGN KEY ("EmployeId") REFERENCES "Employes" ("Id"),
    CONSTRAINT "FK_Contrats_CategoriesProfessionnelles_CategorieProfessionnelleId" 
        FOREIGN KEY ("CategorieProfessionnelleId") REFERENCES "CategoriesProfessionnelles" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_Contrats_EmployeId" ON "Contrats" ("EmployeId");
CREATE INDEX IF NOT EXISTS "IX_Contrats_CategorieProfessionnelleId" ON "Contrats" ("CategorieProfessionnelleId");

-- -----------------------------------------------------------------------------
-- 7b. AffectationsPrimesIndemnites (affectation prime → employé, montant mensuel)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "AffectationsPrimesIndemnites" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EmployeId" INTEGER NOT NULL,
    "PrimeIndemniteId" INTEGER NOT NULL,
    "Montant" REAL NOT NULL,
    CONSTRAINT "FK_AffectationsPrimesIndemnites_Employes_EmployeId"
        FOREIGN KEY ("EmployeId") REFERENCES "Employes" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AffectationsPrimesIndemnites_PrimesIndemnites_PrimeIndemniteId"
        FOREIGN KEY ("PrimeIndemniteId") REFERENCES "PrimesIndemnites" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_AffectationsPrimesIndemnites_EmployeId" ON "AffectationsPrimesIndemnites" ("EmployeId");
CREATE INDEX IF NOT EXISTS "IX_AffectationsPrimesIndemnites_PrimeIndemniteId" ON "AffectationsPrimesIndemnites" ("PrimeIndemniteId");

-- -----------------------------------------------------------------------------
-- 8. AyantsDroit
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "AyantsDroit" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EmployeId" INTEGER NOT NULL,
    "Nom" TEXT NOT NULL,
    "LienParente" TEXT NOT NULL,
    "DateNaissance" TEXT,
    CONSTRAINT "FK_AyantsDroit_Employes_EmployeId" 
        FOREIGN KEY ("EmployeId") REFERENCES "Employes" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_AyantsDroit_EmployeId" ON "AyantsDroit" ("EmployeId");

-- -----------------------------------------------------------------------------
-- 9. PretsAvances
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "PretsAvances" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EmployeId" INTEGER NOT NULL,
    "MontantTotal" REAL NOT NULL,
    "DateOctroi" TEXT NOT NULL,
    "NbEcheances" INTEGER NOT NULL,
    "MontantMensuel" REAL NOT NULL,
    "SoldeRestant" REAL NOT NULL,
    "Statut" TEXT,
    CONSTRAINT "FK_PretsAvances_Employes_EmployeId" 
        FOREIGN KEY ("EmployeId") REFERENCES "Employes" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_PretsAvances_EmployeId" ON "PretsAvances" ("EmployeId");

-- -----------------------------------------------------------------------------
-- 10. AbsencesConges
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "AbsencesConges" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EmployeId" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    "DateDebut" TEXT NOT NULL,
    "DateFin" TEXT NOT NULL,
    "EstPaye" INTEGER NOT NULL,
    CONSTRAINT "FK_AbsencesConges_Employes_EmployeId" 
        FOREIGN KEY ("EmployeId") REFERENCES "Employes" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_AbsencesConges_EmployeId" ON "AbsencesConges" ("EmployeId");

-- -----------------------------------------------------------------------------
-- 11. PeriodesPaie
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "PeriodesPaie" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Mois" INTEGER NOT NULL,
    "Annee" INTEGER NOT NULL,
    "TauxChangeBudget" REAL NOT NULL,
    "Cloturee" INTEGER NOT NULL
);

-- -----------------------------------------------------------------------------
-- 12. GrillesIpr (barème IPR)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "GrillesIpr" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "BorneInf" REAL NOT NULL,
    "BorneSup" REAL NOT NULL,
    "Taux" REAL NOT NULL
);

-- -----------------------------------------------------------------------------
-- 13. TauxSociaux (CNSS, INPP, ONEM...)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "TauxSociaux" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Code" TEXT NOT NULL,
    "Pourcentage" REAL NOT NULL
);

-- -----------------------------------------------------------------------------
-- 14. ParametresIpr
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "ParametresIpr" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "TauxEffectifMaximum" REAL NOT NULL,
    "ReductionParEnfant" REAL NOT NULL
);

-- -----------------------------------------------------------------------------
-- 15. BulletinsPaie
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "BulletinsPaie" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "EmployeId" INTEGER NOT NULL,
    "PeriodePaieId" INTEGER NOT NULL,
    "NumeroBulletin" TEXT,
    "DateGeneration" TEXT NOT NULL,
    "TotalGainImposable" REAL NOT NULL,
    "TotalGainNonImposable" REAL NOT NULL,
    "BaseIpr" REAL NOT NULL,
    "MontantIprBrut" REAL NOT NULL,
    "ReductionFamille" REAL NOT NULL,
    "MontantIprNet" REAL NOT NULL,
    "CotisationCnssOuvrier" REAL NOT NULL,
    "NetAPayer" REAL NOT NULL,
    "NetAPayerDeviseLocale" REAL NOT NULL,
    CONSTRAINT "FK_BulletinsPaie_Employes_EmployeId" 
        FOREIGN KEY ("EmployeId") REFERENCES "Employes" ("Id"),
    CONSTRAINT "FK_BulletinsPaie_PeriodesPaie_PeriodePaieId" 
        FOREIGN KEY ("PeriodePaieId") REFERENCES "PeriodesPaie" ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_BulletinsPaie_EmployeId" ON "BulletinsPaie" ("EmployeId");
CREATE INDEX IF NOT EXISTS "IX_BulletinsPaie_PeriodePaieId" ON "BulletinsPaie" ("PeriodePaieId");

-- -----------------------------------------------------------------------------
-- 16. BulletinsDetails (lignes de bulletin)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "BulletinsDetails" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "BulletinPaieId" INTEGER NOT NULL,
    "Libelle" TEXT NOT NULL,
    "BaseCalcul" REAL NOT NULL,
    "Taux" REAL NOT NULL,
    "Gain" REAL NOT NULL,
    "Retenue" REAL NOT NULL,
    CONSTRAINT "FK_BulletinsDetails_BulletinsPaie_BulletinPaieId" 
        FOREIGN KEY ("BulletinPaieId") REFERENCES "BulletinsPaie" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_BulletinsDetails_BulletinPaieId" ON "BulletinsDetails" ("BulletinPaieId");

-- =============================================================================
-- Données initiales (équivalent SeedSiVide)
-- =============================================================================
INSERT INTO "Entreprises" ("RaisonSociale", "Adresse") 
SELECT 'Mon Entreprise', 'Kinshasa, RDC' 
WHERE NOT EXISTS (SELECT 1 FROM "Entreprises" LIMIT 1);

INSERT INTO "Etablissements" ("EntrepriseId", "NomSite") 
SELECT (SELECT "Id" FROM "Entreprises" LIMIT 1), 'Siège' 
WHERE NOT EXISTS (SELECT 1 FROM "Etablissements" LIMIT 1);

INSERT INTO "Departements" ("EtablissementId", "NomDepartement") 
SELECT (SELECT "Id" FROM "Etablissements" LIMIT 1), 'Général' 
WHERE NOT EXISTS (SELECT 1 FROM "Departements" LIMIT 1);

-- Exemple : paramètre IPR par défaut (une seule ligne)
INSERT INTO "ParametresIpr" ("TauxEffectifMaximum", "ReductionParEnfant") 
SELECT 0.30, 0 
WHERE NOT EXISTS (SELECT 1 FROM "ParametresIpr" LIMIT 1);

-- =============================================================================
-- Fin du script
-- =============================================================================
