namespace MelodyPaieRDC.Services;

/// <summary>Corrections paie Août 2026 validées (liste papier + Excel FichesPaie_Aout2026).</summary>
public static class Aout2026CorrectionsCatalog
{
    public const int MoisCible = 8;
    public const int AnneeCible = 2026;
    public const decimal StagiaireSalaireUsd = 100m;

    /// <summary>Employés stagiaires : KUBUENA, MIFITA Elvis, MAFA.</summary>
    public static readonly HashSet<int> StagiaireEmployeIds = [41, 42, 51];

    public static IReadOnlyList<Aout2026CorrectionLigne> Lignes { get; } = CreerLignes();

    private static IReadOnlyList<Aout2026CorrectionLigne> CreerLignes() =>
    [
        L(55, quinz: 0, net: 900),
        L(3, quinz: 0, net: 700),
        L(2, km: 225.15m, quinz: 0, net: 900),
        L(5, quinz: 0, net: 700),
        L(13, quinz: 100, net: 400),
        L(7, quinz: 100, net: 400),
        L(12, km: 102.15m, retenu: 0, quinz: 0, net: 450),
        L(8, quinz: 100, net: 480),
        L(17, km: 80.85m, retenu: 16, quinz: 150, net: 300),
        L(18, km: 67.75m, retenu: 11.04m, quinz: 100, net: 350),
        L(14, km: 212.97m, quinz: 0, net: 900),
        L(25, quinz: 50, net: 200),
        L(50, quinz: 50, net: 200),
        L(33, quinz: 100, net: 250),
        L(4, quinz: 30, net: 170),
        L(52, quinz: 100, net: 150),
        L(56, quinz: 100, net: 240),
        L(45, quinz: 0, net: 300),
        L(20, retenu: 15.5m, quinz: 50, net: 200),
        L(16, retenu: 13, quinz: 100, net: 255),
        L(19, km: 62.88m, quinz: 150, net: 300),
        L(30, retenu: 10.77m, quinz: 100, net: 350),
        L(29, quinz: 150, net: 300),
        L(15, retenu: 10, quinz: 100, prime: 35, net: 335),
        L(11, km: 48.77m, quinz: 0, net: 400),
        L(10, retenu: 14, quinz: 100, net: 300),
        L(9, retenu: 15.5m, quinz: 100, net: 300),
        L(27, retenu: 23.14m, quinz: 150, net: 300),
        L(34, km: 64.37m, quinz: 100, net: 255),
        L(32, km: 198.17m, quinz: 100, net: 255),
        L(23, km: 0, retenu: 21.42m, log: 51.87m, quinz: 50, net: 200),
        L(22, km: 164.5m, log: 80.34m, quinz: 150, net: 400),
        L(21, km: 107.48m, log: 69.42m, quinz: 100, net: 350),
        L(39, km: 0, retenu: 16.16m, log: 69.42m, quinz: 100, net: 250),
        L(31, km: 43.29m, quinz: 100, net: 350),
        L(35, quinz: 150, prime: 50, net: 300),
        L(24, km: 2, quinz: 100, net: 300),
        L(38, km: 7.14m, quinz: 150, net: 200),
        L(49, km: 37.72m, log: 51.87m, quinz: 100, net: 200),
        L(1, km: 34, quinz: 100, net: 180),
        L(44, km: 29, log: 51.87m, quinz: 50, net: 250),
        L(59, km: 87.38m, log: 60.11m, quinz: 50, net: 330),
        L(37, km: 54.10m, quinz: 200, net: 310),
        L(28, km: 26.38m, quinz: 150, net: 400),
        L(48, km: 41.81m, quinz: 50, net: 155),
        L(51, quinz: 0, net: 100),
        L(40, quinz: 100, net: 200),
        L(47, quinz: 100, net: 250),
        L(26, km: 93.55m, quinz: 100, net: 150),
        L(58, quinz: 0, net: 150),
        L(6, quinz: 0, net: 370),
        L(43, quinz: 0, net: 200),
        L(36, quinz: 50, net: 300),
        L(53, km: 5.85m, quinz: 50, prime: 25, net: 150),
        L(46, quinz: 0, net: 250),
        L(57, quinz: 50, net: 300),
        L(42, quinz: 0, net: 100),
        L(41, quinz: 0, net: 100),
    ];

    private static Aout2026CorrectionLigne L(
        int employeId,
        decimal? km = null,
        decimal? retenu = null,
        decimal? log = null,
        decimal quinz = 0,
        decimal? prime = null,
        decimal net = 0)
        => new(employeId, km, retenu, log, quinz, prime, net);
}

/// <summary>Une ligne de correction — seuls les champs non null (KM, log, retenu, prime) sont écrits.</summary>
public sealed record Aout2026CorrectionLigne(
    int EmployeId,
    decimal? Km,
    decimal? RetenuSalaire,
    decimal? Logement,
    decimal Quinzaine,
    decimal? Prime,
    decimal NetCibleReference);
