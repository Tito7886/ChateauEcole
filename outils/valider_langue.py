#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Vérifie qu'un world.<langue>.json est une traduction VALIDE de world.json :

  - même SQUELETTE (ids, flags, cibles de sortie, structure, nombres, booléens) -> sinon le
    jeu casse : ce sont des « problèmes » (✗) ;
  - mêmes PLACEHOLDERS {NOM}/{CODE...} et mêmes MARQUEURS [slow]/[pause]/[clear]/couleurs
    dans chaque texte -> sinon un effet ou un remplacement est perdu : ce sont des
    « avertissements » (⚠). Les nombres de [slow=NN]/[pause=N] peuvent différer (style).

Usage :
    python3 outils/valider_langue.py world.json world.en.json
Sortie : code 1 s'il y a au moins un problème structurel, 0 sinon.
"""
import sys
import json
import re

# Champs dont la VALEUR est du texte destiné au joueur (traduisible).
TEXT_FIELDS = {
    "name", "description", "text", "repeatText", "label", "transitionText",
    "lockedMessage", "deathWithoutItemMessage", "blockedWithoutItemMessage",
    "deadlyOnRepeatMessage", "deadlyOnRepeatWithoutItemMessage", "deadlyMessage",
    "resultText", "deathMessage", "companionText", "titleArt", "successText",
    "failText", "prompt", "bypassTransitionText",
}

problems = []   # cassants
warnings = []   # placeholders / marqueurs


def tokens(s):
    placeholders = sorted(re.findall(r"\{[^}]*\}", s))          # {NOM}, {CODE}...
    markers = re.findall(r"\[[^\]]*\]", s)                       # [slow], [rouge], [/rouge]...
    markers = sorted(re.sub(r"=\d+", "", m) for m in markers)    # [slow=200] -> [slow]
    return placeholders, markers


def check_text(path, a, b):
    pa, ma = tokens(a)
    pb, mb = tokens(b)
    if pa != pb:
        warnings.append(f"{path}: placeholders différents  FR={pa}  trad={pb}")
    if ma != mb:
        warnings.append(f"{path}: marqueurs différents  FR={ma}  trad={mb}")


def walk(path, a, b, key=None):
    if isinstance(a, dict):
        if not isinstance(b, dict):
            problems.append(f"{path}: objet attendu dans la traduction")
            return
        if key == "items":  # dict id -> texte
            if set(a) != set(b):
                problems.append(f"{path}: items différents  manquants={sorted(set(a)-set(b))}  en trop={sorted(set(b)-set(a))}")
            for k in set(a) & set(b):
                if isinstance(a[k], str) and isinstance(b[k], str):
                    check_text(f"{path}.{k}", a[k], b[k])
            return
        ka, kb = set(a), set(b)
        if ka != kb:
            problems.append(f"{path}: clés différentes  manquantes={sorted(ka-kb)}  en trop={sorted(kb-ka)}")
        for k in ka & kb:
            walk(f"{path}.{k}" if path else k, a[k], b[k], k)
    elif isinstance(a, list):
        if not isinstance(b, list):
            problems.append(f"{path}: liste attendue dans la traduction")
            return
        if len(a) != len(b):
            problems.append(f"{path}: longueur de liste différente  FR={len(a)}  trad={len(b)}")
        for i in range(min(len(a), len(b))):
            walk(f"{path}[{i}]", a[i], b[i], key)  # key = nom du champ liste (ex. 'rooms')
    elif isinstance(a, str):
        if key in TEXT_FIELDS:
            if not isinstance(b, str):
                problems.append(f"{path}: texte attendu")
            else:
                check_text(path, a, b)
        elif a != b:
            problems.append(f"{path}: valeur structurelle modifiée  FR={a!r}  trad={b!r}")
    else:
        if a != b:
            problems.append(f"{path}: valeur modifiée  FR={a!r}  trad={b!r}")


def main():
    if len(sys.argv) != 3:
        print("usage: valider_langue.py world.json world.<langue>.json")
        sys.exit(2)
    fr = json.load(open(sys.argv[1], encoding="utf-8"))
    tr = json.load(open(sys.argv[2], encoding="utf-8"))
    walk("", fr, tr)

    for w in warnings:
        print("  ⚠  " + w)
    for p in problems:
        print("  ✗  " + p)

    if problems:
        print(f"\n{len(problems)} problème(s) STRUCTUREL(S) — le jeu risque de casser. {len(warnings)} avertissement(s).")
        sys.exit(1)
    print(f"\nOK : structure conforme. {len(warnings)} avertissement(s) placeholders/marqueurs.")
    sys.exit(0)


if __name__ == "__main__":
    main()
