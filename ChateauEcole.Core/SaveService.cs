using System.Text.Json;

namespace ChateauEcole.Core;

/// <summary>Une entrée du tableau des meilleurs scores.</summary>
public class HighScore
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public bool Victory { get; set; }

    /// <summary>Marque affichée pour une victoire (ex. « [ÉVADÉ] », « [À QUEL PRIX] »). Null = défaut.</summary>
    public string? Mark { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>
/// Sauvegarde (1 slot, JSON) + meilleurs scores (top 10, JSON).
/// Le répertoire est fourni par l'application hôte (console aujourd'hui,
/// version 2D demain) — le moteur ne décide pas où écrire.
/// Toutes les erreurs disque sont avalées : perdre une sauvegarde
/// ne doit jamais faire planter le jeu.
/// </summary>
public class SaveService
{
    private readonly string _dir;
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    private string CheckpointPath => Path.Combine(_dir, "sauvegarde.json");
    private string ScoresPath => Path.Combine(_dir, "highscores.json");

    public SaveService(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    // ----- Sauvegarde (checkpoint) -----

    public void SaveCheckpoint(GameState state)
    {
        try { File.WriteAllText(CheckpointPath, JsonSerializer.Serialize(state, Opts)); }
        catch { /* disque indisponible : tant pis pour ce checkpoint */ }
    }

    public GameState? LoadCheckpoint()
    {
        try
        {
            if (!File.Exists(CheckpointPath)) return null;
            return JsonSerializer.Deserialize<GameState>(File.ReadAllText(CheckpointPath));
        }
        catch { return null; }
    }

    public bool HasCheckpoint() => File.Exists(CheckpointPath);

    public void DeleteCheckpoint()
    {
        try { File.Delete(CheckpointPath); } catch { }
    }

    // ----- Meilleurs scores -----

    public List<HighScore> LoadHighScores()
    {
        try
        {
            if (!File.Exists(ScoresPath)) return new List<HighScore>();
            return JsonSerializer.Deserialize<List<HighScore>>(File.ReadAllText(ScoresPath))
                   ?? new List<HighScore>();
        }
        catch { return new List<HighScore>(); }
    }

    public void AddHighScore(string name, int score, bool victory, string? mark = null)
    {
        var scores = LoadHighScores();
        scores.Add(new HighScore { Name = name, Score = score, Victory = victory, Mark = mark, Date = DateTime.Now });
        scores = scores.OrderByDescending(s => s.Score).Take(10).ToList();
        try { File.WriteAllText(ScoresPath, JsonSerializer.Serialize(scores, Opts)); }
        catch { }
    }
}
