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

        string path = Path.Combine(AppContext.BaseDirectory, "world.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        WorldData world = JsonSerializer.Deserialize<WorldData>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException("world.json introuvable ou invalide.");

        // Sauvegardes et scores dans %AppData%\ChateauEcole : ils survivent aux recompilations
        string saveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChateauEcole");

        var engine = new GameEngine(world, new ConsoleIO(), new SaveService(saveDir));
        engine.Run();
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
        Console.WriteLine();
        Console.WriteLine($"{prompt}  ({timeoutSeconds} secondes pour choisir !)");
        for (int i = 0; i < options.Count; i++)
            Console.WriteLine($"  {i + 1}. {options[i]}");
        Console.Write("> ");

        try
        {
            DateTime limite = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < limite)
            {
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
            // Entrée redirigée (tests, pipes) : pas de chrono possible, choix classique
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
