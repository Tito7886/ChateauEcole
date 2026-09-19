# Traduction / multilingue

Le jeu peut tourner en plusieurs langues. **Le français est la source et le repli** : s'il
manque une traduction, le français s'affiche (jamais d'écran vide).

Langues prévues : **fr** (source), **en**, **pl**, puis **it** plus tard. Toutes latines →
aucun souci d'affichage console.

## Comment ça marche

- Au lancement, le jeu propose un sélecteur de langue (détecté depuis l'OS par défaut), puis
  **mémorise** le choix dans `%AppData%\ChateauEcole\langue.cfg`. Pour rechoisir : supprimer ce
  fichier.
- Le contenu du jeu est dans `world.json` (français). Pour chaque autre langue, il suffit d'un
  fichier **`world_<langue>.json`** (copie traduite) : `world_en.json`, `world_pl.json`, …
- Le jeu charge, dans l'ordre : `world_<langue>.json` **à côté de l'exe**, sinon la version
  **embarquée**, sinon `world.json` (français).

### Deux façons d'ajouter une langue
1. **Sans recompiler** (idéal pour tester) : poser `world_en.json` **à côté de l'exe**.
2. **Dans l'exe** (pour distribuer un fichier unique) : mettre `world_en.json` dans le projet
   `ChateauEcole.ConsoleApp` et **ajouter une ligne** `<EmbeddedResource>` dans le `.csproj`
   (copier celle de `world_en.json` déjà présente), puis recompiler.

## Produire une traduction avec une IA

Donne le `world.json` à l'IA avec cette consigne (adapte la langue cible) :

> Traduis ce fichier `world.json` en **anglais** et produis `world_en.json`. Règles **absolues** :
> - Garde la **structure JSON identique** (mêmes clés, mêmes tableaux, **même ordre**), résultat **UTF-8 valide**.
> - **Ne traduis jamais** : les `id`, les flags (`setsFlag`, `requiredFlags`, `requiredFlagsAbsent`, `hiddenUntilFlag`, `hiddenIfFlag`, `bypassIfFlag`, `specialActionRequiredFlag`…), les cibles `target`, les noms de recette `itemA`/`itemB`, les **clés** de `items`, `floorItems`, `grantsItems`, `requiredItems`, `decorTargets[].id`, les valeurs booléennes et numériques.
> - **Conserve exactement** les placeholders : `{NOM}`, `{CODE}`, `{CODE1}`, `{CODE2}`, `{CODE3}`, `{CODE4}`.
> - **Conserve exactement** les marqueurs de mise en forme présents dans les textes : `[slow]`, `[slow=NN]`, `[/slow]`, `[pause]`, `[pause=N]`, `[clear]`, et les couleurs `[rouge]…[/rouge]`, `[vert]`, `[bleu]`, `[jaune]`, `[cyan]`, `[magenta]`, `[gris]`, `[blanc]`, fermeture `[/]`.
> - **Champs à traduire** (uniquement leurs valeurs texte) : `name`, `description`, `text`, `repeatText`, `label`, `transitionText`, `lockedMessage`, tous les champs se terminant par `Message`, `resultText`, `deathMessage`, `companionText`, `titleArt`, `successText`, `failText`, `prompt`, et les **valeurs** de `items`.

## Vérifier une traduction

```
python3 outils/valider_langue.py ChateauEcole.ConsoleApp/world.json ChateauEcole.ConsoleApp/world_en.json
```

Le validateur signale :
- ✗ **problèmes structurels** (ids/flags/cibles/structure modifiés) → le jeu casserait, à corriger ;
- ⚠ **avertissements** placeholders/marqueurs (un `{NOM}` ou un `[rouge]` perdu dans un texte).

Les nombres des marqueurs (`[slow=200]` vs `[slow=250]`) peuvent différer sans alerte : c'est du
style. Les `{placeholders}` doivent, eux, être strictement identiques.

## Interface et mini-jeux (`ui_<langue>.json`)

En plus du **contenu** (`world.json`), **toutes les chaînes d'interface écrites en C#** sont
traduisibles : menus (« Que voulez-vous faire ? », « Inventaire »…), invites, en-têtes du
classement, marques de score (`[ÉVADÉ]`/`[MORT]`/`[À QUEL PRIX]`) et **l'intégralité des textes
des mini-jeux** (chifoumi de Mme Bernard, prof de sport, cantine, grenouille, orgue, charge du
téléphone). Même l'indice chiffré de Mme Bernard (« neuf moins deux ») est traduit.

Modèle **gettext** : la chaîne française est à la fois la **clé** et le **repli**. Chaque langue
a un fichier plat **`ui_<langue>.json`** = un dictionnaire `"français" → "traduit"` :

```json
{
  "Que voulez-vous faire ?": "What do you want to do?",
  "Inventaire": "Inventory",
  "Score actuel : {0} pts": "Current score: {0} pts"
}
```

- Une **clé absente** (ou valeur vide) → le français s'affiche. Rien à supprimer, rien à casser.
- **Conserve exactement** les placeholders `{0}`, `{1}`, `{NOM}`… et les balises `[rouge]…[/rouge]`,
  `[gris]…[/gris]`, etc. (mêmes règles que `world.json`).
- Chargement identique aux mondes : `ui_<langue>.json` **à côté de l'exe** d'abord (test sans
  recompiler), sinon **embarqué**. Noms avec **underscore** (jamais `.en.` → assemblys satellites).
- Ajouter une langue = ajouter `ui_<langue>.json` **et** une ligne `<EmbeddedResource>` dans le
  `.csproj` (à côté de `ui_en.json` / `ui_pl.json`).

### Produire un `ui_<langue>.json` avec une IA

> Traduis les **valeurs** de ce dictionnaire `ui_fr` en **anglais** (garde chaque clé française
> **inchangée**). Résultat **UTF-8 valide**, mêmes clés. **Conserve exactement** les placeholders
> `{0}`, `{1}`, `{NOM}` et les balises de couleur `[rouge]…[/rouge]`, `[gris]…`, etc.

### Vérifier la parité des `ui_<langue>.json`

```
python3 -c "import json;a=json.load(open('ChateauEcole.ConsoleApp/ui_en.json'));\
b=json.load(open('ChateauEcole.ConsoleApp/ui_pl.json'));\
print('même jeu de clés' if set(a)==set(b) else 'CLÉS DIFFÉRENTES')"
```

## Reste à faire

Le jeu est **entièrement multilingue** (contenu + interface + mini-jeux) en **fr / en / pl**.
Pour l'**italien** : produire `world_it.json` + `ui_it.json`, ajouter `"it"` dans `Langues` et
`LangChoix` (Program.cs) et les deux `<EmbeddedResource>` correspondants.
