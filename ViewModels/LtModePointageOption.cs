namespace MelodyPaieRDC.ViewModels;

public sealed class LtModePointageOption
{
    public LtModePointageOption(string code, string libelle)
    {
        Code = code;
        Libelle = libelle;
    }

    public string Code { get; }
    public string Libelle { get; }
}
