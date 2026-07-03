namespace ChateauEcole.Core.Models;

/// <summary>
/// Une salle du lycée. Tout le contenu vient de world.json :
/// le moteur ne connaît aucune salle en dur.
/// </summary>
public class Room
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Affichée uniquement à la première visite.</summary>
    public string Description { get; set; } = "";

    public List<Exit> Exits { get; set; } = new();

    /// <summary>
    /// Textes affichés lors des visites SUIVANTES, selon l'état de la partie :
    /// le premier dont le flag est posé est affiché. Permet aux salles de
    /// changer après une réussite ou un échec (prof de sport vaincu, etc.).
    /// </summary>
    public List<StateText> StateTexts { get; set; } = new();

    /// <summary>Ce qui se passe quand le joueur choisit "Examiner". Null = pas d'option Examiner.</summary>
    public Interaction? Examine { get; set; }

    /// <summary>Id d'une action codée en C# (ex. mini-jeu), enregistrée dans GameEngine.</summary>
    public string? SpecialActionId { get; set; }

    /// <summary>Libellé du choix affiché pour l'action spéciale.</summary>
    public string? SpecialActionLabel { get; set; }
}

/// <summary>Un texte conditionné par un flag, pour faire évoluer une salle.</summary>
public class StateText
{
    public string Flag { get; set; } = "";
    public string Text { get; set; } = "";
}
