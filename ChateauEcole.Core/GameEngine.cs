using ChateauEcole.Core.MiniGames;
using ChateauEcole.Core.Models;

namespace ChateauEcole.Core;

/// <summary>
/// Le moteur : une boucle unique qui lit la salle courante dans les données.
/// Checkpoint automatique à chaque changement de salle ; après une mort,
/// le joueur peut reprendre à la dernière sauvegarde.
/// </summary>
public class GameEngine
{
    private readonly WorldData _world;
    private readonly IGameIO _io;
    private readonly SaveService _save;
    private readonly Dictionary<string, Action<GameEngine, IGameIO>> _specialActions;

    public GameState State { get; private set; }

    public GameEngine(WorldData world, IGameIO io, SaveService save)
    {
        _world = world;
        _io = io;
        _save = save;
        State = new GameState();

        // Les mini-jeux et scènes scriptées sont enregistrés ici, par id.
        // world.json y fait référence via "specialActionId".
        _specialActions = new Dictionary<string, Action<GameEngine, IGameIO>>
        {
            ["chifoumi_bernard"] = ChifoumiBernard.Jouer,
            ["prof_sport"] = ProfDeSport.Jouer,
            ["choix_cantine"] = CantineChoix.Jouer
        };
    }

    /// <summary>Remplace {NOM} par le prénom du joueur dans les textes du monde.</summary>
    private string T(string text) => text.Replace("{NOM}", State.PlayerName);

    // ------------------------------------------------------------------
    // Boucle principale
    // ------------------------------------------------------------------

    public void Run()
    {
        _io.Clear();
        _io.WriteLine("==========================================");
        _io.WriteLine("        LE LYCÉE SAINT-LÉGER");
        _io.WriteLine("  Personne ne devrait rester ici la nuit.");
        _io.WriteLine("==========================================");
        ShowHighScores();

        NewGame();

        bool continuer = true;
        while (continuer)
        {
            PlayOnce();
            continuer = HandleGameEnd();
        }

        _io.WriteLine();
        _io.WriteLine(T("À bientôt, {NOM}... si tu oses revenir."));
    }

    private void NewGame()
    {
        State = new GameState();
        State.Reset(_world.StartRoom);

        string nom = _io.AskText("Quel est ton prénom ?").Trim();
        State.PlayerName = string.IsNullOrWhiteSpace(nom) ? "Bichette" : nom;

        _io.WriteLine();
        _io.WriteLine(T("« {NOM} »... c'est noté. Quelque part, quelque chose vient de le noter aussi."));
        _io.WriteLine();
        _io.WriteLine("* Règle du lycée : si tu meurs, une UNIQUE réanimation te sera proposée.");
        _io.WriteLine("* Elle te ramènera à l'entrée de la dernière salle... contre UN QUART de tes points.");
        _save.SaveCheckpoint(State);
    }

    private void PlayOnce()
    {
        while (!State.IsDead && !State.IsVictory)
        {
            Room room = GetRoom(State.CurrentRoomId);
            ShowRoom(room);
            DoTurn(room);
        }
    }

    /// <summary>Gère la fin d'une partie. Retourne false pour quitter le jeu.</summary>
    private bool HandleGameEnd()
    {
        if (State.IsVictory)
        {
            _save.DeleteCheckpoint();
            _save.AddHighScore(State.PlayerName, State.Score, victory: true);
            ShowHighScores();
            int c = _io.AskChoice("Voulez-vous rejouer ?", new List<string> { "Oui", "Non" });
            if (c == 0) { NewGame(); return true; }
            return false;
        }

        // Mort
        bool canResurrect = !State.ResurrectionUsed && _save.HasCheckpoint();
        var options = new List<string>();
        if (canResurrect)
            options.Add("Être réanimée (UNE SEULE FOIS par partie — coûte 1/4 de tes points acquis)");
        options.Add("Recommencer une nouvelle partie");
        options.Add("Quitter");

        string question = canResurrect
            ? "Dans le noir, quelque chose te propose un marché... Que veux-tu faire ?"
            : "Cette fois, rien ne te propose de marché. La mort est définitive. Que veux-tu faire ?";
        int choix = _io.AskChoice(question, options);

        if (canResurrect && choix == 0)
        {
            GameState restored = _save.LoadCheckpoint()!;
            restored.ResurrectionUsed = true;
            int malus = restored.Score > 0 ? restored.Score / 4 : 0;
            restored.Score -= malus;
            State = restored;
            _save.SaveCheckpoint(State);

            _io.WriteLine();
            _io.WriteLine("Le temps se rembobine dans un grincement de craie...");
            _io.WriteLine($"* La réanimation te coûte {malus} points. Rien n'est gratuit, ici.");
            _io.WriteLine("* La sauvegarde s'est consumée dans l'opération : il n'y en aura PAS d'autre.");
            _io.WriteLine(T("Te revoilà, {NOM}. Ne gaspille pas cette faveur."));
            return true;
        }

        // La partie s'arrête vraiment : le score entre au tableau
        _save.AddHighScore(State.PlayerName, State.Score, victory: false);
        ShowHighScores();
        _save.DeleteCheckpoint();

        int offsetRecommencer = canResurrect ? 1 : 0;
        if (choix == offsetRecommencer)
        {
            NewGame();
            return true;
        }
        return false;
    }

