using System.Text.Json;
using ChateauEcole.Core;

namespace ChateauEcole.ConsoleApp;

/// <summary>
/// Classement en ligne via HTTP (scores.php, hébergement Free). BEST-EFFORT : tout échec ou
/// timeout se traduit par une liste vide / une publication silencieuse — le jeu retombe alors
/// sur les scores locaux. Vit côté application hôte : le moteur (Core) ne touche jamais le réseau
/// (il ne connaît que l'interface IScoreBoard, comme IGameIO pour la console).
/// </summary>
public class HttpScoreBoard : IScoreBoard
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly string _url;
    private readonly string _secret;

    public HttpScoreBoard(string url, string secret)
    {
        _url = url;
        _secret = secret;
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; // jamais de blocage durable
        // Free bloque parfois les requêtes sans User-Agent « navigateur ».
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ChateauEcole/1.0");
        return c;
    }

    public List<HighScore> GetTop(int count)
    {
        try
        {
            string json = Http.GetStringAsync(_url).GetAwaiter().GetResult();
            var entries = JsonSerializer.Deserialize<List<Entry>>(json, JsonOpts) ?? new List<Entry>();
            return entries.Take(count).Select(e => new HighScore
            {
                Name = string.IsNullOrEmpty(e.Name) ? "?" : e.Name!,
                Score = e.Score,
                Mark = e.Mark ?? "",
                Victory = !string.IsNullOrEmpty(e.Mark) && e.Mark != "[MORT]" && e.Mark != "[LACHE]" && e.Mark != "[disparu]",
                Date = DateTime.TryParse(e.Date, out var d) ? d : DateTime.MinValue
            }).ToList();
        }
        catch
        {
            return new List<HighScore>(); // hors-ligne / serveur muet / JSON invalide
        }
    }

    public void Submit(HighScore entry)
    {
        try
        {
            string mark = entry.Mark ?? (entry.Victory ? "[ÉVADÉ]" : "[MORT]");
            // Heure LOCALE du joueur (pour le fun : « joué à 4h du matin »), pas celle du serveur.
            DateTime quand = entry.Date == default ? DateTime.Now : entry.Date;
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", _secret),
                new KeyValuePair<string, string>("name", entry.Name),
                new KeyValuePair<string, string>("score", entry.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("mark", mark),
                new KeyValuePair<string, string>("when", quand.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)),
            });
            using var resp = Http.PostAsync(_url, form).GetAwaiter().GetResult();
            // Corps ignoré : best-effort.
        }
        catch
        {
            /* silencieux : le classement en ligne ne doit jamais gêner la partie */
        }
    }

    /// <summary>Miroir du JSON renvoyé par scores.php.</summary>
    private sealed class Entry
    {
        public string? Name { get; set; }
        public int Score { get; set; }
        public string? Mark { get; set; }
        public string? Date { get; set; }
    }
}
