namespace ChateauEcole.Core.Models;

/// <summary>
/// Une sortie d'une salle vers une autre, avec conditions optionnelles.
/// Target peut valoir "VICTOIRE" pour terminer le jeu.
/// </summary>
public class Exit
{
    public string Target { get; set; } = "";
    public string Label { get; set; } = "";

    /// <summary>Objets requis dans l'inventaire pour emprunter la sortie.</summary>
    public List<string> RequiredItems { get; set; } = new();

    /// <summary>Flags requis (ex. un indice déjà découvert).</summary>
    public List<string> RequiredFlags { get; set; } = new();

    /// <summary>Si vrai, les objets requis sont retirés de l'inventaire au passage (ex. sandwich donné au surveillant).</summary>
    public bool ConsumeRequiredItems { get; set; }

    /// <summary>Message affiché si les conditions ne sont pas remplies.</summary>
    public string? LockedMessage { get; set; }

    /// <summary>La sortie n'apparaît pas dans la liste tant que ce flag n'est pas posé (passage secret).</summary>
    public string? HiddenUntilFlag { get; set; }

    /// <summary>Texte narratif affiché quand la sortie est empruntée avec succès.</summary>
    public string? TransitionText { get; set; }
}
