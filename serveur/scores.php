<?php
// Château École - classement en ligne. Compatible PHP 4 (Free perso, 4.4.3).
// Stockage : lignes "score<TAB>nom<TAB>mark<TAB>datetime" dans scores.dat (append + verrou).
// La date+heure vient du JOUEUR (heure locale envoyée par le jeu) ; repli = heure serveur.
// Sortie : JSON construit à la main (PHP 4 n'a pas json_encode).
error_reporting(0);
header('Content-Type: application/json; charset=utf-8');

$FILE   = dirname(__FILE__) . '/scores.dat';  // le .dat doit être dans le MÊME dossier
$SECRET = 'cefir-chateau-2026';               // doit être IDENTIQUE dans le jeu
$MAX    = 96;                                 // score max réel -> rejet au-delà
$TOP    = 20;                                 // nombre d'entrées renvoyées au jeu
$KEEP   = 200;                                // entrées CONSERVÉES dans scores.dat (les meilleures)

function ce_cmp($a, $b) { return $b[0] - $a[0]; } // tri par score décroissant

// Retire les caractères de contrôle et ceux qui casseraient le stockage ou le JSON.
function ce_clean($v) {
    $v = preg_replace('/[\x00-\x1F\x7F]/', '', $v);
    return str_replace(array('\\', '"', '|', "\t"), '', $v);
}

// Enlève une séquence UTF-8 multioctet INCOMPLÈTE en fin de chaîne (après une coupe).
function ce_utf8_trim($s) {
    $len = strlen($s);
    if ($len == 0) return $s;
    $i = $len - 1;
    while ($i >= 0 && (ord($s[$i]) & 0xC0) == 0x80) $i--;
    if ($i < 0) return '';
    $lead = ord($s[$i]);
    if ($lead < 0x80) $need = 1;
    elseif (($lead & 0xE0) == 0xC0) $need = 2;
    elseif (($lead & 0xF0) == 0xE0) $need = 3;
    elseif (($lead & 0xF8) == 0xF0) $need = 4;
    else return substr($s, 0, $i);
    $have = $len - $i;
    if ($have < $need) return substr($s, 0, $i);
    return $s;
}

// Charge et parse toutes les lignes valides du fichier en tableau de [score, nom, mark, when].
function ce_load($file) {
    $rows = array();
    $lines = @file($file);
    if (is_array($lines)) {
        for ($i = 0; $i < count($lines); $i++) {
            $p = explode("\t", rtrim($lines[$i], "\r\n"));
            if (count($p) >= 4) $rows[] = array(intval($p[0]), $p[1], $p[2], $p[3]);
        }
    }
    return $rows;
}

// Élagage : ne conserve que les $keep MEILLEURS scores dans le fichier (borne sa taille tout en
// garantissant qu'au moins $keep entrées sont retenues). Réécriture atomique sous verrou.
// N'agit que si le fichier dépasse $keep, pour ne pas réécrire à chaque partie.
function ce_prune($file, $keep) {
    $rows = ce_load($file);
    if (count($rows) <= $keep) return;
    usort($rows, 'ce_cmp');
    $n = count($rows); if ($n > $keep) $n = $keep;
    $buf = '';
    for ($i = 0; $i < $n; $i++) {
        $buf .= $rows[$i][0] . "\t" . $rows[$i][1] . "\t" . $rows[$i][2] . "\t" . $rows[$i][3] . "\n";
    }
    $fp = fopen($file, 'w'); // 'w' : réécriture complète (PHP 4 : pas de mode 'c')
    if ($fp) {
        flock($fp, LOCK_EX);
        fwrite($fp, $buf);
        flock($fp, LOCK_UN);
        fclose($fp);
    }
}

// --- Écriture (POST) : append d'une ligne, sous verrou ---
if ($_SERVER['REQUEST_METHOD'] == 'POST' && isset($_POST['secret']) && $_POST['secret'] === $SECRET) {
    $name = isset($_POST['name']) ? ce_clean($_POST['name']) : '';
    $name = ce_utf8_trim(substr(trim($name), 0, 40));   // ~20 caractères accentués max
    $sc   = isset($_POST['score']) ? intval($_POST['score']) : -1;
    $mk   = isset($_POST['mark']) ? ce_utf8_trim(substr(ce_clean($_POST['mark']), 0, 24)) : '';
    // Date+heure LOCALE du joueur (envoyée par le jeu). Repli sur l'heure serveur si absente/invalide.
    $when = isset($_POST['when']) ? trim(ce_clean($_POST['when'])) : '';
    if (!preg_match('/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/', $when)) $when = date('Y-m-d H:i');
    if ($name != '' && $sc >= 0 && $sc <= $MAX) {
        $fp = fopen($FILE, 'a');
        if ($fp) {
            flock($fp, LOCK_EX);
            fwrite($fp, $sc . "\t" . $name . "\t" . $mk . "\t" . $when . "\n");
            flock($fp, LOCK_UN);
            fclose($fp);
        }
        ce_prune($FILE, $KEEP); // on borne le fichier aux 200 meilleurs
    }
}

// --- Lecture + sortie JSON (top N) ---
$rows = ce_load($FILE);
usort($rows, 'ce_cmp');

$n = count($rows); if ($n > $TOP) $n = $TOP;
$out = array();
for ($i = 0; $i < $n; $i++) {
    $out[] = '{"name":"' . $rows[$i][1] . '","score":' . $rows[$i][0]
           . ',"mark":"' . $rows[$i][2] . '","date":"' . $rows[$i][3] . '"}';
}
echo '[' . implode(',', $out) . ']';