    private void ShowHighScores()
    {
        var scores = _save.LoadHighScores();
        if (scores.Count == 0) return;
        _io.WriteLine();
        _io.WriteLine("--- Meilleurs scores ---");
        int rang = 1;
        foreach (var s in scores.Take(5))
        {
            string marque = s.Victory ? "[ÉVADÉ]" : "[disparu]";
            _io.WriteLine($"  {rang}. {s.Name} — {s.Score} pts {marque}");
            rang++;
        }
    }

    // ------------------------------------------------------------------
    // Tour de jeu
    // ------------------------------------------------------------------

    private Room GetRoom(string id) =>
        _world.Rooms.FirstOrDefault(r => r.Id == id)
        ?? throw new InvalidOperationException($"Salle inconnue dans world.json : '{id}'");

    private void ShowRoom(Room room)
    {
        _io.WriteLine();
        _io.WriteLine($"=== {room.Name} ===");
        string visitedFlag = "visited_" + room.Id;
        if (!State.Flags.Contains(visitedFlag))
        {
            _io.WriteLine(T(room.Description));
            State.Flags.Add(visitedFlag);
        }
        else
        {
            // La salle a-t-elle changé depuis ? (prof vaincu, chose partie...)
            var etat = room.StateTexts.FirstOrDefault(st => State.Flags.Contains(st.Flag));
            if (etat != null)
                _io.WriteLine(T(etat.Text));
        }
    }

    private void DoTurn(Room room)
    {
        var choices = new List<(string Label, Action Act)>();

        foreach (Exit exit in room.Exits)
        {
            if (exit.HiddenUntilFlag != null && !State.Flags.Contains(exit.HiddenUntilFlag))
                continue; // passage secret pas encore découvert

            Exit e = exit; // capture locale pour la lambda
            choices.Add((e.Label, () => TryExit(e)));
        }

        if (room.Examine != null)
            choices.Add(("Examiner", () => DoExamine(room)));

        if (room.SpecialActionId != null &&
            _specialActions.TryGetValue(room.SpecialActionId, out var action))
        {
            choices.Add((room.SpecialActionLabel ?? "Action spéciale", () => action(this, _io)));
        }

        choices.Add(("Inventaire", ShowInventory));

        int idx = _io.AskChoice(
            "Que voulez-vous faire ?",
            choices.Select(c => c.Label).ToList(),
            onInvalid: () => State.Score -= 2);

        _io.WriteLine();
        choices[idx].Act();
        CheckPhoneAndSms();
    }

    /// <summary>
    /// Après chaque action : allumage du téléphone (téléphone cassé + chargeur),
    /// puis envoi d'au plus UN SMS de « R. » dont les conditions sont réunies.
    /// </summary>
    private void CheckPhoneAndSms()
    {
        if (State.IsDead || State.IsVictory) return;

        if (!State.Flags.Contains("telephone_charge"))
        {
            if (State.Inventory.Contains("telephone_casse") && State.Inventory.Contains("chargeur"))
            {
                State.Flags.Add("telephone_charge");
                _io.WriteLine();
                _io.WriteLine("* Tu branches le chargeur sur une prise murale. L'écran fissuré s'allume en grésillant.");
                _io.WriteLine("* Ton téléphone est de nouveau en vie. Enfin... « en vie ».");
            }
            return;
        }

        foreach (var sms in _world.Sms)
        {
            string flag = "sms_" + sms.Id;
            if (State.Flags.Contains(flag)) continue;
            if (sms.RequiredItems.Any(i => !State.Inventory.Contains(i))) continue;
            if (sms.RequiredFlags.Any(f => !State.Flags.Contains(f))) continue;

            State.Flags.Add(flag);
            _io.WriteLine();
            _io.WriteLine("* Ton téléphone vibre.");
            _io.WriteLine(T("* " + sms.Text));
            break; // un seul SMS par tour, pour le rythme
        }
    }

