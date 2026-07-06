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
    /// Effet machine à écrire : affiche le texte caractère par caractère (délai msParCaractere).
    /// Respecte les balises couleur ([rouge]...[/rouge]). Instantané si la sortie est redirigée.
    /// L'animation va toujours à son terme (non sautable). Ajoute un saut de ligne final.
    /// En 2D : affichage progressif dans le panneau de texte (même signature).
    /// </summary>
    void WriteSlow(string text, int msParCaractere = 30);

    /// <summary>Affiche un message et attend une frappe. Retour immédiat si entrée redirigée.</summary>
    void Pause(string? message = null);

    /// <summary>Comme Pause, puis efface l'écran (Clear).</summary>
    void PauseThenClear(string? message = null);

    /// <summary>Attend N secondes (petit décompte discret) puis efface l'écran. Pas d'attente si redirigé.</summary>
    void WaitThenClear(int secondes);
}
