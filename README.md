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
- **Meilleurs scores** : top 10 avec prénom, [ÉVADÉ] ou [disparu]. Le score
  n'est enregistré que lorsque la partie se termine vraiment.
- **Fichiers** : `%AppData%\ChateauEcole\sauvegarde.json` et `highscores.json`.

## Mini-jeux et contenu optionnel

- **Mme Bernard (Maths)** : chifoumi pour la clef de Sciences. Humeur aléatoire
  à la 1re visite, mais défi GARANTI dès la 2e visite (pity timer, compteur
  GameState.Counters). Un post-it du couloir du 1er oriente vers elle dès le départ.
- **Prof de sport (Gymnase)** : fuite en 3 choix CHRONOMÉTRÉS (15 s avec décompte
  visible, sinon choix par défaut perdant) — rattrapé = -5 pts et éjection dans la
  cour. Alternative : brandir la feuille de verbes irréguliers anglais (trouvée tôt,
  dans un casier du couloir du 1er) = victoire immédiate. Récompense : balles de tennis.
- **Classe de 6e** : la chose au fond de la salle. Une balle de tennis (consommée)
  la distrait et libère un chargeur de téléphone. Insister à mains nues (2e tentative
  sans balles) = mort (le 1er refus sert d'avertissement).
- **Téléphone + SMS de « R. »** : téléphone cassé trouvé au départ (Français) +
  chargeur (6e) = le téléphone s'allume, puis un mystérieux « R. » envoie des SMS
  qui commentent la progression (règles data-driven dans world.json, section "sms",
  un SMS max par tour). Le signal meurt dans le passage sous-terrain.
- **Cantine** : le sandwich (utile, amadoue le surveillant) et la « Surprise du chef »
  — un piège MORTEL (le menu la souligne trois fois, trois mains différentes : tu étais
  prévenue).
- **Salles évolutives** : `stateTexts` sur une salle = textes affichés lors des
  visites suivantes selon les flags posés (prof vaincu, chose partie, etc.).

## Solution du jeu (spoiler)

1. Classe de Français (départ) : fouiller le bureau (téléphone), inspecter puis pousser la bibliothèque → passage secret vers l'Histoire.
2. Classe de Maths : battre Mme Bernard au chifoumi → clef de la classe de Sciences.
3. Classe de Sciences (2e étage) : examiner → masque à gaz.
4. Toilettes (1er) : examiner AVEC le masque → clef du sous-sol (sans masque : mort !).
5. Classe de Techno (RDC) : examiner → lampe torche.
6. Cantine (cour) : examiner → sandwich. Salle des profs : le sandwich amadoue le surveillant → bureau du Proviseur (indice).
7. Sous-sol (avec la clef) → Salle de Musique : examiner → médaillon + partition.
8. Aumônerie (avec la lampe) → passage sous-terrain → église Saint-Léger.
9. Jouer la partition et déposer le médaillon sur l'orgue → VICTOIRE.

Contenu optionnel : verbes irréguliers (casier du couloir du 1er) → faire fuir le prof de sport
(Gymnase) → balles de tennis → distraire la chose (6e) → chargeur → téléphone allumé → SMS de « R. ».

Pièges mortels : toilettes sans masque, relire le livre de latin, la « Surprise du chef » de la
cantine, insister à mains nues sur la chose de la 6e.

## Roadmap 2D (old-school)

Le moteur ne connaissant que `IGameIO`, la version graphique consiste à :
1. Créer un projet `ChateauEcole.Gui` référençant Core.
2. Recommandé : **Raylib-cs** (NuGet `Raylib-cs`) — simple, parfait pour du pixel-art rétro.
   Alternative : MonoGame (plus standard, plus de plomberie).
3. Implémenter `IGameIO` : les textes s'affichent dans un panneau, les choix deviennent
   des boutons ou une liste navigable au clavier ; ajouter un sprite de salle par `roomId`.
Aucune modification du Core ni de world.json n'est nécessaire.
