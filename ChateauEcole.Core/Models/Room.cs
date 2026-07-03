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

    /// <summary>
    /// Actions génériques déclarées en JSON (fouiller un meuble, pousser une bibliothèque,
    /// proposer un objet à prendre ou à laisser...). Elles apparaissent comme des choix
    /// dédiés, distincts d'« Examiner », et se conditionnent entre elles via les flags.
    /// </summary>
    public List<RoomAction> Actions { get; set; } = new();

    /// <summary>Id d'une action codée en C# (ex. mini-jeu), enregistrée dans GameEngine.</summary>
    public string? SpecialActionId { get; set; }

    /// <summary>Libellé du choix affiché pour l'action spéciale.</summary>
    public string? SpecialActionLabel { get; set; }
}

/// <summary>
/// Une action de salle générique, pilotée par les données. Permet de séquencer
/// une découverte (« Fouiller le bureau » puis « Inspecter la bibliothèque » puis
/// « Pousser la bibliothèque ») sans écrire de C#. Une action non répétable
/// disparaît une fois jouée (flag interne « did_&lt;roomId&gt;_&lt;id&gt; »).
/// </summary>
public class RoomAction
{
    /// <summary>Identifiant unique dans la salle (sert au flag « déjà joué »).</summary>
    public string Id { get; set; } = "";

    /// <summary>Libellé du choix affiché au joueur.</summary>
    public string Label { get; set; } = "";

    /// <summary>L'action n'apparaît que si TOUS ces flags sont posés (enchaînement).</summary>
    public List<string> RequiredFlags { get; set; } = new();

    /// <summary>L'action n'apparaît que si AUCUN de ces flags n'est posé.</summary>
    public List<string> RequiredFlagsAbsent { get; set; } = new();

    /// <summary>Si vrai, l'action reste disponible après avoir été jouée.</summary>
    public bool Repeatable { get; set; }

    /// <summary>Texte affiché quand l'action est jouée.</summary>
    public string Text { get; set; } = "";

    /// <summary>Texte des fois suivantes pour une action répétable (sinon Text est réutilisé).</summary>
    public string? RepeatText { get; set; }

    /// <summary>Flag posé quand l'action est jouée (révèle un mécanisme, ouvre une sortie...).</summary>
    public string? SetsFlag { get; set; }

    /// <summary>Objets ajoutés directement à l'inventaire (échange proposé si le sac est plein).</summary>
    public List<string> GrantsItems { get; set; } = new();

    /// <summary>Objets posés au sol de la salle courante (récupérables plus tard).</summary>
    public List<string> DropsToFloor { get; set; } = new();

    /// <summary>Objet proposé « à prendre ou à laisser » : la décision revient au joueur.</summary>
    public string? OffersItem { get; set; }
}

/// <summary>Un texte conditionné par un flag, pour faire évoluer une salle.</summary>
public class StateText
{
    public string Flag { get; set; } = "";
    public string Text { get; set; } = "";
}
