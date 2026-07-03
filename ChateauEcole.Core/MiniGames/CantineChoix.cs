namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// Les cuisines de la cantine : trois choses à prendre, chacune une seule fois.
/// Le sandwich est l'objet utile (surveillant), la pomme un petit bonus,
/// la « Surprise du chef » une très mauvaise idée (-5 pts, mais quelle ambiance).
/// </summary>
public static class CantineChoix
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        var state = engine.State;
        var options = new List<(string Label, string Id)>();

        if (!state.Flags.Contains("cantine_sandwich"))
            options.Add(("Le sandwich sous cellophane, posé bien en évidence au milieu du plan de travail", "sandwich"));
        if (!state.Flags.Contains("cantine_surprise"))
            options.Add(("La « Surprise du chef » qui frémit doucement sous sa cloche, en chambre froide", "surprise"));
        if (!state.Flags.Contains("cantine_pomme"))
            options.Add(("Une pomme rouge. Parfaite. Un peu trop parfaite", "pomme"));

        if (options.Count == 0)
        {
            io.WriteLine("Les cuisines sont définitivement vides. Le frigo enchaîné préférerait que tu n'insistes pas.");
            return;
        }

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

            case "surprise":
                state.Flags.Add("cantine_surprise");
                state.Score -= 5;
                io.WriteLine("Tu soulèves la cloche de la « Surprise du chef ».");
                io.WriteLine("...");
                io.WriteLine("Tu la reposes immédiatement. Tu ne dormiras plus jamais tout à fait pareil. (-5 pts)");
                io.WriteLine("Quelque part dans le lycée, un chef est très fier de lui.");
                break;

            case "pomme":
                state.Flags.Add("cantine_pomme");
                engine.AddItem("pomme");
                io.WriteLine("Une pomme par jour éloigne le médecin. Ici, tu prends tout ce qui éloigne quoi que ce soit.");
                break;

            default:
                io.WriteLine("Sage décision. Probablement.");
                break;
        }
    }
}
