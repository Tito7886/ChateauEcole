namespace ChateauEcole.Core.Models;

/// <summary>Racine de world.json : la totalité du contenu du jeu.</summary>
public class WorldData
{
    public string StartRoom { get; set; } = "";

    /// <summary>Id d'objet -> nom affiché.</summary>
    public Dictionary<string, string> Items { get; set; } = new();

    public List<Room> Rooms { get; set; } = new();

    /// <summary>SMS du mystérieux « R. », envoyés quand le téléphone est chargé et les conditions remplies.</summary>
    public List<SmsRule> Sms { get; set; } = new();
}

/// <summary>Un SMS de progression : envoyé une seule fois, dès que les conditions sont réunies.</summary>
public class SmsRule
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<string> RequiredItems { get; set; } = new();
    public List<string> RequiredFlags { get; set; } = new();
}
