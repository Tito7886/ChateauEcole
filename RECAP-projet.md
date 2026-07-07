# RECAP — Château École (Lycée Saint-Léger)

Jeu d'aventure textuel en console (C# / .NET 8), ton « Undertale », **entièrement
data-driven** : tout le contenu vit dans `ChateauEcole.ConsoleApp/world.json`. Un portage 2D
est prévu.

## Règle d'architecture ABSOLUE

`ChateauEcole.Core` (le moteur) ne touche **jamais** `System.Console` : toutes les
entrées/sorties passent par l'interface `IGameIO`, implémentée **uniquement** par `ConsoleIO`
(dans `ChateauEcole.ConsoleApp`). Tout effet doit **se dégrader proprement** quand la
sortie/entrée est redirigée (tests, pipes) : pas de couleur, pas d'attente, pas de blocage,
texte brut sans balise. C'est cette abstraction qui rend le portage 2D possible sans toucher au
Core.

```
ChateauEcole.Core        moteur, modèles, mini-jeux (aucune référence à la console)
  IGameIO.cs               contrat d'affichage/saisie (clé du portage 2D)
  GameEngine.cs            boucle de jeu, T() (placeholders), Emit() (marqueurs data)
  Models/Room.cs           Room, Exit, Interaction... (miroir de world.json)
  MiniGames/               scènes scriptées en C# (RituelOrgue, GrenouilleSciences...)
ChateauEcole.ConsoleApp
  Program.cs               ConsoleIO : couleur, machine à écrire, pauses, écran-titre
  world.json               TOUT le contenu (salles, objets, énigmes, textes)
```

---

## 🎨 Aide-mémoire de la mise en forme (balises & marqueurs)

Il existe **deux familles** de codes inline.

### A. Balises de COULEUR — partout