    /// <summary>Déplace le joueur (utilisé par les mini-jeux, ex. éjection du gymnase).</summary>
    public void TeleportTo(string roomId)
    {
        State.CurrentRoomId = roomId;
        _save.SaveCheckpoint(State);
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    private void TryExit(Exit exit)
    {
        bool manqueObjet = exit.RequiredItems.Any(i => !State.Inventory.Contains(i));
        bool manqueFlag = exit.RequiredFlags.Any(f => !State.Flags.Contains(f));

        if (manqueObjet || manqueFlag)
        {
            _io.WriteLine(T(exit.LockedMessage ?? "C'est fermé..."));
            return;
        }

        if (exit.ConsumeRequiredItems)
        {
            foreach (string item in exit.RequiredItems)
            {
                State.Inventory.Remove(item);
                _io.WriteLine($"Vous utilisez : {ItemName(item)}");
            }
        }

        if (exit.TransitionText != null)
            _io.WriteLine(T(exit.TransitionText));

        if (exit.Target == "VICTOIRE")
        {
            Win();
            return;
        }

        State.CurrentRoomId = exit.Target;
        _save.SaveCheckpoint(State); // checkpoint automatique à chaque changement de salle
    }

    private void DoExamine(Room room)
    {
        Interaction ex = room.Examine!;
        string examinedFlag = "examined_" + room.Id;
        bool dejaFouille = State.Flags.Contains(examinedFlag);

        // Objet requis manquant : mortel (toilettes sans masque) ou simple refus (6e sans balles)
        if (ex.RequiredItem != null && !State.Inventory.Contains(ex.RequiredItem))
        {
            if (ex.DeathWithoutItemMessage != null)
            {
                Die(ex.DeathWithoutItemMessage);
                return;
            }
            if (ex.BlockedWithoutItemMessage != null)
            {
                _io.WriteLine(T(ex.BlockedWithoutItemMessage));
                return;
            }
        }

        // Fouiller deux fois peut être fatal (mourir d'ennui en Latin...)
        if (dejaFouille && ex.DeadlyOnRepeatMessage != null)
        {
            Die(ex.DeadlyOnRepeatMessage);
            return;
        }

        _io.WriteLine(T(dejaFouille && ex.RepeatText != null ? ex.RepeatText : ex.Text));

        if (!dejaFouille)
        {
            if (ex.ConsumeRequiredItem && ex.RequiredItem != null && State.Inventory.Remove(ex.RequiredItem))
                _io.WriteLine($"Vous utilisez : {ItemName(ex.RequiredItem)}");
            foreach (string item in ex.GrantsItems)
                AddItem(item);
            if (ex.SetsFlag != null)
                State.Flags.Add(ex.SetsFlag);
        }

        State.Flags.Add(examinedFlag);
    }

    public void AddItem(string itemId)
    {
        if (State.Inventory.Contains(itemId))
        {
            _io.WriteLine($"{ItemName(itemId)} est déjà dans votre inventaire.");
            return;
        }
        if (State.Inventory.Count >= State.MaxInventory)
        {
            _io.WriteLine("Votre sac est plein ! Impossible de prendre cet objet.");
            return;
        }
        State.Inventory.Add(itemId);
        State.Score += 5;
        _io.WriteLine($"» {ItemName(itemId)} ajouté à votre inventaire (+5 pts)");
    }

    public string ItemName(string id) =>
        _world.Items.TryGetValue(id, out var name) ? name : id;

    private void ShowInventory()
    {
        if (State.Inventory.Count == 0)
        {
            _io.WriteLine("Votre inventaire est vide.");
        }
        else
        {
            _io.WriteLine("Vous avez dans votre inventaire :");
            foreach (string item in State.Inventory)
                _io.WriteLine($"  - {ItemName(item)}");
        }
        _io.WriteLine($"Score actuel : {State.Score} pts");
    }

    // ------------------------------------------------------------------
    // Fins de partie
    // ------------------------------------------------------------------

    public void Die(string message)
    {
        _io.WriteLine(T(message));
        _io.WriteLine();
        _io.WriteLine("* Le lycée devient très silencieux.");
        _io.WriteLine(T("* Quelle négligence, {NOM}..."));
        _io.WriteLine($"* Ton score : {State.Score} pts");
        State.IsDead = true;
    }

    private void Win()
    {
        State.Score += 20;
        _io.WriteLine();
        _io.WriteLine("=== FIN DE L'AVENTURE — ÉVASION RÉUSSIE ! ===");
        _io.WriteLine($"Ton score : {State.Score} pts");
        State.IsVictory = true;
    }
}
