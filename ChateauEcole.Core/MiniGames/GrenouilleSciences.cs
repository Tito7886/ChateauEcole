namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// La voie HONNÊTE de « l'essence du savoir animalier » : attraper la grenouille de
/// dissection dans la salle de Sciences. Aléatoire (best-of-3), retentable à volonté
/// (pas de softlock). N'apparaît qu'après avoir tenté la sortie (flag « sortie_tentee »,
/// géré par RituelOrgue + Room.SpecialActionRequiredFlag).
/// Récompense : grenouille_morte (= l'essence). Cameo aléatoire de Nestor.
/// </summary>
public static class GrenouilleSciences
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        var s = engine.State;
        if (s.Inventory.Contains("grenouille_morte"))
        {
            io.WriteLine("Le bocal de formol est vide : tu as déjà ta grenouille. Le prof de Sciences fixe le mur, immobile depuis 1912.");
            return;
        }
        if (s.Flags.Contains("grenouille_perdue"))
        {
            io.WriteLine("Le bocal est fracassé, vide. La grenouille s'est échappée pour de bon. La voie honnête est morte — il ne te reste plus qu'une seule offrande possible.");
            return;
        }

        io.WriteLine("Sur la paillasse, un vieux bocal. À l'intérieur, LA grenouille de dissection — parfaitement");
        io.WriteLine("conservée, parfaitement morte, et pourtant elle BOUGE. « L'essence du savoir animalier »,");
        io.WriteLine("annonce le prof de Sciences d'une voix de tombe. « Attrape-la. Si tu l'oses. »");

        if (Random.Shared.Next(4) == 0)
        {
            io.WriteLine();
            io.WriteLine("Du coin de l'œil, deux grandes oreilles vertes dépassent d'une armoire à réactifs.");
            io.WriteLine("Quand tu tournes la tête, elles se sauvent dans un ricanement aigu. « ...ce bg de ouf. »");
        }

        io.WriteLine();
        int prises = 0;
        for (int manche = 1; manche <= 3 && prises < 2; manche++)
        {
            int c = io.AskChoice($"Manche {manche} — la grenouille fixe une direction. Où plonges-tu ton filet ?",
                                 new List<string> { "À gauche", "À droite" });
            int saut = Random.Shared.Next(2);
            if (c == saut)
            {
                prises++;
                io.WriteLine($"CLAC ! Le filet se referme dessus. ({prises}/2)");
            }
            else
            {
                io.WriteLine("Elle bondit de l'autre côté dans un coassement moqueur.");
            }
            io.WriteLine();
        }

        if (prises >= 2)
        {
            io.WriteLine("Tu la coinces enfin au fond du filet. Elle cesse de bouger — vraiment, cette fois.");
            io.WriteLine("Le prof de Sciences hoche lentement la tête : « Le savoir a toujours un prix, petite. »");
            engine.AddItem("grenouille_morte");
            return;
        }

        // Échec : la grenouille s'échappe. Au 2e échec, elle disparaît DÉFINITIVEMENT.
        int echecs = (engine.State.Counters.TryGetValue("grenouille_echecs", out int e) ? e : 0) + 1;
        engine.State.Counters["grenouille_echecs"] = echecs;

        if (echecs < 2)
        {
            io.WriteLine("La grenouille file entre les mailles et se rétablit d'un bond, narquoise. Elle t'a échappé.");
            io.WriteLine("Tu peux retenter — mais dépêche-toi. Une bête pareille ne restera pas coincée éternellement.");
            return;
        }

        // 2e échec : point de non-retour.
        engine.State.Flags.Add("grenouille_perdue");
        io.WriteLine();
        io.WriteLine("Cette fois, la grenouille bondit trop loin — elle se faufile par une fissure du mur et DISPARAÎT.");
        io.WriteLine("Le bocal roule à terre et se brise. C'était ta dernière chance de faire les choses proprement.");
        io.WriteLine(engine.T("La voie honnête est morte, {NOM}. Il ne te reste qu'une seule chose à offrir à l'orgue... et elle trottine à tes côtés."));
        io.WriteLine();
        io.WriteLine("Tes jambes te portent malgré toi vers l'église. Tu ne décides plus rien, maintenant.");
        engine.TeleportTo("eglise");
    }
}
