# Château École — Le Lycée Saint-Léger

Jeu d'aventure textuel type « château aventure ». Objectif : sortir du lycée en passant par l'église Saint-Léger.

## Lancer le jeu

Ouvrir `ChateauEcole.sln` dans Visual Studio, définir **ChateauEcole.ConsoleApp** comme projet de démarrage, puis F5.
Ou en ligne de commande : `dotnet run --project ChateauEcole.ConsoleApp`

Prérequis : .NET 8 SDK. Aucun package NuGet.

## Architecture

```
ChateauEcole.Core        <- moteur : AUCUNE salle en dur, aucune référence à la console
  Models/                   Room, Exit, Interaction, WorldData (miroir de world.json)
  GameState.cs              inventaire, flags, score
  GameEngine.cs             la boucle de jeu unique (remplace la récursion du prototype Python)
  IGameIO.cs                abstraction entrées/sorties -> clef du futur portage 2D
  MiniGames/                actions codées en C# référencées par world.json (specialActionId)

ChateauEcole.ConsoleApp  <- affichage texte uniquement
  Program.cs                ConsoleIO (implémente IGameIO) + chargement de world.json
  world.json                TOUT le contenu du jeu : salles, sorties, objets, énigmes
```

## Ajouter du contenu (sans toucher au C#)

- **Une salle** : ajouter un bloc dans `rooms` de world.json et une sortie depuis une salle existante.
- **Un objet** : ajouter l'id dans `items`, puis `grantsItems` dans l'`examine` d'une salle.
- **Une porte verrouillée** : `requiredItems` + `lockedMessage` sur la sortie.
- **Un passage secret** : `hiddenUntilFlag` sur la sortie + `setsFlag` sur l'`examine` qui le révèle.
- **Une salle mortelle sans protection** : `requiredItem` + `deathWithoutItemMessage` sur l'`examine`.
- **Un objet consommé** (sandwich du surveillant) : `consumeRequiredItems: true`.

Un mini-jeu ou une scène scriptée = une classe dans `Core/MiniGames/` + une entrée dans le
dictionnaire `_specialActions` de `GameEngine`, référencée par `specialActionId` dans world.json.

## Prénom, réanimation unique et meilleurs scores

- **Prénom** : demandé au lancement. Le placeholder `{NOM}` de world.json est
  remplacé dans tous les textes (touches narratives façon Undertale).
- **Réanimation** : un checkpoint invisible est pris à chaque changement de
  salle, mais il ne sert QU'À une chose : en cas de mort, le joueur se voit
  proposer UNE UNIQUE réanimation par partie, qui le ramène à l'entrée de la
  dernière salle et lui coûte 1/4 de ses points acquis (arrondi à l'entier
  inférieur, jamais appliqué sur un score négatif). La règle est annoncée en
  début de partie, le coût est rappelé au moment du choix, et le joueur est
  prévenu après coup que la sauvegarde est consumée. Flag : GameState.ResurrectionUsed.
- **Meilleurs scores** : top 10 avec prénom et marque de fin — `[ÉVADÉ]` (fin juste),
  `[À QUEL PRIX]` (fin immorale), `[disparu]` (mort). Le score n'est enregistré que
  lorsque la partie se termine vraiment.
- **Fichiers** : `%AppData%\ChateauEcole\sauvegarde.json` et `highscores.json`.

## Progression v2 : 3 branches parallèles + un choix moral

Fini la cascade linéaire. Pour ouvrir la porte de l'église (l'orgue), il faut réunir
**trois éléments indépendants**, dans l'ordre qu'on veut :

