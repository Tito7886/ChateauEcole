namespace ChateauEcole.Core;

/// <summary>
/// Tout l'état d'une partie. Propriétés settables pour la (dé)sérialisation
/// JSON de la sauvegarde (System.Text.Json).
/// </summary>
public class GameState
{
    public string PlayerName { get; set; } = "";
    public string CurrentRoomId { get; set; } = "";
    public List<string> Inventory { get; set; } = new();
    public HashSet<string> Flags { get; set; } = new();

    /// <summary>Compteurs génériques (visites, tentatives...) — utilisés par les mini-jeux.</summary>
    public Dictionary<string, int> Counters { get; set; } = new();

    /// <summary>
    /// Objets posés au sol, par salle (roomId -> ids d'objets). Alimenté quand le
    /// joueur lâche un objet (sac plein) ou refuse d'en prendre un. Sérialisé avec
    /// la partie : la réanimation restaure donc les objets au sol.
    /// </summary>
    public Dictionary<string, List<string>> FloorItems { get; set; } = new();
    public int Score { get; set; }
    public int MaxInventory { get; set; } = 8;
    public bool IsDead { get; set; }

    /// <summary>Lapinou apprivoisé accompagne le joueur (compagnon persistant, sérialisé).</summary>
    public bool LapinouSuit { get; set; }

    /// <summary>La réanimation unique a-t-elle déjà été consommée ?</summary>
    public bool ResurrectionUsed { get; set; }
    public bool IsVictory { get; set; }

    /// <summary>Vrai si la victoire a été obtenue par le sacrifice de Lapinou (fin immorale).</summary>
    public bool VictoryImmoral { get; set; }

    public void Reset(string startRoom)
    {
        CurrentRoomId = startRoom;
        Inventory.Clear();
        Flags.Clear();
        Counters.Clear();
        FloorItems.Clear();
        Score = 0;
        IsDead = false;
        IsVictory = false;
        VictoryImmoral = false;
        LapinouSuit = false;
        ResurrectionUsed = false;
    }
}
