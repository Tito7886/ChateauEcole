namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// Les cuisines de la cantine :
///  - le sandwich (surveillant → bureau) ;
///  - un légume pour Lapinou : la carotte (le bon choix) OU la laitue (piège :
///    l'offrir à Lapinou est mortel). On ne peut porter qu'UN seul des deux à la fois,
///    mais on peut reposer l'un pour prendre l'autre (donc jamais de blocage définitif) ;
///  - la « Surprise du chef », piège mortel pur Undertale (menu souligné trois fois).
/// </summary>
public static class CantineChoix
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        var state = engine.State;
        bool aCarotte = state.Inventory.Contains("carotte");
        bool aLaitue = state.Inventory.Contains("laitue");

        var options = new List<(string Label, string Id)>();

        if (!state.Flags.Contains("cantine_sandwich"))
            options.Add(("Le sandwich sous cellophane, posé bien en évidence au milieu du plan de travail", "sandwich"));

        // Carotte OU laitue, jamais les deux en même temps : les prises n'apparaissent que si
        // l'on n'a aucun des deux ; sinon on peut reposer celui que l'on porte pour changer.
        if (!aCarotte && !aLaitue)
        {
            options.Add(("Une carotte oubliée rabougrie dans un cageot, presque plus orange, est-elle encore fraîche...?", "carotte"));
            options.Add(("Une superbe laitue, surement appétissante pour ceux qui aiment", "laitue"));
        }
        else if (aCarotte)
        {
            options.Add(("Reposer la carotte dans le cageot", "repose_carotte"));
        }
        else
        {
            options.Add(("Reposer la laitue dans le cageot", "repose_laitue"));
        }

        // La « Surprise du chef » reste toujours proposée : c'est un choix, et une très mauvaise idée.
        options.Add(("La « Surprise du chef » qui frémit doucement sous sa cloche, en chambre froide", "surprise"));
        options.Add(("Ne rien toucher", "rien"));

        int c = io.AskChoice("Dans les cuisines, plusieurs choses attirent ton attention :",
                             options.Select(o => o.Label).ToList());

        switch (options[c].Id)
        {
            case "sandwich":
                state.Flags.Add("cantine_sandwich");
                engine.AddItem("sandwich");
                io.WriteLine("Il a été laissé là bien en évidence. Comme pour toi. Ou comme pour quelqu'un qui a très faim.");
                break;

            case "carotte":
                engine.AddItem("carotte");
                io.WriteLine("Une carotte, dans un lycée où tout pourrit ou se fige. Trop fraîche pour être honnête.");
                io.WriteLine("Tu la gardes quand même. On ne sait jamais qui, ici, aurait le cœur d'aimer une carotte.");
                break;

            case "laitue":
                engine.AddItem("laitue");
                io.WriteLine("Une laitue simplement superbe MAIS une laitue quand même.");
                io.WriteLine("Tu la prends, va savoir pourquoi. Qui pourrait bien vouloir manger ÇA ?");
                break;

            case "repose_carotte":
                state.Inventory.Remove("carotte");
                io.WriteLine("Tu reposes la carotte dans le cageot. Tu peux toujours changer d'avis.");
                break;

            case "repose_laitue":
                state.Inventory.Remove("laitue");
                io.WriteLine("Tu reposes la laitue, presque soulagée de t'en débarrasser. Le cageot, lui, n'en veut pas non plus.");
                break;

            case "surprise":
                io.WriteLine("Le menu la soulignait trois fois. Trois mains différentes. Tu soulèves quand même la cloche.");
                io.WriteSlow("...");
                io.Pause();
                engine.Die("Sous la cloche, la « Surprise du chef » te regarde. Puis elle [rouge]sourit[/rouge]. Tu n'avais jamais vu un plat sourire. Tu ne verras plus jamais rien d'autre. Quelque part dans le lycée, un chef est très, très fier de lui.");
                break;

            default:
                io.WriteLine("Sage décision. Probablement.");
                break;
        }
    }
}
