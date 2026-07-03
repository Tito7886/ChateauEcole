namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// L'orgue de l'église Saint-Léger : le rituel de sortie et le choix moral.
/// Trois éléments : médaillon (Lapinou), partition (Musique/code), essence du savoir
/// animalier (= la grenouille morte, voie honnête OU sacrifice de Lapinou, voie immorale).
///
/// Ordre des tests IMPORTANT :
///  - manque médaillon OU partition : on n'ouvre NI le choix NI la voie honnête (sortie non tentée).
///  - médaillon + partition + grenouille : FIN JUSTE.
///  - médaillon + partition sans grenouille, Lapinou présent : pose « sortie_tentee »
///    (débloque la grenouille en Sciences) puis propose le CHOIX demi-tour / sacrifice.
/// </summary>
public static class RituelOrgue
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        var s = engine.State;
        bool medaillon = s.Inventory.Contains("medaillon");
        bool partition = s.Inventory.Contains("partition");
        bool essence = s.Inventory.Contains("grenouille_morte");

        // Il manque un composant « obligatoire » : on continue à chercher, sans rien débloquer.
        if (!medaillon || !partition)
        {
            io.WriteLine("L'orgue attend, immense et patient. Une encoche ronde est creusée dans le pupitre.");
            if (!partition)
                io.WriteLine("* Il te manque la MÉLODIE : la partition « Hymne de Saint-Léger ». On dit qu'elle dort au sous-sol.");
            if (!medaillon)
                io.WriteLine("* Il te manque la MÉDAILLE : un médaillon ancien. Peut-être qu'à l'aumônerie, quelqu'un en porte un...");
            io.WriteLine("L'orgue n'est plus à quelques heures près. Reviens quand tu auras tout.");
            return;
        }

        // Médaille + Mélodie réunies. Reste l'essence.
        if (essence)
        {
            io.WriteLine(engine.T("Tu déposes le médaillon dans l'encoche et tu poses la grenouille — « l'essence du savoir animalier » — sur le pupitre. Puis tu joues."));
            io.WriteLine("Les premières notes de l'Hymne de Saint-Léger s'élèvent sous la voûte. Le médaillon s'illumine.");
            io.WriteLine(engine.T("La grande porte s'ouvre dans un grondement. Tu sors dans la nuit fraîche, {NOM}. Lapinou bondit devant toi, libre, vivant, et file vers les lampadaires comme s'il connaissait déjà le chemin."));
            io.WriteLine("Derrière toi, les fenêtres du lycée s'éteignent une à une. Quelque part, très loin, quelque chose murmure ton prénom une dernière fois — sans colère. Presque avec fierté.");
            engine.EndJuste();
            return;
        }

        // Médaille + Mélodie mais PAS l'essence : on ouvre la voie honnête et on tend la tentation.
        if (!s.Flags.Contains("sortie_tentee"))
            s.Flags.Add("sortie_tentee");

        io.WriteLine("Tu déposes le médaillon dans l'encoche, tu poses la partition, tu joues l'Hymne...");
        io.WriteLine("Les notes résonnent — mais la porte ne bouge pas. Il manque quelque chose sur le pupitre : une encoche vide, humide, en forme de petit corps.");
        io.WriteLine(engine.T("Une inscription s'éclaire : « il faut une essence du savoir animalier. » Tu comprends, {NOM}. Il faut une créature."));
        io.WriteLine();

        if (!s.LapinouSuit)
        {
            // Filet de sécurité : normalement médaillon => Lapinou présent.
            io.WriteLine("Il te faudra retourner à la salle de Sciences, tout là-haut, et y trouver de quoi faire « l'essence ». C'est loin. Mais c'est la seule voie qui te reste.");
            return;
        }

        io.WriteLine("Et là, tu le sens : Lapinou est contre ta jambe. Chaud. Confiant. Une créature. Le pupitre semble... exactement à sa taille.");
        int choix = io.AskChoice("La salle de Sciences est à l'autre bout du lycée. Six salles. Aller-retour. Ou bien...", new List<string>
        {
            "Faire demi-tour vers la salle de Sciences (la voie honnête)",
            "Regarder Lapinou un peu trop longtemps"
        });

        if (choix == 0)
        {
            io.WriteLine();
            io.WriteLine("Tu prends Lapinou dans tes bras et tu tournes les talons. « Viens. On va faire ça bien. »");
            io.WriteLine("La salle de Sciences t'attend, tout en haut. C'est long. C'est juste.");
            return;
        }

        io.WriteLine();
        io.WriteLine(engine.T("L'idée est déjà là, immonde, évidente. Il t'a donné le médaillon de son cou. Il t'a suivie partout. Il te fait confiance. Et l'encoche est juste à sa taille."));
        int confirme = io.AskChoice("Personne ne le saura jamais. Personne, sauf toi.", new List<string>
        {
            "Renoncer. Le reprendre dans tes bras et faire le trajet honnête.",
            "Sacrifier Lapinou."
        });

        if (confirme == 0)
        {
            io.WriteLine();
            io.WriteLine("Tu recules d'un pas. Lapinou lève les yeux vers toi. Tu as un peu honte d'y avoir pensé si longtemps.");
            io.WriteLine("Tu le prends contre toi et tu pars vers les Sciences. La voie juste. Ça, au moins, tu pourras le regarder en face.");
            return;
        }

        // FIN IMMORALE — l'épilogue frontal exigé par l'auteur.
        s.LapinouSuit = false;
        io.WriteLine();
        io.WriteLine(engine.T("Sacrifier un si gentil lapin. Si doux. Si jeune. Déjà si proche de toi — quelqu'un qui t'avait fait confiance, qui t'avait donné jusqu'au médaillon de son cou, qui te suivait partout sans se douter de rien."));
        io.WriteLine(engine.T("Tu es une personne immonde, {NOM}. Vraiment."));
        io.WriteLine("...");
        io.WriteLine(engine.T("Mais bon. La fin justifie les moyens. La porte s'ouvre en grinçant sur la nuit. Tu es sauvé(e). Bravo, {NOM}. Bravo."));
        engine.EndImmoral();
    }
}
