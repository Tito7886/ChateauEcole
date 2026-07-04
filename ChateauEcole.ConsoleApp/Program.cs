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

/// <summary>Implémentation console de IGameIO. La future version 2D en fera une autre.</summary>
public class ConsoleIO : IGameIO
{
    public void WriteLine(string text = "") => Console.WriteLine(text);

    public void Clear()
    {
        try { Console.Clear(); } catch { /* redirections : ignorer */ }
    }

    public string AskText(string prompt)
    {
        Console.WriteLine();
        Console.Write(prompt + " ");
        return Console.ReadLine() ?? "";
    }

    public int AskChoiceTimed(string prompt, IReadOnlyList<string> options, int timeoutSeconds, int defaultIndex)
    {
        // Entrée redirigée (tests, pipes) : pas de chrono possible -> choix classique.
        if (Console.IsInputRedirected)
            return AskChoice(prompt, options);

        Console.WriteLine();
        Console.WriteLine($"{prompt}  (tu as {timeoutSeconds} secondes — appuie sur un chiffre !)");
        for (int i = 0; i < options.Count; i++)
            Console.WriteLine($"  {i + 1}. {options[i]}");

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
        Console.WriteLine();
        Console.WriteLine(prompt);
        for (int i = 0; i < options.Count; i++)
            Console.WriteLine($"  {i + 1}. {options[i]}");

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
