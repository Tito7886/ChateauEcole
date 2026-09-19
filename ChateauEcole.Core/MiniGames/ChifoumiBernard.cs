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

        io.WriteLine(engine.L("Mme Bernard : « Je sais bien que tu t'appelles {NOM}, mais ici, tout le monde est Bichette. »"));
        io.WriteLine("Mme Bernard : « Ahah Bichette ! J'ai bien envie d'une partie de pierre-feuille-ciseaux ! »");
        io.WriteLine("Vous voilà embarquée dans une partie endiablée... il y aura peut-être une récompense !");

        string[] coups = { "Pierre", "Feuille", "Ciseaux" };
        int joueurWins = 0;
        int bernardWins = 0;

        while (joueurWins < 2 && bernardWins < 2)
        {
            int j = io.AskChoice("Votre coup :", coups);
            int b = Random.Shared.Next(3);
            io.WriteLine(engine.L("Mme Bernard joue : {0}", engine.L(coups[b])));

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
            io.WriteLine(engine.L("Score — Mme Bernard : {0} | Toi : {1}", bernardWins, joueurWins));
            io.WriteLine();
        }

        if (joueurWins > bernardWins)
        {
            // Le 3e chiffre n'est PLUS donné en clair : Bernard le déguise en petit calcul.
            int code3 = engine.State.CodeCombination.Length == 4 ? engine.State.CodeCombination[2] - '0' : 0;
            var (phrase, _) = GenererCalcul(code3, engine);
            io.WriteLine("Mme Bernard : « Bravo Bichette, félicitations ! Tu sais quoi ? Le proviseur ADORAIT les codes. »");
            io.WriteLine("Mme Bernard : « Le 3e chiffre de la serrure du sous-sol ? Pour une matheuse comme toi, c'est cadeau : »");
            io.WriteLine(engine.L("* « C'est {0}. Débrouille-toi, Bichette. Et note-le, je ne le répéterai pas. »", phrase));
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

    /// <summary>
    /// Génère un petit calcul (« a moins b », « a plus b » ou « a fois b ») dont le résultat
    /// vaut EXACTEMENT <paramref name="chiffre"/> (0-9), faisable de tête, non ambigu.
    /// La soustraction est toujours disponible (fallback garanti). Public pour le test.
    /// </summary>
    public static (string Phrase, int Resultat) GenererCalcul(int chiffre, GameEngine? engine = null)
    {
        // Les nombres et opérateurs sont traduits (via engine.L) pour que l'indice reste lisible
        // dans la langue du joueur. Sans engine (tests), on reste en français.
        string L(string fr) => engine != null ? engine.L(fr) : fr;
        string[] mots =
        {
            L("zéro"), L("un"), L("deux"), L("trois"), L("quatre"), L("cinq"), L("six"),
            L("sept"), L("huit"), L("neuf"), L("dix"), L("onze"), L("douze")
        };
        string moins = L("moins"), plus = L("plus"), fois = L("fois");
        var candidats = new List<string>();

        // Soustraction : a - b = chiffre (a <= 12, b >= 1) — toujours au moins une possibilité.
        for (int b = 1; b <= 9; b++)
        {
            int a = chiffre + b;
            if (a <= 12) candidats.Add($"{mots[a]} {moins} {mots[b]}");
        }
        // Addition : a + b = chiffre (a, b >= 1) — pour chiffre >= 2.
        for (int a = 1; a < chiffre; a++)
        {
            int b = chiffre - a;
            if (b >= 1 && b <= 9) candidats.Add($"{mots[a]} {plus} {mots[b]}");
        }
        // Multiplication : x * y = chiffre (x, y >= 2) — pour 4, 6, 8, 9.
        for (int x = 2; x <= 9; x++)
        {
            if (chiffre % x == 0)
            {
                int y = chiffre / x;
                if (y >= 2 && y <= 9) candidats.Add($"{mots[x]} {fois} {mots[y]}");
            }
        }

        return (candidats[Random.Shared.Next(candidats.Count)], chiffre);
    }
}
