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
    private readonly IScoreBoard? _online; // classement en ligne (optionnel, best-effort)
    private readonly Dictionary<string, Action<GameEngine, IGameIO>> _specialActions;

    public GameState State { get; private set; }

    public GameEngine(WorldData world, IGameIO io, SaveService save, IScoreBoard? online = null)
    {
        _world = world;
        _io = io;
        _save = save;
        _online = online;
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

    /// <summary>
    /// Résout les placeholders des textes du monde : {NOM} = prénom du joueur ;
    /// {CODE1}/{CODE2}/{CODE3} = chiffres de la serrure du sous-sol tirés au sort à chaque
    /// partie ; {CODE} = la combinaison complète (utilisée par la serrure elle-même).
    /// </summary>
    public string T(string text)
    {
        text = text.Replace("{NOM}", State.PlayerName);
        if (State.CodeCombination.Length == 4)
        {
            text = text
                .Replace("{CODE1}", State.CodeCombination[0].ToString())
                .Replace("{CODE2}", State.CodeCombination[1].ToString())
                .Replace("{CODE3}", State.CodeCombination[2].ToString())
                .Replace("{CODE4}", State.CodeCombination[3].ToString())
                .Replace("{CODE}", State.CodeCombination);
        }
        return text;
    }

    /// <summary>
    /// Affiche un texte de CONTENU (world.json) en interprétant les marqueurs d'effet
    /// data-driven : [clear] efface l'écran avant, [slow] = machine à écrire, [pause] = pause
    /// après. Les balises couleur ([rouge]...[/rouge]) restent dans le texte et sont rendues
    /// par l'IO. Le Core reste agnostique : il n'appelle que des méthodes de IGameIO.
    /// </summary>
    private void Emit(string raw)
    {
        string text = T(raw);
        // Marqueurs STRUCTURELS (actions du moteur) : [clear] efface avant, [pause] attend après.
        bool clear = text.Contains("[clear]");
        bool pause = text.Contains("[pause]");
        if (clear) text = text.Replace("[clear]", "");
        if (pause) text = text.Replace("[pause]", "");
        text = text.Trim();

        if (clear) _io.Clear();
        // Les balises de RENDU restantes ([rouge]..., [slow]/[slow=NN]/[/slow]) sont interprétées
        // par l'IO : WriteLine anime les régions [slow] et affiche le reste normalement.
        _io.WriteLine(text);
        if (pause) _io.Pause();
    }

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
        _io.WriteLine(T("À bientôt, {NOM}... "));
    }

    private void NewGame()
    {
        State = new GameState();
        State.Reset(_world.StartRoom);
        _titledRoomId = null; // forcer l'écran-titre de la salle de départ

        // Code de la serrure du sous-sol : tiré au sort à chaque partie (4 chiffres).
        State.CodeCombination = $"{Random.Shared.Next(10)}{Random.Shared.Next(10)}{Random.Shared.Next(10)}{Random.Shared.Next(10)}";

        // Objets au sol initiaux (déclarés en JSON) : semés une seule fois, puis sérialisés.
        foreach (Room room in _world.Rooms)
            foreach (string item in room.FloorItems)
                DropToFloor(room.Id, item);

        string nom = _io.AskText("Quel est ton prénom ? (20 caractères max)").Trim();
        const int maxNom = 20;
        if (nom.Length > maxNom) nom = nom.Substring(0, maxNom).Trim(); // évite qu'un copier-coller géant devienne le pseudo
        State.PlayerName = string.IsNullOrWhiteSpace(nom) ? "Bichette" : nom;

        _io.WriteLine();
        _io.WriteSlow(T("« {NOM} »... c'est noté. Quelque part, quelque chose vient de le noter aussi."));
        _io.WriteLine();
        _io.WriteLine("* Règle du lycée : si tu meurs, une UNIQUE réanimation te sera proposée.");
        _io.WriteLine("* Elle te ramènera à l'entrée de la dernière salle... contre UN QUART de tes points.");
        _io.Pause(); // lire l'intro/les règles avant que l'écran-titre de la 1re salle n'efface l'écran
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
            PublishOnline(marque);
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
            _titledRoomId = null; // réafficher l'écran-titre de la salle où l'on ressuscite
            _save.SaveCheckpoint(State);

            _io.WriteLine();
            _io.WriteLine("Le temps se rembobine dans un grincement de craie...");
            _io.WriteLine($"* La réanimation te coûte {malus} points. Rien n'est gratuit, ici.");
            _io.WriteLine("* La sauvegarde s'est consumée dans l'opération : il n'y en aura PAS d'autre.");
            _io.WriteLine(T("Te revoilà, {NOM}. Ne gaspille pas cette faveur."));
            _io.Pause(); // lire avant que l'écran-titre de la salle de réanimation n'efface l'écran
            return true;
        }

        // La partie s'arrête vraiment : le score entre au tableau
        _save.AddHighScore(State.PlayerName, State.Score, victory: false);
        PublishOnline("[MORT]");
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
        // Classement en ligne d'abord (best-effort) ; repli sur le local si vide/indisponible.
        List<HighScore> scores = _online?.GetTop(10) ?? new List<HighScore>();
        bool enLigne = scores.Count > 0;
        if (!enLigne) scores = _save.LoadHighScores();
        if (scores.Count == 0) return;

        _io.WriteLine();
        _io.WriteLine(enLigne ? "--- Classement en ligne ---" : "--- Meilleurs scores (local) ---");
        int rang = 1;
        foreach (var s in scores.Take(5))
        {
            string marque = !string.IsNullOrEmpty(s.Mark) ? s.Mark! : (s.Victory ? "[ÉVADÉ]" : "[MORT]");
            // Date + heure locale du joueur (ex. « 18/09 04:12 »), quand elle est connue.
            string quand = s.Date > DateTime.MinValue ? $" · {s.Date:dd/MM HH:mm}" : "";
            _io.WriteLine($"  {rang}. {s.Name} — {s.Score} pts {marque}{quand}");
            rang++;
        }
    }

    /// <summary>Publie le score de fin de partie en ligne (best-effort, silencieux si KO).</summary>
    private void PublishOnline(string mark)
    {
        if (_online == null) return;
        try
        {
            _online.Submit(new HighScore
            {
                Name = State.PlayerName,
                Score = State.Score,
                Victory = State.IsVictory,
                Mark = mark,
                Date = DateTime.Now
            });
        }
        catch { /* le classement en ligne ne doit jamais gêner la partie */ }
    }

    // ------------------------------------------------------------------
    // Tour de jeu
    // ------------------------------------------------------------------

    private Room GetRoom(string id) =>
        _world.Rooms.FirstOrDefault(r => r.Id == id)
        ?? throw new InvalidOperationException($"Salle inconnue dans world.json : '{id}'");

    // Dernière salle dont l'écran-titre a été affiché : l'écran-titre (et son Clear) ne se
    // déclenche qu'à l'ENTRÉE d'une salle, pas à chaque tour — sinon le Clear effacerait la
    // sortie de l'action que le joueur vient de faire dans la même salle.
    private string? _titledRoomId;

    private void ShowRoom(Room room)
    {
        bool nouvelleEntree = _titledRoomId != room.Id;
        if (nouvelleEntree)
        {
            _io.ShowRoomTitle(room.Name, room.TitleArt); // efface l'écran + affiche l'écran-titre
            _titledRoomId = room.Id;
        }
        else
        {
            _io.WriteLine();
        }

        string visitedFlag = "visited_" + room.Id;
        if (!State.Flags.Contains(visitedFlag))
        {
            Emit(room.Description);
            State.Flags.Add(visitedFlag);
        }
        else if (nouvelleEntree)
        {
            // La salle a-t-elle changé depuis ? (prof vaincu, chose partie...)
            var etat = room.StateTexts.FirstOrDefault(st => State.Flags.Contains(st.Flag));
            if (etat != null)
                Emit(etat.Text);
        }

        // Compagnon : présence affichée à l'entrée, avec une réplique propre à la salle si définie.
        if (nouvelleEntree && State.LapinouSuit)
            _io.WriteLine("* " + T(room.CompanionText ?? "Lapinou trottine à tes côtés, truffe frémissante."));
    }

    private void DoTurn(Room room)
    {
        var choices = new List<(string Label, Action Act)>();

        foreach (Exit exit in room.Exits)
        {
            if (exit.HiddenUntilFlag != null && !State.Flags.Contains(exit.HiddenUntilFlag))
                continue; // passage secret pas encore découvert
            if (exit.HiddenIfFlag != null && State.Flags.Contains(exit.HiddenIfFlag))
                continue; // issue condamnée (point de non-retour)

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

        // Charge du téléphone : choix intégré disponible DANS TOUTE SALLE dès qu'on a le
        // téléphone + le chargeur et qu'il n'est pas déjà chargé (anti-softlock : peu importe
        // où l'on a trouvé le chargeur, on peut toujours lancer l'animation de charge).
        if (State.Inventory.Contains("telephone_casse") && State.Inventory.Contains("chargeur")
            && !State.Flags.Contains("telephone_charge"))
            choices.Add(("Brancher le téléphone pour le charger", () => ChargeTelephone.Jouer(this, _io)));

        if (State.Inventory.Count > 0)
            choices.Add(("Combiner / utiliser un objet", () => DoCombine(room)));

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
    /// Après chaque action : envoi d'au plus UN SMS de « N. » dont les conditions sont réunies.
    /// Le téléphone ne s'allume PLUS automatiquement : il faut le charger via l'action
    /// « Brancher le téléphone pour le charger » (téléphone + chargeur), ce qui pose le flag
    /// « telephone_charge ». Chaque SMS a un rituel animé : vibration en machine à écrire (verte)
    /// puis le message en vert. Dégradation propre en sortie redirigée (WriteSlow instantané,
    /// balises retirées).
    /// </summary>
    private void CheckPhoneAndSms()
    {
        if (State.IsDead || State.IsVictory) return;
        if (!State.Flags.Contains("telephone_charge")) return; // téléphone pas encore chargé

        foreach (var sms in _world.Sms)
        {
            string flag = "sms_" + sms.Id;
            if (State.Flags.Contains(flag)) continue;
            if (sms.RequiredItems.Any(i => !State.Inventory.Contains(i))) continue;
            if (sms.RequiredFlags.Any(f => !State.Flags.Contains(f))) continue;

            State.Flags.Add(flag);
            _io.WriteLine();
            _io.WriteSlow("[vert]Bzzt. Bzzt.[/vert]", 120); // la vibration, en machine à écrire verte
            _io.WriteLine("[vert]" + T(sms.Text) + "[/vert]"); // le message (déjà signé « — N. »)
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
            // Serrure à code : la saisie n'est proposée qu'ici, quand on tente vraiment la sortie.
            if (exit.CodeLock != null && !State.Flags.Contains(exit.CodeLock.SetsFlag))
            {
                if (!TryEnterCode(exit)) return; // renoncé ou mauvais code : on reste
                // bon code : le flag SetsFlag est posé, on poursuit vers l'ouverture normale
            }

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
            Emit(transition);

        if (exit.Target == "VICTOIRE")
        {
            Win();
            return;
        }

        // La salle suivante commence par un Clear (écran-titre) : laisser lire le texte de
        // transition (ou le message d'objet consommé) avant l'effacement. Pas de double pause
        // si la transition portait déjà son propre [pause].
        bool changeSalle = exit.Target != State.CurrentRoomId;
        bool aAffiché = transition != null || (exit.ConsumeRequiredItems && exit.RequiredItems.Count > 0);
        if (changeSalle && aAffiché && !(transition?.Contains("[pause]") ?? false))
            _io.Pause();

        State.CurrentRoomId = exit.Target;
        _save.SaveCheckpoint(State); // checkpoint automatique à chaque changement de salle
    }

    private void DoExamine(Room room)
    {
        Interaction ex = room.Examine!;
        string examinedFlag = "examined_" + room.Id;
        bool dejaFouille = State.Flags.Contains(examinedFlag);

        // Objet requis manquant : mortel (toilettes sans masque) ou simple refus (6e sans radis)
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

        Emit(dejaFouille && ex.RepeatText != null ? ex.RepeatText : ex.Text);

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
        Emit(dejaFait && action.RepeatText != null ? action.RepeatText : action.Text);

        // Action mortelle (ex. offrir la laitue à Lapinou) : rien n'est accordé, le joueur meurt.
        if (action.DeadlyMessage != null)
        {
            Die(action.DeadlyMessage);
            return;
        }

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
    // Combinaisons d'objets (objet+objet, objet+décor) — data-driven
    // ------------------------------------------------------------------

    /// <summary>Action « Combiner » : choisir un objet, puis une cible (objet ou décor).</summary>
    private void DoCombine(Room room)
    {
        if (State.Inventory.Count == 0)
        {
            _io.WriteLine("Tu n'as rien à combiner.");
            return;
        }

        // 1) Choisir le premier ingrédient (un objet de l'inventaire).
        var invOptions = State.Inventory.Select(ItemName).ToList();
        invOptions.Add("Annuler");
        int a = _io.AskChoice("Utiliser quel objet ?", invOptions);
        if (a >= State.Inventory.Count) return;
        string itemA = State.Inventory[a];

        // 2) Choisir la cible : autre objet de l'inventaire OU élément de décor de la salle.
        var targets = new List<(string Label, string Id, bool Decor)>();
        foreach (string it in State.Inventory)
            if (it != itemA) targets.Add(($"Objet : {ItemName(it)}", it, false));
        foreach (DecorTarget d in room.DecorTargets)
            targets.Add((d.Label, d.Id, true));

        if (targets.Count == 0)
        {
            _io.WriteLine("Il n'y a rien ici sur quoi l'utiliser.");
            return;
        }

        var targetOptions = targets.Select(t => t.Label).ToList();
        targetOptions.Add("Annuler");
        int b = _io.AskChoice($"Utiliser {ItemName(itemA)} sur quoi ?", targetOptions);
        if (b >= targets.Count) return;
        var target = targets[b];

        // 3) Chercher une recette (ordre indifférent) et l'appliquer.
        Combination? recipe = _world.Combinations.FirstOrDefault(c =>
            (c.ItemA == itemA && c.ItemB == target.Id) ||
            (c.ItemA == target.Id && c.ItemB == itemA));

        if (recipe == null)
        {
            _io.WriteLine("Ça ne donne rien. (Ou alors ce n'est pas le bon geste.)");
            return;
        }

        ApplyCombination(recipe, room, target.Decor);
    }

    private void ApplyCombination(Combination recipe, Room room, bool targetIsDecor)
    {
        // Clé « déjà fait » : par salle pour les combos décor (ex. craie sur CHAQUE tableau),
        // globale pour les combos objet+objet.
        string pair = string.CompareOrdinal(recipe.ItemA, recipe.ItemB) <= 0
            ? $"{recipe.ItemA}+{recipe.ItemB}" : $"{recipe.ItemB}+{recipe.ItemA}";
        string doneFlag = targetIsDecor ? $"combo:{room.Id}:{pair}" : $"combo:{pair}";
        bool firstTime = !State.Flags.Contains(doneFlag);

        if (!firstTime)
        {
            if (!recipe.Repeatable)
            {
                _io.WriteLine("Tu as déjà tenté ça. Une fois suffisait largement.");
                return;
            }
            _io.WriteLine(T(recipe.ResultText)); // répétable : on réaffiche, sans re-récompense
            return;
        }

        _io.WriteLine(T(recipe.ResultText));

        if (recipe.Deadly)
        {
            Die(recipe.DeathMessage ?? "La combinaison t'a été fatale.");
            return;
        }

        if (recipe.ScoreDelta != 0)
        {
            State.Score += recipe.ScoreDelta;
            _io.WriteLine(recipe.ScoreDelta > 0 ? $"(+{recipe.ScoreDelta} pts)" : $"({recipe.ScoreDelta} pts)");
        }
        foreach (string it in recipe.RemovesItems)
            if (State.Inventory.Remove(it))
                _io.WriteLine($"Vous utilisez : {ItemName(it)}");
        foreach (string it in recipe.GrantsItems)
            if (!AcquireOrSwap(it))
                DropToFloor(State.CurrentRoomId, it);
        if (recipe.SetsFlag != null)
            State.Flags.Add(recipe.SetsFlag);

        State.Flags.Add(doneFlag);
    }

    // ------------------------------------------------------------------
    // Serrure à code (générique, déclenchée par la tentative de sortie)
    // ------------------------------------------------------------------

    /// <summary>
    /// Verrou à combinaison d'une sortie : affiche le lockedMessage puis propose la saisie.
    /// Retourne true si le bon code vient d'être composé (la sortie peut alors s'ouvrir),
    /// false si le joueur renonce ou se trompe (code erroné : -2 pts, en silence).
    /// </summary>
    private bool TryEnterCode(Exit exit)
    {
        CodeLock cl = exit.CodeLock!;
        _io.WriteLine(T(exit.LockedMessage ?? "C'est verrouillé."));

        int c = _io.AskChoice("Un clavier à combinaison est encastré dans la porte.",
                              new List<string> { cl.Label, "Faire demi-tour" });
        if (c != 0) return false;

        string saisie = _io.AskText(cl.Prompt).Trim();
        if (saisie == T(cl.Combination)) // {CODE} -> combinaison tirée au sort cette partie
        {
            State.Flags.Add(cl.SetsFlag);
            _io.WriteLine(T(cl.SuccessText));
            return true;
        }

        _io.WriteLine(T(cl.FailText));
        State.Score -= 2; // pénalité discrète : jamais annoncée à l'écran
        return false;
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

        // Objet mortel (ex. une digitale déguisée en jolie fleur) : le ramasser tue.
        if (_world.DeadlyItems.TryGetValue(itemId, out var deathMsg))
        {
            Die(deathMsg);
            return;
        }

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
        _io.WriteSlow(T(message)); // machine à écrire : poids dramatique de la mort
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
        _io.WriteLine();
        _io.WriteLine("... soudainement quelquechose te chatouille le nez... mais tu ouvres les yeux (pourquoi étaient-ils fermés?!)");
        _io.WriteLine("... ah tes idées reviennent... tu es dans ton lit avec ton lapin en peluche serré contre toi");
        _io.WriteLine("... et ton petit lutin diabolique en peluche Nestor qui te regarde intensément à l'autre bout du lit");
        _io.WriteLine("... serait-ce un radis frais à côté de lui ?!");
        _io.WriteLine();
        _io.WriteLine($"Ton score : {State.Score} pts");
        State.IsVictory = true;
        State.VictoryImmoral = false;
    }

    /// <summary>Fin IMMORALE (le Sacrifice) : sortie obtenue au prix de Lapinou, marque [À QUEL PRIX].</summary>
    public void EndImmoral()
    {
        State.Score += 15;
        _io.WriteLine();
        _io.WriteLine("=== FIN DE L'AVENTURE — LA PORTE S'OUVRE. À QUEL PRIX. ===");
        _io.WriteLine();
        _io.WriteLine("... soudainement tes idées te reviennent ...tu ouvres les yeux (pourquoi étaient-ils fermés?!)");
        _io.WriteLine("Tu es dans ton lit..., tu tiens entre tes deux mains crispé le cou de ton pauvre petit lapin en peluche... quel horrible spectacle... tu te sens rempli de regret...ou pas...");
        _io.WriteLine("... et ton petit lutin diabolique en peluche Nestor qui te regarde affichant un sourire narquois à l'autre bout du lit");
        _io.WriteLine("... serait-ce un radis frais à côté de lui ?!");
        _io.WriteLine();
        _io.WriteLine($"Ton score : {State.Score} pts");
        State.IsVictory = true;
        State.VictoryImmoral = true;
    }
}
