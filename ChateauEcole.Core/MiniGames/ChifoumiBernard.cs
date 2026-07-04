namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// Mini-jeu de la classe de Maths : pierre-feuille-ciseaux contre Mme Bernard.
/// Récompense : la clef de la classe de Sciences.
/// Exemple d'"action spéciale" codée en C# et référencée depuis world.json.
/// </summary>
public static class ChifoumiBernard
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        if (engine.State.Flags.Contains("bernard_vaincue"))
        {
            io.WriteLine("Mme Bernard : « Reviens me voir quand tu veux Bichette, mais là j'ai du travail ! »");
            return;
        }

        // Humeur aléatoire à la 1re visite... mais défi GARANTI dès la 2e visite (pity timer),
        // pour qu'un joueur malchanceux ne reste jamais bloqué sur du hasard (verrou de départ).
        int visites = engine.State.Counters.TryGetValue("bernard_visites", out int v) ? v + 1 : 1;
        engine.State.Counters["bernard_visites"] = visites;

        string[] humeurs = { "defi", "out", "blabla" };
        string humeur = visites >= 2 ? "defi" : humeurs[Random.Shared.Next(humeurs.Length)];

        if (humeur == "out")
        {
            io.WriteLine("Mme Bernard : « Tu n'as rien à faire ici, allez, dehors Bichette ! »");
            return;
        }
        if (humeur == "blabla")
        {
            io.WriteLine("Mme Bernard : « Ah coucou Bichette ! J'ai encore beaucoup de travail,");
            io.WriteLine("reviens plus tard, j'aurai un petit jeu pour toi. »");
            return;
        }

        io.WriteLine($"Mme Bernard : « Je sais bien que tu t'appelles {engine.State.PlayerName}, mais ici, tout le monde est Bichette. »");
        io.WriteLine("Mme Bernard : « Ahah Bichette ! J'ai bien envie d'une partie de pierre-feuille-ciseaux ! »");
        io.WriteLine("Vous voilà embarquée dans une partie endiablée... il y aura peut-être une récompense !");

        string[] coups = { "Pierre", "Feuille", "Ciseaux" };
        int joueurWins = 0;
        int bernardWins = 0;

        while (joueurWins < 2 && bernardWins < 2)
        {
            int j = io.AskChoice("Votre coup :", coups);
            int b = Random.Shared.Next(3);
            io.WriteLine($"Mme Bernard joue : {coups[b]}");

            if (j == b)
            {
                io.WriteLine("Égalité !");
            }
            else if ((j - b + 3) % 3 == 1) // Pierre>Ciseaux, Feuille>Pierre, Ciseaux>Feuille
            {
                joueurWins++;
                io.WriteLine("Tu as gagné cette manche !");
            }
            else
            {
                bernardWins++;
                io.WriteLine("Mme Bernard gagne cette manche !");
            }
            io.WriteLine($"Score — Mme Bernard : {bernardWins} | Toi : {joueurWins}");
            io.WriteLine();
        }

        if (joueurWins > bernardWins)
        {
            io.WriteLine("Mme Bernard : « Bravo Bichette, félicitations ! Tu sais quoi ? Le proviseur adorait les codes. »");
            io.WriteLine("Elle griffonne un chiffre sur un coin de copie et te le tend :");
            io.WriteLine(engine.T("* « Le TROISIÈME chiffre c'est un {CODE3}. Ne le perds pas, Bichette. »"));
            engine.State.Flags.Add("fragment_3_trouve");
            engine.State.Flags.Add("bernard_vaincue");
        }
        else
        {
            // Pénalité silencieuse : le joueur ne voit PAS le -2 pts ici (contrairement aux autres).
            engine.State.Score -= 2;
            io.WriteLine("Désolé Bichette, mais Mme Bernard est trop forte !");
            io.WriteLine("Elle te raccompagne gentiment à la porte... Retente ta chance plus tard.");
        }
    }
}
