using System.Text;
using System.Text.Json;
using ChateauEcole.Core;
using ChateauEcole.Core.Models;

namespace ChateauEcole.ConsoleApp;

public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8; // accents corrects sous Windows

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        WorldData world = JsonSerializer.Deserialize<WorldData>(ReadWorldJson(), options)
            ?? throw new InvalidOperationException("world.json invalide.");

        // Sauvegardes et scores dans %AppData%\ChateauEcole : ils survivent aux recompilations
        string saveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChateauEcole");

        var engine = new GameEngine(world, new ConsoleIO(), new SaveService(saveDir));
        engine.Run();
    }

    /// <summary>
    /// Lit le contenu de world.json. Priorité à un fichier posé À CÔTÉ de l'exe
    /// (permet d'éditer/modder le jeu sans recompiler) ; sinon, la ressource embarquée
    /// dans l'exe (ce qui rend l'exe autonome : un seul fichier suffit).
    /// </summary>
    private static string ReadWorldJson()
    {
        string external = Path.Combine(AppContext.BaseDirectory, "world.json");
        if (File.Exists(external))
            return File.ReadAllText(external);

        var asm = typeof(Program).Assembly;
        string? resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("world.json", StringComparison.OrdinalIgnoreCase));
        if (resName == null)
            throw new InvalidOperationException(
                "world.json introuvable : ni à côté de l'exe, ni embarqué dans l'assembly.");

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

    /// <summary>Découpe le texte en segments (texte, couleur?). Balises inconnues = littérales,
    /// balises mal fermées = pas de plantage, marqueurs [slow]/[pause]/[clear] = ignorés ici.</summary>
    private static IEnumerable<(string Text, ConsoleColor? Color)> Segments(string text)
    {
        ConsoleColor? courant = null;
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
                    if (Couleurs.ContainsKey(tag))
                    { if (buf.Length > 0) { yield return (buf.ToString(), courant); buf.Clear(); } courant = Couleurs[tag]; i = close + 1; continue; }
                    if (tag.StartsWith("/"))
                    {
                        string n = tag.Substring(1);
                        if (n.Length == 0 || Couleurs.ContainsKey(n))
                        { if (buf.Length > 0) { yield return (buf.ToString(), courant); buf.Clear(); } courant = null; i = close + 1; continue; }
                    }
                    if (tag.Equals("slow", StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("pause", StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    { i = close + 1; continue; } // marqueurs d'effet : jamais affichés
                }
            }
            buf.Append(text[i]); i++;
        }
        if (buf.Length > 0) yield return (buf.ToString(), courant);
    }

    /// <summary>Retire toutes les balises → texte brut (mode redirigé, mesures). Fonction unique.</summary>
    public static string StripTags(string text)
    {
        var sb = new StringBuilder();
        foreach (var (seg, _) in Segments(text)) sb.Append(seg);
        return sb.ToString();
    }

    public void WriteLine(string text = "")
    {
        if (Console.IsOutputRedirected) { Console.WriteLine(StripTags(text)); return; }
        foreach (var (seg, color) in Segments(text))
        {
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.Write(seg);
            if (color.HasValue) Console.ResetColor();
        }
        Console.WriteLine();
    }

    public void WriteSlow(string text, int msParCaractere = 30)
    {
        // Sortie redirigée (tests, pipes) : instantané, sans balise (sinon les tests traînent).
        if (Console.IsOutputRedirected) { Console.WriteLine(StripTags(text)); return; }
        foreach (var (seg, color) in Segments(text))
        {
            if (color.HasValue) Console.ForegroundColor = color.Value;
            foreach (char ch in seg) { Console.Write(ch); Thread.Sleep(msParCaractere); }
            if (color.HasValue) Console.ResetColor();
        }
        Console.WriteLine();
    }

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
