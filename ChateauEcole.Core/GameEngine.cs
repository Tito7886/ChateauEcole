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
            ["choix_cantine"] = CantineChoix.Jouer,
            ["grenouille_sciences"] = GrenouilleSciences.Jouer,
            ["rituel_orgue"] = RituelOrgue.Jouer
        };
    }

    /// <summary>Remplace {NOM} par le prénom du joueur dans les textes du monde.</summary>
    public string T(string text) => text.Replace("{NOM}", State.PlayerName);

    // ------------------------------------------------------------------
    // Boucle principale
    // ------------------------------------------------------------------

    public void Run()
    {
        _io.Clear();
        _io.WriteLine("==========================================");
        _io.WriteLine("        LE LYCÉE NOTRE-DAME");
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
            string marque = State.VictoryImmoral ? "[À QUEL PRIX]" : "[ÉVADÉ]";
            _save.AddHighScore(State.PlayerName, State.Score, victory: true, mark: marque);
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
            string marque = s.Victory ? (s.Mark ?? "[ÉVADÉ]") : "[disparu]";
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

        // Compagnon : présence discrète affichée par le moteur (pas en dur dans une salle).
        if (State.LapinouSuit)
            _io.WriteLine("* Lapinou trottine à tes côtés, truffe frémissante.");
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

        foreach (RoomAction roomAction in room.Actions)
        {
            if (roomAction.RequiredFlags.Any(f => !State.Flags.Contains(f))) continue;
            if (roomAction.RequiredFlagsAbsent.Any(f => State.Flags.Contains(f))) continue;
            if (roomAction.RequiredItem != null && !State.Inventory.Contains(roomAction.RequiredItem)) continue;
            if (!roomAction.Repeatable && State.Flags.Contains(ActionDoneFlag(room, roomAction))) continue;

            RoomAction a = roomAction; // capture locale
            choices.Add((a.Label, () => DoAction(room, a)));
        }

        // Serrure à combinaison (tant qu'elle n'est pas ouverte) : composer / récapituler.
        if (room.CodeLock != null && !State.Flags.Contains(room.CodeLock.SetsFlag))
        {
            CodeLock cl = room.CodeLock;
            choices.Add((cl.Label, () => DoComposeCode(cl)));
            choices.Add((cl.RecapLabel, () => DoRecapCode(cl)));
        }

        if (room.Examine != null)
            choices.Add(("Examiner", () => DoExamine(room)));

        bool specialDébloquée = room.SpecialActionRequiredFlag == null
                                || State.Flags.Contains(room.SpecialActionRequiredFlag);
        if (room.SpecialActionId != null && specialDébloquée &&
            _specialActions.TryGetValue(room.SpecialActionId, out var action))
        {
            choices.Add((room.SpecialActionLabel ?? "Action spéciale", () => action(this, _io)));
        }

        if (State.FloorItems.TryGetValue(room.Id, out var atSol) && atSol.Count > 0)
            choices.Add(("Ramasser (objets au sol)", () => DoPickUp(room)));

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
        // Passage déjà « acquis » (ex. surveillant amadoué) : conditions et consommation ignorées.
        bool bypass = exit.BypassIfFlag != null && State.Flags.Contains(exit.BypassIfFlag);

        if (!bypass)
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
        }

        if (exit.SetsFlag != null)
            State.Flags.Add(exit.SetsFlag);

        string? transition = bypass ? (exit.BypassTransitionText ?? exit.TransitionText) : exit.TransitionText;
        if (transition != null)
            _io.WriteLine(T(transition));

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
                // 1er refus = avertissement ; insister sans l'objet (2e essai) peut être fatal.
                string blockedFlag = "blocked_" + room.Id;
                if (State.Flags.Contains(blockedFlag) && ex.DeadlyOnRepeatWithoutItemMessage != null)
                {
                    Die(ex.DeadlyOnRepeatWithoutItemMessage);
                    return;
                }
                State.Flags.Add(blockedFlag);
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

    /// <summary>
    /// Ajoute un objet à l'inventaire s'il reste de la place. API simple utilisée par
    /// les mini-jeux : refuse si le sac est plein (pas d'échange). Pour un ajout qui
    /// propose un échange en cas de sac plein, voir AcquireOrSwap.
    /// </summary>
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
        PutInInventory(itemId);
    }

    /// <summary>
    /// Pose réellement l'objet dans l'inventaire. Le bonus de +5 n'est accordé QU'À la
    /// toute première acquisition de l'objet dans la partie (flag « got_&lt;id&gt; ») :
    /// lâcher puis reprendre un objet ne rapporte donc jamais de points en double.
    /// </summary>
    private void PutInInventory(string itemId)
    {
        State.Inventory.Add(itemId);
        if (State.Flags.Add("got_" + itemId))
        {
            State.Score += 5;
            _io.WriteLine($"» {ItemName(itemId)} ajouté à votre inventaire (+5 pts)");
        }
        else
        {
            _io.WriteLine($"» {ItemName(itemId)} récupéré.");
        }
    }

    /// <summary>
    /// Tente d'acquérir un objet. Si le sac est plein, propose de lâcher un objet
    /// (qui tombe alors au sol de la salle courante, récupérable plus tard) ou de
    /// renoncer. Retourne true si l'objet a fini dans l'inventaire.
    /// </summary>
    private bool AcquireOrSwap(string itemId)
    {
        if (State.Inventory.Contains(itemId)) return true;
        if (State.Inventory.Count < State.MaxInventory)
        {
            PutInInventory(itemId);
            return true;
        }

        _io.WriteLine($"Ton sac est plein ({State.MaxInventory} objets). Pour prendre {ItemName(itemId)}, il faut laisser quelque chose ici.");
        var options = State.Inventory.Select(i => $"Laisser au sol : {ItemName(i)}").ToList();
        options.Add($"Renoncer à {ItemName(itemId)} pour l'instant");

        int c = _io.AskChoice("Que veux-tu laisser ?", options);
        if (c >= State.Inventory.Count) return false; // a renoncé

        string dropped = State.Inventory[c];
        State.Inventory.RemoveAt(c);
        DropToFloor(State.CurrentRoomId, dropped);
        _io.WriteLine($"Tu poses {ItemName(dropped)} au sol. Il t'attendra ici.");
        PutInInventory(itemId);
        return true;
    }

    /// <summary>Ajoute un objet aux objets au sol d'une salle (sans doublon).</summary>
    private void DropToFloor(string roomId, string itemId)
    {
        if (!State.FloorItems.TryGetValue(roomId, out var list))
        {
            list = new List<string>();
            State.FloorItems[roomId] = list;
        }
        if (!list.Contains(itemId))
            list.Add(itemId);
    }

    private static string ActionDoneFlag(Room room, RoomAction action) => $"did_{room.Id}_{action.Id}";

    /// <summary>Exécute une action de salle générique (fouiller, pousser, proposer un objet...).</summary>
    private void DoAction(Room room, RoomAction action)
    {
        bool dejaFait = State.Flags.Contains(ActionDoneFlag(room, action));
        _io.WriteLine(T(dejaFait && action.RepeatText != null ? action.RepeatText : action.Text));

        if (action.OffersItem != null)
            OfferItem(action.OffersItem);

        foreach (string item in action.GrantsItems)
            if (!AcquireOrSwap(item))
                DropToFloor(State.CurrentRoomId, item); // jamais perdu : au sol par défaut

        foreach (string item in action.DropsToFloor)
            DropToFloor(State.CurrentRoomId, item);

        if (action.ConsumesItem && action.RequiredItem != null && State.Inventory.Remove(action.RequiredItem))
            _io.WriteLine($"Vous utilisez : {ItemName(action.RequiredItem)}");

        if (action.SetsCompanion)
            State.LapinouSuit = true;

        if (action.SetsFlag != null)
            State.Flags.Add(action.SetsFlag);

        if (!action.Repeatable)
            State.Flags.Add(ActionDoneFlag(room, action));
    }

    // ------------------------------------------------------------------
    // Serrure à code (générique, data-driven)
    // ------------------------------------------------------------------

    private void DoComposeCode(CodeLock cl)
    {
        string saisie = _io.AskText(cl.Prompt).Trim();
        if (saisie == cl.Combination)
        {
            State.Flags.Add(cl.SetsFlag);
            _io.WriteLine(T(cl.SuccessText));
        }
        else
        {
            _io.WriteLine(T(cl.FailText));
        }
    }

    private void DoRecapCode(CodeLock cl)
    {
        var trouves = cl.Fragments.Where(f => State.Flags.Contains(f.Flag)).ToList();
        if (trouves.Count == 0)
        {
            _io.WriteLine(T(cl.EmptyRecapText));
            return;
        }
        _io.WriteLine(T(cl.RecapHeader));
        foreach (var f in trouves)
            _io.WriteLine("  - " + T(f.Text));
    }

    /// <summary>Propose au joueur de prendre un objet ou de le laisser au sol de la salle.</summary>
    private void OfferItem(string itemId)
    {
        int c = _io.AskChoice(
            $"Que fais-tu de {ItemName(itemId)} ?",
            new List<string> { "Le prendre", "Le laisser au sol" });

        if (c == 1)
        {
            DropToFloor(State.CurrentRoomId, itemId);
            _io.WriteLine($"Tu laisses {ItemName(itemId)} là, par terre. Il t'attendra, si tu changes d'avis.");
            return;
        }

        if (!AcquireOrSwap(itemId))
        {
            DropToFloor(State.CurrentRoomId, itemId);
            _io.WriteLine($"Finalement, tu n'emportes pas {ItemName(itemId)}. Il reste au sol, à portée de main.");
        }
    }

    /// <summary>Ramasse un objet posé au sol de la salle courante (échange si le sac est plein).</summary>
    private void DoPickUp(Room room)
    {
        if (!State.FloorItems.TryGetValue(room.Id, out var floor) || floor.Count == 0)
        {
            _io.WriteLine("Il n'y a rien à ramasser ici.");
            return;
        }

        var options = floor.Select(ItemName).ToList();
        options.Add("Ne rien ramasser");

        int c = _io.AskChoice("Au sol, tu peux ramasser :", options);
        if (c >= floor.Count) return;

        string itemId = floor[c];
        if (AcquireOrSwap(itemId))
        {
            floor.Remove(itemId);
            if (floor.Count == 0)
                State.FloorItems.Remove(room.Id);
        }
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

    private void Win() => EndJuste();

    /// <summary>Fin JUSTE (l'Hymne) : score plein, marque [ÉVADÉ]. L'épilogue narratif est
    /// affiché par l'appelant (ex. RituelOrgue) ; ici on finalise l'état et le score.</summary>
    public void EndJuste()
    {
        State.Score += 20;
        _io.WriteLine();
        _io.WriteLine("=== FIN DE L'AVENTURE — ÉVASION RÉUSSIE ! ===");
        _io.WriteLine($"Ton score : {State.Score} pts");
        State.IsVictory = true;
        State.VictoryImmoral = false;
    }

    /// <summary>Fin IMMORALE (le Sacrifice) : sortie obtenue au prix de Lapinou, marque [À QUEL PRIX].</summary>
    public void EndImmoral()
    {
        State.Score += 5;
        _io.WriteLine();
        _io.WriteLine("=== FIN DE L'AVENTURE — LA PORTE S'OUVRE. À QUEL PRIX. ===");
        _io.WriteLine($"Ton score : {State.Score} pts");
        State.IsVictory = true;
        State.VictoryImmoral = true;
    }
}