Interprétées au niveau de `ConsoleIO.WriteLine`, donc valables sur **toutes** les lignes
(textes de `world.json` ET `io.WriteLine(...)` des mini-jeux C#). Une balise inconnue ou mal
fermée est rendue **littéralement** (jamais de plantage). En sortie redirigée, elles sont
retirées (`ConsoleIO.StripTags`).

| Balise | Couleur | Exemple | Rendu |
|---|---|---|---|
| `[rouge]…[/rouge]` | rouge | `une [rouge]porte[/rouge]` | **porte** en rouge |
| `[vert]…[/vert]` | vert | `un [vert]radis[/vert]` | radis en vert |
| `[bleu]…[/bleu]` | bleu | `le [bleu]ciel[/bleu]` | ciel en bleu |
| `[jaune]…[/jaune]` | jaune | `[jaune]attention[/jaune]` | attention en jaune |
| `[cyan]…[/cyan]` | cyan | `[cyan]orgue[/cyan]` | orgue en cyan |
| `[magenta]…[/magenta]` | magenta | `[magenta]rêve[/magenta]` | rêve en magenta |
| `[gris]…[/gris]` | gris foncé | `[gris](aparté)[/gris]` | aparté en gris |
| `[blanc]…[/blanc]` | blanc | `[blanc]NEIGE[/blanc]` | NEIGE en blanc |

- **Fermeture générique** : `[/]` ferme la couleur courante sans la nommer.
  Ex. `[cyan]ciel[/] clair` → « ciel » en cyan, « clair » normal.
- Les balises peuvent s'enchaîner dans une même ligne :
  `une [rouge]porte[/rouge] et un [vert]radis[/vert]`.

### B. Marqueurs d'EFFET — sur les textes de contenu (data-driven)

Placés dans les champs texte de `world.json` (description, `examine.text`, `actions[].text`,
`transitionText`…). Ils sont de **deux sous-types** :

- **STRUCTURELS** (`[clear]`, `[pause]`) : actions du moteur, consommées par `GameEngine.Emit()`
  et retirées du texte.
- **DE RENDU** (`[slow]`, `[slow=NN]`, `[/slow]`) : interprétées par l'affichage (`ConsoleIO`),
  **exactement comme la couleur** — elles peuvent donc changer **plusieurs fois au milieu d'une
  même ligne**, se mélanger à la couleur et cohabiter avec du texte normal (instantané).

| Marqueur | Type | Effet | Exemple (world.json) |
|---|---|---|---|
| `[clear]` | structurel | efface l'écran **avant** d'afficher | `"[clear]Tu émerges dans la nef..."` |
| `[pause]` | structurel | attend une **touche** **après** l'affichage | `"...derrière cette porte.[pause]"` |
| `[slow]` | rendu | machine à écrire, **vitesse par défaut** | `"[slow]« COURS, {NOM} »"` |
| `[slow=NN]` | rendu | machine à écrire à **NN ms/caractère** | `"[slow=90]lentement, très lentement..."` |
| `[/slow]` | rendu | **retour au normal** (instantané) | `"[slow]lent[/slow] et hop, normal."` |
| `[pause=N]` | rendu | attend **N secondes AU MILIEU** du texte, sans touche, puis **repart** | `"Il attend[pause=3] puis reprend son souffle."` |

> ⚠️ Ne pas confondre les deux pauses : `[pause]` (structurel) attend une **touche** en fin
> de texte ; `[pause=N]` (rendu) attend **N secondes** là où il est placé dans la ligne, puis
> continue tout seul. Comme les autres balises de rendu, `[pause=N]` ne bloque pas et n'attend
> pas en sortie redirigée (tests/pipes).

**Vitesses mixées dans une même ligne** — chaque `[slow]`/`[slow=NN]` redéfinit la vitesse
courante, `[/slow]` revient à l'instantané :

```
"Normal, puis [slow]vitesse par défaut, [slow=100]bien plus lent[slow] retour au slow[/slow] et de nouveau normal."
```

→ « Normal, puis » instantané, « vitesse par défaut, » à ~50 ms, « bien plus lent » à 100 ms,
« retour au slow » à ~50 ms, « et de nouveau normal. » instantané.

On combine avec les structurels et la couleur :
`"[clear][slow]Bonjour [rouge]{NOM}[/rouge][pause]"` = efface, écrit lentement (prénom en
rouge), puis attend une touche. (Dans les mini-jeux C#, on n'utilise pas les marqueurs
structurels : on appelle directement `io.WriteSlow(...)`, `io.Pause()`, `io.Clear()` — mais les
balises de rendu `[slow]`/`[rouge]` fonctionnent aussi dans un `io.WriteLine(...)`/`io.WriteSlow(...)`.)

### C. Placeholders de texte (résolus par `GameEngine.T()`)

| Placeholder | Remplacé par | Exemple |
|---|---|---|
| `{NOM}` | prénom du joueur | `"Cours, {NOM} !"` |
| `{CODE1}`…`{CODE4}` | les 4 chiffres du code (tiré au sort) | `"le premier chiffre est {CODE1}"` |
| `{CODE}` | le code complet à 4 chiffres | `"la combinaison {CODE}"` |

> Les placeholders et les balises se cumulent : `"« [rouge]COURS, {NOM}[/rouge] »"`.

---

## 🖼️ Écran-titre de salle

À **chaque entrée** dans une salle, le moteur appelle `IGameIO.ShowRoomTitle(nom, titleArt?)`
(à la place de l'ancien `=== nom ===`). Effet : effacement de l'écran puis mise en valeur du
nom, avant la description.

- **Par défaut** : encadré cyan dont la bordure s'adapte à la longueur réelle du nom
  (accents compris) :
  ```
  ╔══════════════════════╗
  ║  CLASSE DE FRANÇAIS  ║
  ╚══════════════════════╝
  ```
- **Surcharge par salle** : champ optionnel `Room.TitleArt` dans `world.json` — un ASCII art
  multi-lignes (séparateur `\n`, **balises couleur autorisées**) affiché à la place de
  l'encadré. Vide partout par défaut ; exemple sur `eglise` :
  ```json
  "titleArt": "        [jaune]✝[/jaune]\n     ╒═══════════╕\n     │ █ █ █ █ █ │\n     │ █ █ █ █ █ │\n     ╘═══════════╛\n   [cyan]ÉGLISE SAINT-LÉGER[/cyan]"
  ```
- L'écran-titre n'est **pas** re-déclenché tant qu'on reste dans la même salle (sinon le
  `Clear` effacerait la sortie de l'action qu'on vient de faire) — uniquement au changement.
- **Dégradation** : en sortie redirigée, `ShowRoomTitle` n'affiche qu'un simple `=== nom ===`.

### Pauses ajoutées à cause des `Clear` d'entrée

Comme l'entrée de salle efface l'écran, un `Pause()` a été inséré là où un texte s'affiche
juste avant un changement de salle :

- fin de l'intro / règles au **lancement** (`GameEngine.NewGame`) ;
- message de **réanimation** (`HandleGameEnd`) ;
- **texte de transition** d'une sortie (`TryExit`, si `transitionText` présent) ;
- **mini-jeux qui téléportent** : fuite de la grenouille → église (`GrenouilleSciences`),
  éjection du gymnase → cour (`ProfDeSport`).

---

## ⏱️ Vitesse de la machine à écrire (réglable)

`WriteSlow(text, msParCaractère = 0)` :

- **par appel** : passer une valeur — `io.WriteSlow(t, 12)` (rapide), `io.WriteSlow(t, 55)` (lent) ;
- **par défaut** (`0` ou omis) : utilise `ConsoleIO.VitesseParDefautMs`, **modifiable à chaud**
  (repères : lent ≈ 55, normal ≈ 30, rapide ≈ 12) ;
- **par data** : marqueur `[slow]` (vitesse par défaut) ou `[slow=NN]` (NN ms/caractère explicite).

---

## 🕹️ Contenu & mécaniques (rappel)

Progression non-linéaire : réunir **médaille** (Lapinou + carotte), **mélodie** (partition,
serrure à code 4 chiffres déductible) et **essence** (grenouille OU sacrifice de Lapinou) pour
l'orgue de l'église → **2 fins** (juste / immorale). Mini-jeux : chifoumi Bernard, grenouille
(Sciences), prof de sport (gymnase chronométré), rituel de l'orgue. Combinaisons d'objets
data-driven, objets au sol, SMS de « N. » (Nestor), pièges mortels. Détail complet des salles,
énigmes et de la solution : voir `README.md`.

---

## 🔭 Note portage 2D

Rien à changer dans le Core : il suffit d'une nouvelle implémentation d'`IGameIO`. La couleur
des balises devient une couleur de police, `WriteSlow` une animation de texte, `Pause`/`Clear`
une transition d'écran, et `ShowRoomTitle` un bandeau / une scène d'entrée (le `TitleArt` d'une
salle devenant une image plein écran). Réutiliser la logique `StripTags`/segments pour parser
les balises côté rendu graphique.
