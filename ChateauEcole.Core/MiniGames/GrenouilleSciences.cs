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
            io.WriteLine("Le bocal de formol est vide : tu as déjà ta grenouille. Le prof de Sciences fixe le mur.");
            return;
        }
        if (s.Flags.Contains("grenouille_perdue"))
        {
            io.WriteLine("Le bocal est fracassé, vide. La grenouille s'est échappée pour de bon. Il ne te reste plus qu'une seule solution ...");
            return;
        }

        io.WriteLine("Sur la paillasse, un vieux bocal. À l'intérieur, LA grenouille de dissection — parfaitement");
        io.WriteLine("conservée, parfaitement morte, et pourtant elle BOUGE. « L'essence du savoir animalier »,");
        io.WriteLine("annonce le prof de Sciences d'une voix de tombe. « Attrape-la. Si tu l'oses. »");

        if (Random.Shared.Next(4) == 0)
        {
            io.WriteLine();
            io.WriteLine("Du coin de l'œil, deux grandes oreilles vertes dépassent d'une armoire à réactifs.");
            io.WriteLine("Quand tu tournes la tête, elles se sauvent dans un ricanement aigu. « ...j'suis trop bg. »");
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
            io.WriteLine("Le prof de Sciences hoche lentement la tête : « J'étais persuadé que tu raterais... »");
            engine.AddItem("grenouille_morte");
            return;
        }

        // Échec : la grenouille s'échappe. Au 2e échec, elle disparaît DÉFINITIVEMENT.
        int echecs = (engine.State.Counters.TryGetValue("grenouille_echecs", out int e) ? e : 0) + 1;
        engine.State.Counters["grenouille_echecs"] = echecs;
        /*
        if (echecs < 1)
        {
            io.WriteLine("La grenouille file entre les mailles et se rétablit d'un bond, narquoise. Elle t'a échappé.");
            io.WriteLine("Tu peux retenter — mais dépêche-toi. Une bête pareille ne restera pas coincée éternellement.");
            return;
        }
        */
        // 2e échec : point de non-retour.
        engine.State.Flags.Add("grenouille_perdue");
        io.WriteLine();
        io.WriteSlow("Cette fois, la grenouille bondit trop loin — elle se faufile par une fissure du mur et [rouge]DISPARAÎT[/rouge].");
        io.WriteLine("Le bocal roule à terre et se brise. C'était ta dernière chance de faire les choses proprement.");
        io.WriteSlow(engine.T("La voie honnête est morte, {NOM}. Il ne te reste qu'une seule chose à offrir à l'orgue... et elle trottine à tes côtés."));
        io.PauseThenClear();
        io.WriteSlow("Tes jambes te portent malgré toi vers l'église. Tu ne décides plus rien, maintenant.");
        io.Pause(); // laisser lire avant que l'écran-titre de l'église n'efface l'écran
        engine.TeleportTo("eglise");
    }
}
