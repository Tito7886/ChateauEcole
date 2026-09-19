namespace ChateauEcole.Core;

/// <summary>
/// Traduction des chaînes d'INTERFACE codées en C# (menus, prompts, mini-jeux...). Modèle
/// « gettext » : le texte FRANÇAIS reste dans le code et sert à la fois de clé et de repli.
/// Une implémentation (côté application hôte) mappe ce français vers la langue courante ;
/// si aucune traduction n'existe (ou langue = fr), on renvoie le français tel quel.
/// Le contenu du jeu (world.json) n'est PAS concerné : il est déjà traduit via world_&lt;langue&gt;.json.
/// </summary>
public interface ILocalizer
{
    /// <summary>Renvoie la traduction du texte français donné, ou le français lui-même à défaut.</summary>
    string Tr(string francais);
}
