namespace ChateauEcole.Core.Models;

/// <summary>
/// Résultat de l'action "Examiner" dans une salle.
/// Les objets et flags ne sont accordés qu'à la première fouille.
/// </summary>
public class Interaction
{
    /// <summary>Texte de la première fouille.</summary>
    public string Text { get; set; } = "";

    /// <summary>Texte des fouilles suivantes (sinon Text est réutilisé).</summary>
    public string? RepeatText { get; set; }

    /// <summary>Objets obtenus à la première fouille.</summary>
    public List<string> GrantsItems { get; set; } = new();

    /// <summary>Flag posé à la première fouille (ex. passage secret révélé).</summary>
    public string? SetsFlag { get; set; }

    /// <summary>Objet requis pour fouiller sans danger (ex. masque à gaz).</summary>
    public string? RequiredItem { get; set; }

    /// <summary>Si RequiredItem manque et que ce message est défini : mort du joueur.</summary>
    public string? DeathWithoutItemMessage { get; set; }

    /// <summary>Si RequiredItem manque et que ce message est défini : fouille refusée (non mortel).</summary>
    public string? BlockedWithoutItemMessage { get; set; }

    /// <summary>Si vrai, RequiredItem est consommé lors de la première fouille réussie (ex. balles de tennis).</summary>
    public bool ConsumeRequiredItem { get; set; }

    /// <summary>Si défini : fouiller une seconde fois tue le joueur (mort d'ennui en Latin...).</summary>
    public string? DeadlyOnRepeatMessage { get; set; }
}
