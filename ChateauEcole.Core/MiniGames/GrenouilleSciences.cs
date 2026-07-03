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
        }
        else
        {
            io.WriteLine("La grenouille se rétablit sous le bocal, narquoise. Ratée pour cette fois.");
            io.WriteLine("Tu peux retenter ta chance — elle n'ira nulle part. Elle est là depuis un siècle.");
        }
    }
}
