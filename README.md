# Château École — Le Lycée Saint-Léger

Jeu d'aventure textuel type « château aventure ». Objectif : sortir du lycée en passant par l'église Saint-Léger.

## Lancer le jeu

Ouvrir `ChateauEcole.sln` dans Visual Studio, définir **ChateauEcole.ConsoleApp** comme projet de démarrage, puis F5.
Ou en ligne de commande : `dotnet run --project ChateauEcole.ConsoleApp`

Prérequis : .NET 8 SDK. Aucun package NuGet.

## Exécutable autonome (un seul fichier)

`world.json` est **embarqué dans l'exe** (ressource) ; `Program.cs` le lit depuis la ressource,
sauf si un `world.json` est posé **à côté de l'exe** (override pour modder sans recompiler).
Un publish self-contained + single-file donne donc **un seul .exe portable** (aucune install
de .NET requise sur la machine cible) :

```
dotnet publish ChateauEcole.ConsoleApp -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Via Visual Studio : clic droit sur **ChateauEcole.ConsoleApp** → **Publier** → Dossier →
Mode **Autonome**, Runtime **win-x64**, **Produire un fichier unique** ✅.
⚠️ Ne PAS activer le trimming (la (dé)sérialisation JSON par réflexion casserait).
Résultat dans `bin/Release/net8.0/win-x64/publish/` (l'exe ; le `.pdb` est optionnel).

## Où sont les sauvegardes ?

Dans `%AppData%\ChateauEcole` (Windows : `C:\Users\<toi>\AppData\Roaming\ChateauEcole\`,
Mac/Linux : `~/.config/ChateauEcole/`) :
- `highscores.json` — le top 10 des meilleurs scores ;
- `sauvegarde.json` — le checkpoint de la réanimation unique.

Ces fichiers survivent aux mises à jour de l'exe.

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
  `[À QUEL PRIX]` (fin immorale), `[MORT]` (mort). Le score n'est enregistré que
  lorsque la partie se termine vraiment.
- **Fichiers** : `%AppData%\ChateauEcole\sauvegarde.json` et `highscores.json`.

## Classement en ligne (optionnel)

Un classement en ligne partagé est disponible en plus des scores locaux, via une abstraction
`IScoreBoard` (comme `IGameIO` pour la console : **le moteur ne touche jamais le réseau**).

- **Backend** : un petit `scores.php` (stockage fichier) hébergé sur un espace perso Free
  (PHP 4). `GET` renvoie le top en JSON, `POST` ajoute un score (secret partagé, rejet des
  scores impossibles `>96` ou négatifs). Le client n'a jamais les identifiants FTP.
- **Client** : `HttpScoreBoard` (dans `ChateauEcole.ConsoleApp`) fait le `GET`/`POST` HTTP.
  **Best-effort / offline-first** : timeout court (3 s), `User-Agent` propre, et **repli
  automatique sur `highscores.json` local** si hors-ligne ou serveur muet — jamais de blocage
  ni de plantage. En fin de partie le score est **publié en ligne ET gardé en local**.
- **Config** : URL + secret sont des constantes dans `Program.cs` (`ScoreUrl`, `ScoreSecret`)
  à ajuster puis recompiler. Le classement affiché indique « en ligne » ou « (local) » selon
  la connectivité.
- **Date + heure** : chaque score porte la date et l'heure **locale du joueur** (envoyée par le
  jeu, champ `when` ; repli sur l'heure serveur), affichées dans le classement — pour le fun
  (« untel a fini le jeu à 4h du matin »).
- **2D** : rien à refaire — la version graphique réutilise `HttpScoreBoard` via `IScoreBoard`.

## Multilingue (fr / en / pl, it à venir)

À **chaque lancement**, le jeu affiche un sélecteur de langue dont le défaut est la langue
courante (choix mémorisé dans `%AppData%\ChateauEcole\langue.cfg`, sinon langue de l'OS) :
**Entrée** garde la langue, **un chiffre** en change — pas besoin de supprimer un fichier pour
réinitialiser. **Le français est la source et le repli** : une traduction manquante retombe sur
le français, jamais d'écran vide.

- Le contenu se traduit en fournissant un fichier **`world_<langue>.json`** (copie traduite de
  `world.json`) — à poser **à côté de l'exe** (test sans recompiler) ou **dans le projet** (une
  ligne `<EmbeddedResource>` à ajouter au `.csproj`) pour l'embarquer dans l'exe unique.
  ⚠️ Nommage avec **underscore** (`world_en.json`, pas `world.en.json`) : sinon .NET prend « en »
  pour une culture et met le fichier dans un assembly satellite → le jeu retombe en français.
- **L'interface** (menus, invites, classement, marques de score) **et tous les textes des
  mini-jeux** se traduisent via un fichier plat **`ui_<langue>.json`** (`"français" → "traduit"`,
  même règle d'underscore et de repli). `ui_en.json` et `ui_pl.json` sont fournis : le jeu est
  **intégralement** en fr / en / pl, rien ne reste en français.
- Procédure complète (consigne prête pour une IA + règles à respecter) et **validateur**
  (`outils/valider_langue.py`) : voir **`docs/TRADUCTION.md`**.

## Progression v2 : 3 branches parallèles + un choix moral

Fini la cascade linéaire. Pour ouvrir la porte de l'église (l'orgue), il faut réunir
**trois éléments indépendants**, dans l'ordre qu'on veut :

1. **Médaille** — `medaillon` : apprivoiser **Lapinou** (mascotte de l'aumônerie) avec la
   **carotte** (cuisines de la cantine). Il te confie son médaillon et **te suit** ensuite
   (compagnon, `GameState.LapinouSuit`, invulnérable). **Seule source** du médaillon.
2. **Mélodie** — `partition` : salle de Musique, au sous-sol, derrière une **serrure à
   code 4 chiffres** (tiré au sort à chaque partie). Les 4 chiffres ne sont plus donnés en clair :
   ils sont **DÉDUCTIBLES** à partir d'infos affichées (le joueur note sur papier) —
   **1** = dernière décimale de l'année de fondation (« depuis 191X », diplômes du bureau) ;
   **2** = nombre de griffures autour de la porte du sous-sol (visible depuis le couloir avant
   d'ouvrir) ; **3** = un petit calcul donné par Mme Bernard (« neuf moins deux ») ; **4** =
   semi-direct au fond de la 6e (après avoir neutralisé « la chose » avec la botte de radis).
3. **Essence du savoir animalier** — le **choix moral**, seulement révélé quand on a déjà
   médaille + mélodie et qu'on tente l'orgue :
   - **voie juste** : retourner LOIN, en salle de Sciences, gagner le mini-jeu de la
     **grenouille** → `grenouille_morte`. Effort honnête.
   - **voie immorale** : **sacrifier Lapinou**, présent à l'orgue — immédiat, gratuit, cruel.
   - **point de non-retour** : rater 2× le mini-jeu grenouille ferme la voie juste et **force**
     le sacrifice (fin immorale « subie », renvoi à l'église, issue condamnée).

**Traversée** : `lampe_torche` (posée **par terre au pied du portail**, à ramasser) pour
franchir le passage vers l'église.

**Deux fins** : JUSTE (`EndJuste`, Lapinou sort avec toi, `[ÉVADÉ]`) vs IMMORALE
(`EndImmoral`, Lapinou meurt, `[À QUEL PRIX]`). Le choix ne se pose qu'à qui a médaille +
mélodie sans l'essence : la tentation, c'est la **flemme** de refaire les 6 salles de trajet.

Nouvelles capacités moteur : **serrure à code** générique portée par une sortie
(`Exit.CodeLock`, saisie déclenchée par la tentative de passage), **état compagnon**
(`GameState.LapinouSuit`, avec réplique de présence variable par salle via `Room.CompanionText`),
**deux fins** (`EndJuste`/`EndImmoral` + marque de score), action générique enrichie
(`RequiredItem`/`ConsumesItem`/`SetsCompanion`/`DeadlyMessage`), gating d'action spéciale
(`SpecialActionRequiredFlag`), **combinaisons d'objets** data-driven (`WorldData.Combinations`
+ `Room.DecorTargets` : action « Combiner / utiliser un objet » qui apparie deux objets, ou un
objet et un élément de décor ; rien ne dit quoi combiner, c'est à déduire), **objets au sol
initiaux** déclarés en JSON (`Room.FloorItems`,
semés au lancement, ramassables via « Ramasser »).

## Mini-jeux et contenu optionnel

- **Combinaisons (réflexion)** : action « Combiner / utiliser un objet » — choisir un objet,
  puis une cible (autre objet OU élément de décor de la salle). Le moteur cherche une recette ;
  sinon « Ça ne donne rien ». Rien n'indique quoi combiner : ça se déduit des descriptions.
  Recettes loufoques/bonus (radis + masque = +5 ; laitue + sandwich = -2 ; grenouille
  + partition = +2 ; **craie** + tableau = inscription cachée +2 par tableau ; lampe + ombre du
  marronnier = frisson). Aucune recette loufoque ne consomme un objet nécessaire (anti-softlock).
  (Le téléphone ne se charge plus par combinaison : voir l'action « Brancher le téléphone » ci-dessous.)
  La **craie** se trouve en Histoire (rebord du tableau), sans usage de progression.
- **Mme Bernard (Maths)** : chifoumi. Récompense = **3e chiffre du code**, donné sous forme d'un
  **petit calcul** (« neuf moins deux ») à résoudre de tête (générateur `GenererCalcul`, résultat
  garanti = le chiffre). Aléatoire à la 1re visite, défi GARANTI dès la 2e. Perdre = -2 pts silencieux.
- **Serrure du sous-sol** : le clavier n'apparaît que lorsqu'on **tente** la porte encore
  verrouillée ; composer les 4 chiffres. **La combinaison est tirée au sort à chaque partie**
  (`GameState.CodeCombination`, sérialisée ; placeholders `{CODE1..4}` et `{CODE}`). Les chiffres
  ne sont plus affichés en clair : ils se **déduisent** (année, griffures, calcul de Bernard, 6e).
  Mauvais code = -2 pts, en silence. Data-driven (`codeLock` porté par la sortie).
- **Grenouille (Sciences)** : voie honnête de l'essence, débloquée seulement après avoir
  tenté l'orgue (`sortie_tentee`). Best-of-3 aléatoire — mais elle peut **s'échapper** : au
  **2e échec** elle disparaît pour de bon (`grenouille_perdue`), le joueur est renvoyé de force
  à l'**église** dont l'issue est alors **condamnée** (`Exit.HiddenIfFlag`), et l'orgue n'offre
  plus que le **sacrifice** — inévitable. Loin de l'église exprès.
- **Prof de sport (Gymnase)** : fuite en 3 choix CHRONOMÉTRÉS (15 s, décompte visible).
  Raccourci : verbes irréguliers (casier du couloir du 1er). Récompense : une botte de radis
  (la collation santé du prof).
- **Classe de 6e** : la botte de radis distrait « la chose » (affamée de frais) → 4ᵉ chiffre du
  code. Insister à mains nues (2e tentative) = mort (avertissement à la 1re).
- **Téléphone + SMS de « N. »** : téléphone cassé (Français, prendre/laisser) + **chargeur trouvé
  très tôt en classe d'Histoire** (sous le bureau du prof : « Regarder » puis « Attraper le fil »,
  prendre/laisser ; **filet de sécurité** : un second chargeur en Techno si tu as laissé le premier).
  Dès qu'on a les deux objets, l'action intégrée **« Brancher le téléphone pour le charger »**
  (disponible dans **toute** salle, anti-softlock) lance une petite **animation de charge comique**
  puis pose `telephone_charge` (le **chargeur est consommé**). Le **premier SMS tombe aussitôt**.
  Chaque SMS a un **rituel animé** : vibration `[vert]Bzzt. Bzzt.[/vert]` en machine à écrire, puis
  le message en **vert**. Les SMS d'un mystérieux **« N. »** (= Nestor, jamais nommé) orientent vers
  les 3 branches et sèment le doute sur Lapinou. Un SMS max par tour ; le signal meurt dans le
  passage. Le téléphone reste un **bonus narratif**, jamais un verrou de progression.
- **Cantine** : sandwich (surveillant → bureau) et, pour Lapinou, **soit la carotte, soit une
  vieille laitue — jamais les deux à la fois** (on peut reposer l'une pour prendre l'autre, donc
  pas de blocage). La carotte l'apprivoise ; **offrir la laitue à Lapinou est MORTEL** (rage
  du lapin). La « Surprise du chef » est un autre piège mortel (menu souligné trois fois).
- **Easter eggs Nestor** : oreilles vertes fugaces, ombre verte, « N... ce bg de ouf ! »
  disséminés dans les salles d'ambiance. Jamais expliqués.

## Effets d'affichage (couleur, machine à écrire, pauses)

Tous les effets sont portés par `IGameIO` et implémentés **uniquement** dans `ConsoleIO`
(le Core ne connaît toujours ni `System.Console` ni la notion de couleur). Ils **se
dégradent proprement** quand la sortie/entrée est redirigée (tests, pipes) : pas de
couleur, pas d'attente, pas de blocage, texte brut sans balise.

- **Couleur par balises inline** : `[rouge]…[/rouge]` (et `vert`, `bleu`, `jaune`, `cyan`,
  `magenta`, `gris`, `blanc` ; fermeture générique `[/]`). Interprétées **au niveau de
  `WriteLine`**, donc **toutes** les lignes (world.json comme mini-jeux C#) sont colorées
  de la même façon. Balise inconnue ou mal fermée = rendue littéralement, **jamais de
  plantage**. Fonction unique `ConsoleIO.StripTags(text)` pour obtenir le texte brut
  (mode redirigé, saisies) — les balises ne s'affichent jamais telles quelles en console.
- **Machine à écrire** : `WriteSlow(text, msParCaractère = 0)` — égrène les caractères
  visibles (les balises sont parsées d'abord, la couleur est respectée). Toujours joué
  jusqu'au bout (non interruptible) ; **instantané** si la sortie est redirigée. **Vitesse
  réglable** : passer `msParCaractère` (ex. `WriteSlow(t, 12)` = rapide) ; `0` = vitesse par
  défaut **configurable à chaud** via `ConsoleIO.VitesseParDefautMs` (repères : lent ≈ 55,
  normal ≈ 30, rapide ≈ 12). Côté data : marqueur `[slow]` (défaut) ou `[slow=NN]` (ms explicites).
- **Écran-titre de salle** : `ShowRoomTitle(roomName, titleArt?)` — appelé **à l'entrée** de
  chaque salle. Efface l'écran (`Clear`) puis, sans art, dessine un **encadré cyan auto** dont
  la bordure s'adapte à la longueur du nom (accents corrects) :
  ```
  ╔══════════════════════╗
  ║  CLASSE DE FRANÇAIS  ║
  ╚══════════════════════╝
  ```
  Avec un `titleArt` (voir §Room ci-dessous), cet ASCII art s'affiche à la place (balises couleur
  rendues). Le moteur ne re-déclenche l'écran-titre **qu'au changement de salle**, pas à chaque
  tour, sinon le `Clear` effacerait la sortie de l'action précédente.
- **Champ `Room.TitleArt`** (world.json, optionnel) : ASCII art multi-lignes (séparateur `\n`,
  balises couleur autorisées) qui remplace l'encadré auto pour cette salle. Vide partout par
  défaut ; un exemple est posé sur `eglise` (petit orgue + croix).
- **Pauses / effacement** : `Pause(message?)` (« — Appuie sur une touche pour continuer — »
  par défaut, ne bloque pas si l'entrée est redirigée), `PauseThenClear(message?)`,
  `WaitThenClear(secondes)` (aucune attente si redirigé) et `Clear()`.
- **Pauses avant les `Clear` d'entrée** : parce que chaque entrée de salle efface l'écran, un
  `Pause()` a été inséré là où un texte s'affiche juste avant un changement de salle, pour
  laisser le temps de lire : intro/règles au lancement, message de réanimation, **texte de
  transition de sortie** (`TransitionText`) et **mini-jeux qui téléportent** (fuite de la
  grenouille → église, éjection du gymnase → cour).
- **Déclenché des deux côtés** :
  - **En C#**, les mini-jeux appellent directement les méthodes et posent des balises
    couleur (ex. sacrifice de l'orgue : `WriteSlow` + `Pause` + « [rouge]immonde[/rouge] » ;
    fuite de la grenouille : `PauseThenClear` avant le renvoi à l'église ; morts de la
    cantine avec `WriteSlow` + `Pause`).
  - **En data (world.json)**, la couleur est libre (via `WriteLine`), et des **marqueurs
    inline** pilotent les effets sur les textes de salles/actions/examens :
    - **structurels** (moteur, `GameEngine.Emit()`) : `[clear]` (efface l'écran avant),
      `[pause]` (attend une touche après) ;
    - **de rendu** (affichage, comme la couleur) : `[slow]` (vitesse par défaut),
      `[slow=NN]` (NN ms/caractère), `[/slow]` (retour au normal instantané) et `[pause=N]`
      (attend **N secondes au milieu** du texte, sans touche, puis repart). Vitesses et pauses
      peuvent survenir **plusieurs fois au milieu d'une même ligne** — ex.
      `"Normal [slow]lent [slow=100]très lent[/slow] et il attend[pause=2] puis repart."`.
      (À distinguer de `[pause]` structurel, qui attend une **touche** en fin de texte.)

Le mémo complet des balises et marqueurs (avec exemples) est dans `RECAP-projet.md`.

## Solution du jeu (spoiler)

Traversée : **lampe** (au sol au pied du portail, action « Ramasser »). Les trois éléments de l'orgue, dans n'importe quel ordre :

1. **Médaille** : carotte (cuisines de la cantine) → aumônerie, « Donner la carotte à Lapinou »
   → médaillon + Lapinou te suit.
2. **Mélodie** : récolter les 4 chiffres du code — bureau du proviseur (via sandwich →
   surveillant) = 9 ; toilettes AVEC le masque (Sciences, libre) = 1 ; chifoumi Bernard = 2.
   Composer le code (aléatoire, révélé par les 3 fragments) sur la serrure du sous-sol → Salle de Musique → partition.
3. **Église** (aumônerie → passage, avec la lampe) → « S'approcher de l'orgue ».
   - Avec médaille + mélodie + **grenouille** → **FIN JUSTE** (Lapinou sort avec toi).
   - Avec médaille + mélodie sans grenouille : le jeu ouvre le mini-jeu grenouille (Sciences,
     tout en haut) et propose le choix. Voie juste = y aller. Voie immorale = **sacrifier
     Lapinou** → **FIN IMMORALE**.

Branche 4e chiffre : verbes irréguliers (couloir 1er) → prof de sport → botte de radis → chose de la 6e → 4ᵉ chiffre du code.
Branche téléphone : téléphone cassé (Français) + chargeur (Histoire, sous le bureau ; secours en Techno) → action « Brancher le téléphone » → SMS animés de « N. » dès le début de partie.

Pièges mortels : toilettes sans masque, relire le livre de latin, la « Surprise du chef »,
offrir la vieille laitue à Lapinou, insister à mains nues sur la chose de la 6e, cueillir et
goûter la « jolie fleur violette » du portail (une digitale mortelle).

Décor : le **bureau de Mme Roy** (sous-sol) reste verrouillé — l'ouvrir laisse seulement filtrer
des plaintes d'anciens élèves, façon bruits de fantômes. On n'insiste pas.

Porte de la classe de **Français** : verrouillée de l'extérieur au départ (on tente, message,
on reste enfermé) — d'où le passage secret derrière la bibliothèque vers l'Histoire, puis le
couloir. Une fois qu'on est entré en Français **depuis le couloir** (flag `francais_deverrouille`
posé par cette sortie), la porte normale s'ouvre des deux côtés : on peut ressortir directement
au couloir, sans repasser par l'Histoire.

## Roadmap 2D (old-school)

Le moteur ne connaissant que `IGameIO`, la version graphique consiste à :
1. Créer un projet `ChateauEcole.Gui` référençant Core.
2. Recommandé : **Raylib-cs** (NuGet `Raylib-cs`) — simple, parfait pour du pixel-art rétro.
   Alternative : MonoGame (plus standard, plus de plomberie).
3. Implémenter `IGameIO` : les textes s'affichent dans un panneau, les choix deviennent
   des boutons ou une liste navigable au clavier ; ajouter un sprite de salle par `roomId`.
   Les effets d'affichage sont déjà abstraits : la couleur des balises `[rouge]…[/rouge]`
   devient une couleur de police, `WriteSlow` une animation de texte, `Pause`/`Clear` une
   transition d'écran, et `ShowRoomTitle` un **bandeau/scène d'entrée de salle** (le
   `TitleArt` d'une salle devenant une image plein écran) — la même API sert au rendu
   graphique (réutiliser la logique de `StripTags`/segments pour parser les balises).
Aucune modification du Core ni de world.json n'est nécessaire.
