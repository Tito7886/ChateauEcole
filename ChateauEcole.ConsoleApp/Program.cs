using System.Text;
using System.Text.Json;
using ChateauEcole.Core;
using ChateauEcole.Core.Models;

namespace ChateauEcole.ConsoleApp;

public static class Program
{
    // Langues gérées (fr = source/repli). L'italien viendra plus tard : ajouter "it" ici + world.it.json.
    private static readonly string[] Langues = { "fr", "en", "pl" };
    private static readonly (string Code, string Label)[] LangChoix =
    {
        ("fr", "Français"), ("en", "English"), ("pl", "Polski"),
    };

    public static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8; // accents corrects sous Windows

        // Sauvegardes, scores et préférence de langue dans %AppData%\ChateauEcole
        string saveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChateauEcole");

        string lang = SelectLanguage(saveDir); // fr / en / pl

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        WorldData world = JsonSerializer.Deserialize<WorldData>(ReadWorldJson(lang), options)
            ?? throw new InvalidOperationException("world.json invalide.");

        // Classement en ligne (scores.php sur Free). Pour changer d'URL/secret : éditer ici puis recompiler.
        const string ScoreUrl = "http://cefir.free.fr/scores.php";
        const string ScoreSecret = "cefir-chateau-2026";
        var online = new HttpScoreBoard(ScoreUrl, ScoreSecret);

