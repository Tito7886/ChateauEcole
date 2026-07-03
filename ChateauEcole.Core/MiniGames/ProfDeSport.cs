namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// Mini-jeu du gymnase : échapper au prof de sport.
/// Trois choix CHRONOMÉTRÉS (5 s chacun) — trop lent ou mauvais choix = rattrapé,
/// tour de terrain punitif (-5 pts) et éjection dans la cour.
/// Alternative instantanée : brandir la feuille de verbes irréguliers anglais,
/// la seule chose au monde qui le terrifie.
/// Récompense : un tube de balles de tennis.
/// </summary>
public static class ProfDeSport
{
    private const int DelaiSecondes = 5;

    public static void Jouer(GameEngine engine, IGameIO io)
    {
        if (engine.State.Flags.Contains("gym_vaincu"))
        {
            io.WriteLine("Le terrain est calme. Le prof de sport n'est plus en état de te poursuivre... pour l'instant.");
            return;
        }

        io.WriteLine("Un coup de sifflet strident déchire le silence.");
        io.WriteLine("LE PROF DE SPORT surgit des vestiaires, le regard vide, le survêtement immaculé :");
        io.WriteLine("« TIENS TIENS. UNE ÉLÈVE. ÉCHAUFFEMENT. DIX TOURS DE TERRAIN. POUR COMMENCER. »");
        io.WriteLine();

        // L'arme secrète : les verbes irréguliers anglais (sa seule faiblesse)
        if (engine.State.Inventory.Contains("cours_anglais"))
        {
            int c = io.AskChoice("Il approche. Que faire ?", new List<string>
            {
                "Brandir la feuille de verbes irréguliers anglais",
                "Tenter la fuite"
            });
            if (c == 0)
            {
                io.WriteLine();
                io.WriteLine("Tu brandis la feuille à bout de bras : « TO BE ! WAS, WERE, BEEN ! »");
                io.WriteLine("Le prof pousse un hurlement inhumain — « NOOON, PAS LES IRRÉGULIEEERS » —");
                io.WriteLine("et s'enfuit par la fenêtre de la réserve. On entend son sifflet décroître dans la nuit.");
                Victoire(engine, io);
                return;
            }
            io.WriteLine();
        }

        io.WriteLine("Il faut fuir. VITE. Chaque hésitation te coûtera cher...");

        // Étape 1
        if (!Etape(io,
            "Il fonce droit sur toi !",
            new[] { "Courir en ligne droite", "Plonger derrière le cheval d'arçons", "T'excuser poliment" },
            bonneReponse: 1, parDefaut: 0,
            reussite: "Tu plonges derrière le cheval d'arçons ! Il le contourne — ça te laisse deux secondes."))
        { Rattrapee(engine, io); return; }

        // Étape 2
        if (!Etape(io,
            "Il te coupe la route près des espaliers !",
            new[] { "Grimper aux espaliers", "Bifurquer derrière la pile de tapis", "Lui faire face" },
            bonneReponse: 1, parDefaut: 2,
            reussite: "Tu bifurques derrière les tapis ! Il glisse sur un plot et jure dans une langue morte."))
        { Rattrapee(engine, io); return; }

        // Étape 3
        if (!Etape(io,
            "La réserve est droit devant. Il plonge pour te plaquer !",
            new[] { "Sauter par-dessus le banc", "T'arrêter net", "Glissade sous le filet de volley" },
            bonneReponse: 2, parDefaut: 1,
            reussite: "Glissade parfaite sous le filet ! Il s'emmêle dedans en beuglant des consignes d'étirement."))
        { Rattrapee(engine, io); return; }

        io.WriteLine();
        io.WriteLine("Tu claques la porte de la réserve derrière toi. Le voilà enfermé, tambourinant en rythme.");
        Victoire(engine, io);
    }

    private static bool Etape(IGameIO io, string situation, string[] options, int bonneReponse, int parDefaut, string reussite)
    {
        io.WriteLine();
        int choix = io.AskChoiceTimed(situation, options, DelaiSecondes, parDefaut);
        if (choix == bonneReponse)
        {
            io.WriteLine(reussite);
            return true;
        }
        return false;
    }

    private static void Rattrapee(GameEngine engine, IGameIO io)
    {
        engine.State.Score -= 5;
        io.WriteLine();
        io.WriteLine("Une poigne de fer se referme sur ton capuchon. « DIX TOURS. ON NE DISCUTE PAS. »");
        io.WriteLine("Tu ressors du gymnase les jambes en coton, expulsée dans la cour. (-5 pts)");
        io.WriteLine("Tu pourras retenter ta chance... ou trouver sa faiblesse. Il doit bien en avoir une.");
        engine.TeleportTo("cour");
    }

    private static void Victoire(GameEngine engine, IGameIO io)
    {
        io.WriteLine("Sur le banc de touche, son sac est resté ouvert : un tube de balles de tennis en dépasse.");
        engine.AddItem("balles_tennis");
        engine.State.Flags.Add("gym_vaincu");
    }
}
