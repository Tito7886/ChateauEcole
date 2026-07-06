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

    /// <summary>
    /// Objets mortels au ramassage : id d'objet -> message de mort. Ramasser un tel objet
    /// au sol tue le joueur (ex. une digitale déguisée en jolie fleur). Ils ne rejoignent
    /// jamais l'inventaire.
    /// </summary>
    public Dictionary<string, string> DeadlyItems { get; set; } = new();

    /// <summary>
    /// Table de recettes de combinaison (objet+objet ou objet+décor). Le moteur cherche une
    /// recette correspondant à la paire choisie par le joueur, sans jamais dire quoi combiner.
    /// </summary>
    public List<Combination> Combinations { get; set; } = new();
}

/// <summary>
/// Une recette : deux ingrédients (ordre indifférent). ItemB peut être un id d'objet
/// d'inventaire OU un id de cible de décor (Room.DecorTargets). Effets tous optionnels.
/// </summary>
public class Combination
{
    public string ItemA { get; set; } = "";
    public string ItemB { get; set; } = "";
    public string ResultText { get; set; } = "";

    /// <summary>Objets gagnés / consommés (optionnels).</summary>
    public List<string> GrantsItems { get; set; } = new();
    public List<string> RemovesItems { get; set; } = new();

    /// <summary>Flag posé (optionnel).</summary>
    public string? SetsFlag { get; set; }

    /// <summary>Points gagnés (positif) ou perdus (négatif) — appliqués la 1re fois seulement.</summary>
    public int ScoreDelta { get; set; }

    /// <summary>La combinaison tue (avec DeathMessage). Optionnel.</summary>
    public bool Deadly { get; set; }
    public string? DeathMessage { get; set; }

    /// <summary>Rejouable (texte réaffiché, sans re-récompense). Par défaut : une seule fois.</summary>
    public bool Repeatable { get; set; }
}

/// <summary>Un SMS de progression : envoyé une seule fois, dès que les conditions sont réunies.</summary>
public class SmsRule
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<string> RequiredItems { get; set; } = new();
    public List<string> RequiredFlags { get; set; } = new();
}
