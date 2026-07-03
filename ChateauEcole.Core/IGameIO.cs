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
}