        var engine = new GameEngine(world, new ConsoleIO(), new SaveService(saveDir), online);
        engine.Run();
    }

    /// <summary>
    /// Choisit la langue : préférence mémorisée (langue.cfg) si présente, sinon langue de l'OS
    /// comme défaut, proposée dans un petit sélecteur universel (une seule fois, puis mémorisée).
    /// Pour rechoisir plus tard : supprimer %AppData%\ChateauEcole\langue.cfg.
    /// </summary>
    private static string SelectLanguage(string saveDir)
    {
        string cfg = Path.Combine(saveDir, "langue.cfg");

        try
        {
            if (File.Exists(cfg))
            {
                string saved = File.ReadAllText(cfg).Trim().ToLowerInvariant();
                if (Array.IndexOf(Langues, saved) >= 0) return saved;
            }
        }
        catch { /* config illisible : on redemande */ }

        string detected = "fr";
        try
        {
            string two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (Array.IndexOf(Langues, two) >= 0) detected = two;
        }
        catch { /* pas de culture dispo : fr par défaut */ }

        // Sélecteur volontairement multilingue (compréhensible quelle que soit la langue).
        Console.WriteLine();
        Console.WriteLine("  Langue / Language / Język :");
        for (int i = 0; i < LangChoix.Length; i++)
            Console.WriteLine($"    {i + 1}. {LangChoix[i].Label}");
        string defLabel = LangChoix.First(l => l.Code == detected).Label;
        Console.Write($"  > (Entrée = {defLabel}) ");

        string chosen = detected;
        try
        {
            string? line = Console.ReadLine();
            if (int.TryParse(line, out int n) && n >= 1 && n <= LangChoix.Length)
                chosen = LangChoix[n - 1].Code;
        }
        catch { /* entrée redirigée : on garde le défaut */ }

        try { Directory.CreateDirectory(saveDir); File.WriteAllText(cfg, chosen); } catch { }
        return chosen;
    }

    /// <summary>
    /// Lit le contenu du monde pour la langue demandée. Ordre de recherche, pour chaque candidat
    /// (world.&lt;langue&gt;.json puis world.json en repli) : d'abord un fichier posé À CÔTÉ de l'exe
    /// (ajouter/tester une traduction sans recompiler), sinon la ressource embarquée. Le français
    /// (world.json) est toujours le repli — jamais d'écran vide si une traduction manque.
    /// </summary>
    private static string ReadWorldJson(string lang)
    {
        foreach (string name in LangFileCandidates(lang))
        {
            string external = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(external)) return File.ReadAllText(external);

            string? embedded = ReadEmbedded(name);
            if (embedded != null) return embedded;
        }
        throw new InvalidOperationException(
            "Monde introuvable : ni traduction, ni world.json (à côté de l'exe ou embarqué).");
    }

    private static IEnumerable<string> LangFileCandidates(string lang)
    {
        if (lang != "fr") yield return $"world.{lang}.json"; // la traduction demandée d'abord
        yield return "world.json";                           // puis le français (source + repli)
    }

    private static string? ReadEmbedded(string fileName)
    {
        var asm = typeof(Program).Assembly;
        string? resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resName == null) return null;

        using Stream stream = asm.GetManifestResourceStream(resName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Implémentation console de IGameIO. La future version 2D en fera une autre.
/// Les balises couleur inline (« [rouge]...[/rouge] ») sont interprétées AU NIVEAU de
/// WriteLine/WriteSlow : toute ligne — world.json ou C# des mini-jeux — est colorée
/// uniformément, sans que le Core connaisse la couleur. Les marqueurs d'effet
/// [slow]/[pause]/[clear] sont eux gérés par le moteur (GameEngine.Emit) ; s'ils
/// arrivaient malgré tout ici, ils sont ignorés (jamais affichés en clair).
/// </summary>
public class ConsoleIO : IGameIO
{
    private static readonly Dictionary<string, ConsoleColor> Couleurs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rouge"] = ConsoleColor.Red,     ["vert"] = ConsoleColor.Green,
        ["bleu"] = ConsoleColor.Blue,     ["jaune"] = ConsoleColor.Yellow,
        ["cyan"] = ConsoleColor.Cyan,     ["magenta"] = ConsoleColor.Magenta,
        ["gris"] = ConsoleColor.DarkGray, ["blanc"] = ConsoleColor.White,
    };

    /// <summary>Vitesse par défaut de la machine à écrire (ms/caractère), RÉGLABLE à chaud.
    /// Utilisée pour [slow] et pour WriteSlow sans vitesse. Repères : lent ≈ 55, normal ≈ 30,
    /// rapide ≈ 12.</summary>
    public static int VitesseParDefautMs = 50;

    /// <summary>
    /// Découpe le texte en segments (texte, couleur?, vitesse ms/car) en interprétant les
    /// balises INLINE de rendu : couleur ([rouge]...[/rouge], fermeture générique [/]),
    /// vitesse machine à écrire ([slow] = vitesse par défaut, [slow=NN] = NN ms/car, [/slow] =
    /// retour à l'instantané), ET pause chronométrée [pause=N] (attend N secondes AU MILIEU du
    /// texte, sans touche, puis reprend). Vitesse et pauses peuvent donc survenir PLUSIEURS fois
    /// dans une même ligne. <paramref name="vitesseBase"/> = vitesse hors balise (0 pour
    /// WriteLine, la vitesse demandée pour WriteSlow). Balises inconnues/mal fermées =
    /// littérales (jamais de plantage) ; marqueurs structurels [pause]/[clear] (sans « = »)
    /// ignorés ici (gérés par le moteur). Chaque segment porte PauseSec = secondes à attendre
    /// APRÈS son texte.
    /// </summary>
    private static IEnumerable<(string Text, ConsoleColor? Color, int Speed, int PauseSec)> Segments(string text, int vitesseBase)
    {
        ConsoleColor? couleur = null;
        int vitesse = vitesseBase;
        var buf = new StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                int close = text.IndexOf(']', i + 1);
                if (close > i)
                {
                    string tag = text.Substring(i + 1, close - i - 1);
                    // Couleur ouvrante
                    if (Couleurs.ContainsKey(tag))
                    { if (buf.Length > 0) { yield return (buf.ToString(), couleur, vitesse, 0); buf.Clear(); } couleur = Couleurs[tag]; i = close + 1; continue; }
                    // Fermetures
                    if (tag.StartsWith("/"))
                    {
                        string n = tag.Substring(1);
                        if (n.Equals("slow", StringComparison.OrdinalIgnoreCase))
                        { if (buf.Length > 0) { yield return (buf.ToString(), couleur, vitesse, 0); buf.Clear(); } vitesse = 0; i = close + 1; continue; }
                        if (n.Length == 0 || Couleurs.ContainsKey(n))
                        { if (buf.Length > 0) { yield return (buf.ToString(), couleur, vitesse, 0); buf.Clear(); } couleur = null; i = close + 1; continue; }
                    }
                    // Vitesse (machine à écrire)
                    if (tag.Equals("slow", StringComparison.OrdinalIgnoreCase))
                    { if (buf.Length > 0) { yield return (buf.ToString(), couleur, vitesse, 0); buf.Clear(); } vitesse = VitesseParDefautMs; i = close + 1; continue; }
                    if (tag.StartsWith("slow=", StringComparison.OrdinalIgnoreCase) && int.TryParse(tag.Substring(5), out int nn))
                    { if (buf.Length > 0) { yield return (buf.ToString(), couleur, vitesse, 0); buf.Clear(); } vitesse = nn; i = close + 1; continue; }
                    // Pause chronométrée AU MILIEU du texte : écrit ce qui précède, puis attend N s.
                    if (tag.StartsWith("pause=", StringComparison.OrdinalIgnoreCase) && int.TryParse(tag.Substring(6), out int ps))
                    { yield return (buf.ToString(), couleur, vitesse, ps); buf.Clear(); i = close + 1; continue; }
                    // Marqueurs structurels (gérés par le moteur) : jamais affichés
                    if (tag.Equals("pause", StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    { i = close + 1; continue; }
                }
            }
            buf.Append(text[i]); i++;
        }
        if (buf.Length > 0) yield return (buf.ToString(), couleur, vitesse, 0);
    }

    /// <summary>Retire toutes les balises → texte brut (mode redirigé, mesures). Fonction unique.</summary>
    public static string StripTags(string text)
    {
        var sb = new StringBuilder();
        foreach (var (seg, _, _, _) in Segments(text, 0)) sb.Append(seg);
        return sb.ToString();
    }

    /// <summary>Rend une ligne en respectant couleur, vitesse ET pauses chronométrées inline,
    /// puis un saut de ligne. vitesseBase = vitesse hors balise (0 = instantané).</summary>
    private void RenderLine(string text, int vitesseBase)
    {
        if (Console.IsOutputRedirected) { Console.WriteLine(StripTags(text)); return; }
        foreach (var (seg, color, speed, pauseSec) in Segments(text, vitesseBase))
        {
            if (color.HasValue) Console.ForegroundColor = color.Value;
            if (speed > 0) foreach (char ch in seg) { Console.Write(ch); Thread.Sleep(speed); }
            else Console.Write(seg);
            if (color.HasValue) Console.ResetColor();
            if (pauseSec > 0) Thread.Sleep(pauseSec * 1000); // pause silencieuse, puis ça repart
        }
        Console.WriteLine();
    }

    // Ligne normale : instantanée par défaut, mais anime les régions [slow]/[slow=NN].
    public void WriteLine(string text = "") => RenderLine(text, 0);

    // Machine à écrire : toute la ligne s'anime à la vitesse demandée (0 = vitesse par défaut),
    // avec possibilité de la faire varier au milieu via [slow=NN]/[/slow].
    public void WriteSlow(string text, int msParCaractere = 0)
        => RenderLine(text, msParCaractere > 0 ? msParCaractere : VitesseParDefautMs);

    public void Pause(string? message = null)
    {
        WriteLine(message ?? "[gris]— Appuie sur une touche pour continuer —[/gris]");
        if (Console.IsInputRedirected) return; // ne bloque pas les tests/pipes
        try { Console.ReadKey(intercept: true); } catch { /* pas de console interactive */ }
    }

    public void PauseThenClear(string? message = null)
    {
        Pause(message);
        Clear();
    }

    public void WaitThenClear(int secondes)
    {
        if (!Console.IsOutputRedirected)
        {
            try
            {
                for (int s = secondes; s > 0; s--) { Console.Write($"\r  . . . {s}   "); Thread.Sleep(1000); }
                Console.Write("\r               \r");
            }
            catch { /* ignore */ }
        }
        Clear();
    }

    public void Clear()
    {
        try { Console.Clear(); } catch { /* redirections : ignorer */ }
    }

    public void ShowRoomTitle(string roomName, string? titleArt = null)
    {
        Clear();

        // Sortie redirigée (tests, pipes) : un simple nom en clair suffit, rien à animer/encadrer.
        if (Console.IsOutputRedirected) { Console.WriteLine("=== " + StripTags(roomName) + " ==="); return; }

        if (!string.IsNullOrWhiteSpace(titleArt))
        {
            // Art fourni par la salle : rendu tel quel (les balises couleur sont interprétées).
            foreach (string ligne in titleArt.Replace("\r\n", "\n").Split('\n'))
                WriteLine(ligne);
            WriteLine();
            return;
        }

        // Encadré automatique : bordure adaptée à la longueur réelle du nom (accents compris).
        string nom = roomName.ToUpperInvariant();
        int largeur = nom.Length + 4;                 // 2 espaces de marge de chaque côté
        string barre = new string('═', largeur);
        WriteLine($"[cyan]╔{barre}╗[/cyan]");
        WriteLine($"[cyan]║  {nom}  ║[/cyan]");
        WriteLine($"[cyan]╚{barre}╝[/cyan]");
        WriteLine();
    }

    public string AskText(string prompt)
    {
        Console.WriteLine();
        Console.Write(StripTags(prompt) + " ");
        return Console.ReadLine() ?? "";
    }

    public int AskChoiceTimed(string prompt, IReadOnlyList<string> options, int timeoutSeconds, int defaultIndex)
    {
        // Entrée redirigée (tests, pipes) : pas de chrono possible -> choix classique.
        if (Console.IsInputRedirected)
            return AskChoice(prompt, options);

        WriteLine();
        WriteLine($"{prompt}  (tu as {timeoutSeconds} secondes — appuie sur un chiffre !)");
        for (int i = 0; i < options.Count; i++)
            WriteLine($"  {i + 1}. {options[i]}");

        try
        {
            DateTime limite = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            int dernierAffiche = -1;
            while (true)
            {
                double restant = (limite - DateTime.UtcNow).TotalSeconds;
                if (restant <= 0) break;

                // Décompte visible : on réécrit la même ligne chaque seconde (retour chariot).
                int secondes = (int)Math.Ceiling(restant);
                if (secondes != dernierAffiche)
                {
                    Console.Write($"\r  Temps restant : {secondes,2} s   > ");
                    dernierAffiche = secondes;
                }

                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    int n = key.KeyChar - '0';
                    if (n >= 1 && n <= options.Count)
                    {
                        Console.WriteLine(key.KeyChar);
                        return n - 1;
                    }
                }
                Thread.Sleep(25);
            }
            Console.WriteLine();
            Console.WriteLine("TROP LENT !");
            return defaultIndex;
        }
        catch (InvalidOperationException)
        {
            // Sécurité : si la lecture de touche échoue malgré tout, on retombe sur le choix classique.
            Console.WriteLine();
            return AskChoice(prompt, options);
        }
    }

    public int AskChoice(string prompt, IReadOnlyList<string> options, Action? onInvalid = null)
    {
        WriteLine();
        WriteLine(prompt);
        for (int i = 0; i < options.Count; i++)
            WriteLine($"  {i + 1}. {options[i]}");

        while (true)
        {
            Console.Write("> ");
            string? reponse = Console.ReadLine();
            if (int.TryParse(reponse, out int n) && n >= 1 && n <= options.Count)
                return n - 1;

            onInvalid?.Invoke();
            Console.WriteLine("Entrée invalide.... macaque ! (-2 pts)");
        }
    }
}
