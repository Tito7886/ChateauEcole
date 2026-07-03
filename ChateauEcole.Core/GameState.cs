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
    public int Score { get; set; }
    public int MaxInventory { get; set; } = 8;
    public bool IsDead { get; set; }

    /// <summary>La réanimation unique a-t-elle déjà été consommée ?</summary>
    public bool ResurrectionUsed { get; set; }
    public bool IsVictory { get; set; }

    public void Reset(string startRoom)
    {
        CurrentRoomId = startRoom;
        Inventory.Clear();
        Flags.Clear();
        Counters.Clear();
        Score = 0;
        IsDead = false;
        IsVictory = false;
        ResurrectionUsed = false;
    }
}
