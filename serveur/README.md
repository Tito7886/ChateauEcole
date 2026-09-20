# Serveur du classement en ligne

`scores.php` : le classement en ligne de Château École, à déposer par **FTP** sur
l'hébergement perso Free (PHP 4). `scores.dat` (créé automatiquement) doit être dans
le **même dossier** et accessible en écriture.

- `$SECRET` doit être **identique** à celui du jeu (`Program.cs`, `ScoreSecret`).
- `$MAX` : score maximum accepté (rejet au-delà, anti-triche basique).
- `$TOP` : nombre d'entrées renvoyées au jeu (le jeu en affiche 10).
- `$KEEP` : nombre d'entrées **conservées** dans `scores.dat` (les 200 meilleures) ;
  le fichier est élagué après chaque envoi pour ne pas croître sans fin.

Le jeu contacte l'URL via `HttpScoreBoard` (best-effort : hors-ligne = scores locaux).
