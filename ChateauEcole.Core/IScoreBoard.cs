namespace ChateauEcole.Core;

/// <summary>
/// Classement en ligne — abstraction pour que le moteur (Core) ne connaisse PAS le réseau
/// (comme IGameIO pour la console). L'implémentation HTTP vit côté application hôte
/// (ConsoleApp aujourd'hui, 2D demain). Contrat « best-effort » : les deux méthodes ne
/// DOIVENT jamais lever ni bloquer durablement — hors-ligne / serveur muet = liste vide /
/// publication silencieuse. Le jeu retombe alors sur les scores locaux.
/// </summary>
public interface IScoreBoard
{
    /// <summary>Renvoie le haut du classement en ligne, ou une liste vide si indisponible.</summary>
    List<HighScore> GetTop(int count);

    /// <summary>Publie un score. Silencieux si indisponible.</summary>
    void Submit(HighScore entry);
}
