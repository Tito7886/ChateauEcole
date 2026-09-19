using System.Text;
using System.Text.Json;
using ChateauEcole.Core;

namespace ChateauEcole.ConsoleApp;

/// <summary>
/// Traduction de l'interface via un fichier ui_&lt;langue&gt;.json (map « français » → « traduit »).
/// Modèle gettext : le français est la clé ET le repli. Fichier introuvable / clé absente /
/// langue = fr → on renvoie le français. Cherché d'abord À CÔTÉ de l'exe (ajout/test sans
/// recompiler), sinon embarqué. Best-effort : jamais d'exception si le fichier manque.
/// </summary>
public class JsonLocalizer : ILocalizer
{
    private readonly Dictionary<string, string> _map;

    public JsonLocalizer(string lang)
    {
        _map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (lang == "fr") return; // le français est la source : aucune table nécessaire

        try
        {
            string? json = ReadUiFile($"ui_{lang}.json");
            if (json != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                    foreach (var kv in dict)
                        _map[kv.Key] = kv.Value;
            }
        }
        catch { /* table illisible : on reste en français */ }
    }

    public string Tr(string francais)
        => _map.TryGetValue(francais, out var t) && !string.IsNullOrEmpty(t) ? t : francais;

    private static string? ReadUiFile(string name)
    {
        string external = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(external)) return File.ReadAllText(external);

        var asm = typeof(JsonLocalizer).Assembly;
        string? resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        if (resName == null) return null;

        using Stream stream = asm.GetManifestResourceStream(resName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