1. **Médaille** — `medaillon` : apprivoiser **Lapinou** (mascotte de l'aumônerie) avec la
   **carotte** (cuisines de la cantine). Il te confie son médaillon et **te suit** ensuite
   (compagnon, `GameState.LapinouSuit`, invulnérable). **Seule source** du médaillon.
2. **Mélodie** — `partition` : salle de Musique, au sous-sol, derrière une **serrure à
   code 3 chiffres** (tiré au sort à chaque partie). Les 3 chiffres sont des **fragments** à récolter (indices
   croisés) : bureau du proviseur (9), toilettes avec masque (1), chifoumi de Bernard (2).
3. **Essence du savoir animalier** — le **choix moral**, seulement révélé quand on a déjà
   médaille + mélodie et qu'on tente l'orgue :
   - **voie juste** : retourner LOIN, en salle de Sciences, gagner le mini-jeu de la
     **grenouille** → `grenouille_morte`. Effort honnête.
   - **voie immorale** : **sacrifier Lapinou**, présent à l'orgue — immédiat, gratuit, cruel.

**Traversée** : `lampe_torche` (Techno, libre) pour franchir le passage vers l'église.

**Deux fins** : JUSTE (`EndJuste`, Lapinou sort avec toi, `[ÉVADÉ]`) vs IMMORALE
(`EndImmoral`, Lapinou meurt, `[À QUEL PRIX]`). Le choix ne se pose qu'à qui a médaille +
mélodie sans l'essence : la tentation, c'est la **flemme** de refaire les 6 salles de trajet.

Nouvelles capacités moteur : **serrure à code** générique portée par une sortie
(`Exit.CodeLock`, saisie déclenchée par la tentative de passage), **état compagnon**
(`GameState.LapinouSuit`, avec réplique de présence variable par salle via `Room.CompanionText`),
**deux fins** (`EndJuste`/`EndImmoral` + marque de score), action générique enrichie
(`RequiredItem`/`ConsumesItem`/`SetsCompanion`), gating d'action spéciale
(`SpecialActionRequiredFlag`).

## Mini-jeux et contenu optionnel

- **Mme Bernard (Maths)** : chifoumi. Récompense = **3e chiffre du code** (fragment_3).
  Aléatoire à la 1re visite, défi GARANTI dès la 2e (pity timer). Post-it du couloir du 1er.
  Perdre coûte -2 pts, en silence (aucun message).
- **Serrure du sous-sol** : le clavier n'apparaît que lorsqu'on **tente** la porte encore
  verrouillée ; composer les 3 chiffres. **La combinaison est tirée au sort à chaque partie**
  (stockée dans `GameState.CodeCombination`, sérialisée ; exposée aux textes via les
  placeholders `{CODE1}`/`{CODE2}`/`{CODE3}`, et `{CODE}` pour la serrure). Chaque chiffre
  n'est montré qu'une fois, à sa découverte (pas de récapitulatif). Mauvais code = -2 pts, en
  silence. Data-driven (`codeLock` porté par la sortie dans world.json).
- **Grenouille (Sciences)** : voie honnête de l'essence, débloquée seulement après avoir
  tenté l'orgue (`sortie_tentee`). Best-of-3 aléatoire, retentable. Loin de l'église exprès.
- **Prof de sport (Gymnase)** : fuite en 3 choix CHRONOMÉTRÉS (15 s, décompte visible).
  Raccourci : verbes irréguliers (casier du couloir du 1er). Récompense : balles de tennis.
- **Classe de 6e** : une balle de tennis distrait la chose → chargeur. Insister à mains nues
  (2e tentative) = mort (avertissement à la 1re).
- **Téléphone + SMS de « N. »** : téléphone cassé (Français) + chargeur (classe de Techno,
  accessible tôt sans verrou ; aussi en 6e via les balles) = SMS d'un
  mystérieux **« N. »** (= Nestor, jamais nommé) qui oriente vers les 3 branches et sème le
  doute sur Lapinou. Un SMS max par tour ; le signal meurt dans le passage.
- **Cantine** : sandwich (surveillant → bureau) et carotte (Lapinou). La « Surprise du chef »
  est un piège MORTEL (menu souligné trois fois).
- **Easter eggs Nestor** : oreilles vertes fugaces, ombre verte, « N... ce bg de ouf ! »
  disséminés dans les salles d'ambiance. Jamais expliqués.

## Solution du jeu (spoiler)

Traversée : **lampe** (Techno, libre). Les trois éléments de l'orgue, dans n'importe quel ordre :

1. **Médaille** : carotte (cuisines de la cantine) → aumônerie, « Donner la carotte à Lapinou »
   → médaillon + Lapinou te suit.
2. **Mélodie** : récolter les 3 chiffres du code — bureau du proviseur (via sandwich →
   surveillant) = 9 ; toilettes AVEC le masque (Sciences, libre) = 1 ; chifoumi Bernard = 2.
   Composer le code (aléatoire, révélé par les 3 fragments) sur la serrure du sous-sol → Salle de Musique → partition.
3. **Église** (aumônerie → passage, avec la lampe) → « S'approcher de l'orgue ».
   - Avec médaille + mélodie + **grenouille** → **FIN JUSTE** (Lapinou sort avec toi).
   - Avec médaille + mélodie sans grenouille : le jeu ouvre le mini-jeu grenouille (Sciences,
     tout en haut) et propose le choix. Voie juste = y aller. Voie immorale = **sacrifier
     Lapinou** → **FIN IMMORALE**.

Branche optionnelle : verbes irréguliers → prof de sport → balles → chose de la 6e → chargeur
→ téléphone → SMS de « N. ».

Pièges mortels : toilettes sans masque, relire le livre de latin, la « Surprise du chef »,
insister à mains nues sur la chose de la 6e.

## Roadmap 2D (old-school)

Le moteur ne connaissant que `IGameIO`, la version graphique consiste à :
1. Créer un projet `ChateauEcole.Gui` référençant Core.
2. Recommandé : **Raylib-cs** (NuGet `Raylib-cs`) — simple, parfait pour du pixel-art rétro.
   Alternative : MonoGame (plus standard, plus de plomberie).
3. Implémenter `IGameIO` : les textes s'affichent dans un panneau, les choix deviennent
   des boutons ou une liste navigable au clavier ; ajouter un sprite de salle par `roomId`.
Aucune modification du Core ni de world.json n'est nécessaire.
