# PID_INT — conversion depuis le PI de MKR

**Statut : documentaire, NON QUALIFIÉ.** Source : Schneider Electric, *EcoStruxure Control Expert — Bibliothèque de blocs standard*, 33002520, édition 09/2020 (.20), chapitre 30, pages 135 à 141, fourni par le mainteneur le 24/11/2026. Les équations de la page 141 sont des images, lues sur ces images.

## Ce que le document établit
- Entrées et sortie en entiers normalisés **[0..10000]** (mesure, consigne, `PID_Out`).
- ε = SP − PV (schéma p. 137).
- Sans intégrale (TI = 0) : OUT = KP·ε/100 + 5000. Avec intégrale : ΔOUT = KP·[Δε + TS/(10·TI)·ε]/100, OUT = OUT + ΔOUT.
- Unités des registres : KP ×100 (−10000 à +10000), TI en dixièmes de seconde (0 à 20000), TD en dixièmes (0 à 10000), TS en centièmes (1 à 32000, ramené à un multiple de la période de la tâche).

## Correspondance MKR → registres (PI seulement)
Avec les signaux en comptes, KP = Kp × étendue_PV / étendue_MV, puis registre KP = KP × 100, TI = Ti × 10, TS = Ts × 100. Le signe de Kp est conservé : l'équation donne la même action que MKR (u = Kp·(SP − PV)).
Les étendues physiques de la mesure et de la sortie (qui font [0..10000]) viennent de l'installation : MKR les exige, il ne les devine pas.

## Refusé, car le document ne le règle pas
- **Action dérivée** : le document écrit « Dt = action dérivée » sans équation ni filtre. Toute conversion avec Td > 0 est refusée.
- **Valeurs hors des plages documentées** des registres.

## Incohérences relevées dans le document
- Le texte écrit « TI < 0 » pour l'action intégrale (probable coquille pour TI > 0).
- La page 136 annonce un algorithme « série / parallèle » sans paramètre de choix ; l'équation est mixte.
- La page 136 cite l'échelle [0-1000] pour des signaux [0..10000].
- Les noms de sens « avant / arrière » ne sont pas définis ; MKR s'appuie sur l'équation, pas sur ces mots.

## Non reproduit (à qualifier sur banc)
Arithmétique entière et arrondis, limiteurs OUT_MAX/OUT_MIN, anti-saturation, mode sans à-coups, période TS arrondie à la tâche, décalage de 5000 quand TI = 0.

## Vérification
Un oracle écrit depuis l'équation par cycle de la page 141 pilote le même procédé que la simulation continue de MKR : l'écart de PV est divisé par deux quand le pas est divisé par deux (0,056 / 0,028 / 0,014 pour 0,4 / 0,2 / 0,1 s). Cela établit la forme de la conversion, pas la qualification du bloc.
