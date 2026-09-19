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

## Reste à faire (phase 2)

Le **contenu** (`world.json`) est entièrement traduisible dès maintenant. Les **quelques chaînes
d'interface en C#** (menus « Que voulez-vous faire ? », « Inventaire », en-têtes du classement,
textes des mini-jeux) sont encore en français : elles seront extraites dans des fichiers
`ui.<langue>.json` (clés → texte) dans une seconde passe, sur le même principe (fr = repli).
