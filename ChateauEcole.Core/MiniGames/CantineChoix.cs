namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// Les cuisines de la cantine : le sandwich (surveillant → bureau) et la carotte
/// (apprivoiser Lapinou à l'aumônerie). La « Surprise du chef » est un piège mortel pur
/// Undertale — le menu la signale (soulignée trois fois par trois mains différentes).
/// </summary>
public static class CantineChoix
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        var state = engine.State;
        var options = new List<(string Label, string Id)>();

        if (!state.Flags.Contains("cantine_sandwich"))
            options.Add(("Le sandwich sous cellophane, posé bien en évidence au milieu du plan de travail", "sandwich"));
        if (!state.Flags.Contains("cantine_carotte"))
            options.Add(("Une carotte oubliée dans un cageot, encore fraîche, presque trop orange", "carotte"));
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
                state.Flags.Add("cantine_carotte");
                engine.AddItem("carotte");
                io.WriteLine("Une carotte, dans un lycée où tout pourrit ou se fige. Trop fraîche pour être honnête.");
                io.WriteLine("Tu la gardes quand même. On ne sait jamais qui, ici, aurait le cœur d'aimer une carotte.");
                break;

            case "surprise":
                io.WriteLine("Le menu la soulignait trois fois. Trois mains différentes. Tu soulèves quand même la cloche.");
                io.WriteLine("...");
                engine.Die("Sous la cloche, la « Surprise du chef » te regarde. Puis elle sourit. Tu n'avais jamais vu un plat sourire. Tu ne verras plus jamais rien d'autre. Quelque part dans le lycée, un chef est très, très fier de lui.");
                break;

            default:
                io.WriteLine("Sage décision. Probablement.");
                break;
        }
    }
}
