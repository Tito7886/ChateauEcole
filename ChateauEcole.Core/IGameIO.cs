namespace ChateauEcole.Core;

/// <summary>
/// Abstraction des entrées/sorties. C'est CETTE interface qui rend
/// le portage 2D possible : le moteur ne touche jamais System.Console.
/// </summary>
public interface IGameIO
{
    void WriteLine(string text = "");

    /// <summary>
    /// Affiche les options et retourne l'index (0-based) du choix valide.
    /// onInvalid est appelé à chaque saisie invalide (pénalité de score).
    /// </summary>
    int AskChoice(string prompt, IReadOnlyList<string> options, Action? onInvalid = null);

    /// <summary>
    /// Choix sous pression : si le joueur ne répond pas dans le délai,
    /// l'option defaultIndex est retenue. Utilisé par les mini-jeux d'action.
    /// </summary>
    int AskChoiceTimed(string prompt, IReadOnlyList<string> options, int timeoutSeconds, int defaultIndex);

    /// <summary>Demande une saisie de texte libre (ex. le prénom du joueur).</summary>
    string AskText(string prompt);

    void Clear();

    // --- Effets d'affichage (implémentés côté ConsoleIO ; en 2D : animation de panneau,
    //     bouton « continuer », couleurs de police — sans changer une ligne du Core). ---

    /// <summary>
    /// Écran-titre affiché à l'ENTRÉE d'une salle : efface l'écran (Clear) puis met en valeur
    /// le nom de la salle. Sans <paramref name="titleArt"/> : un encadré automatique coloré
    /// dont la bordure s'adapte à la longueur du nom. Avec <paramref name="titleArt"/> (ASCII
    /// art multi-lignes, balises couleur autorisées) : cet art est affiché à la place.
    /// Dégradation : en sortie redirigée, un simple affichage texte du nom suffit.
    /// En 2D : un bandeau / une scène d'entrée, l'art devenant une image par salle.
    /// </summary>
    void ShowRoomTitle(string roomName, string? titleArt = null);

    /// <summary>
    /// Effet machine à écrire : affiche le texte caractère par caractère.
    /// <paramref name="msParCaractere"/> règle la vitesse ; 0 (ou &lt; 0) = vitesse par défaut
    /// configurable (voir ConsoleIO.VitesseParDefautMs). Respecte les balises couleur
    /// ([rouge]...[/rouge]). Instantané si la sortie est redirigée. L'animation va toujours à
    /// son terme (non sautable). Ajoute un saut de ligne final.
    /// En 2D : affichage progressif dans le panneau de texte (même signature).
    /// </summary>
    void WriteSlow(string text, int msParCaractere = 0);

    /// <summary>Affiche un message et attend une frappe. Retour immédiat si entrée redirigée.</summary>
    void Pause(string? message = null);

    /// <summary>Comme Pause, puis efface l'écran (Clear).</summary>
    void PauseThenClear(string? message = null);

    /// <summary>Attend N secondes (petit décompte discret) puis efface l'écran. Pas d'attente si redirigé.</summary>
    void WaitThenClear(int secondes);
}
