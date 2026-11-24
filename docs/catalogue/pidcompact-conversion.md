# PID_Compact V2.x et V3.x — conversion depuis le PID de MKR

**Statut : documentaire, NON QUALIFIÉ.** Source : Siemens, *PID control*, Function Manual, 11/2025, A5E35300227-AJ (chapitre 6.2 pages 88 à 93 ; chapitre 11.1 pages 245 à 271), fourni par le mainteneur le 24/11/2026. Les équations des pages 88 et 91 sont des images, lues sur ces images. Aucune règle absente de ce document n'est utilisée. TIA Portal n'est pas disponible : rien n'est vérifié sur banc.

## Équation (V2 p. 88, V3 p. 91, zone morte désactivée)
y = Kp · [ (b·w − x) + 1/(Ti·s) · (w − x) + Td·s / (a·Td·s + 1) · (c·w − x) ]
avec b = `PWeighting`, c = `DWeighting`, a = `TdFiltRatio` (retard T1 = a × Td). C'est un PIDT1 avec anti-windup.

## Correspondance MKR → bloc
| MKR | PID_Compact |
|---|---|
| \|Kp\| × (% de sortie par unité de MV) | `Retain.CtrlParams.Gain` (≥ 0) |
| Ti, Td en secondes | `Ti`, `Td` (0 à 100000 s) |
| Tf, filtre de dérivée | `TdFiltRatio` = Tf / Td |
| proportionnelle sur l'écart | `PWeighting` = 1 |
| dérivée sur la mesure seule | `DWeighting` = 0 |
| signe de Kp | `Config.InvertControl` : un Kp négatif donne TRUE, car le bloc refuse un gain négatif (p. 92, 270) |

La sortie du bloc est en pourcentage : MKR exige le nombre de pour cent que vaut une unité de MV (1 si la MV est déjà en %).

## Différences V2.x et V3.x
- **Équation :** identique, hors la **zone morte** de V3 (`DeadZone`, réglée à la main, jamais calculée par le réglage). MKR l'impose à 0.
- **V3.0 :** limitation active de l'action intégrale (changement de limites de sortie en automatique) ; utilisable sur S7-1200 G2.
- **V2.3 / V2.4 :** réinitialisation de l'intégrale par défaut à la bascule Inactif → Automatique (IntegralResetMode = 4) ; V2.4 : signe de la sortie modifié avec OverwriteInitialOutputValue et commande inversée.

## Compatibilité matériel (p. 246-247, 11.1.2)
| CPU | Micrologiciel | Versions V2.x / V3.x |
|---|---|---|
| S7-1200 | ≥ 4.2 | V2.2, V2.3 |
| S7-1200 | 4.0 à 4.1 | V2.2 |
| S7-1200 G2 | ≥ 1.0 | V3.0 |
| S7-1500 | ≥ 3.1 | V2.4, V3.0 |
| S7-1500 | 3.0 | V2.4 |
| S7-1500 | 2.5 à 2.9 | V2.0 à V2.4 |
| S7-1500 | 2.0 à 2.1 | V2.0 à V2.3 |
| S7-1500 | 1.5 à 1.8 | V2.0 à V2.2 |
| S7-1500 | 1.1 | V2.0, V2.1 |
| S7-1500 | 1.0 | V2.0 |

Les S7-1200 de micrologiciel inférieur à 4.0 n'ont que V1.x : hors périmètre. Si l'utilisateur renseigne la famille de CPU et le micrologiciel, MKR refuse une version absente de ce tableau. Les versions de TIA Portal compatibles ne sont pas lues.

## Refusé, car le document ne le règle pas
- Version hors V2.0 à V2.4 et V3.0.
- Dérivée sans filtre : a = 0 rend la dérivée « efficace un seul cycle » (p. 93) ; elle ne reproduit pas la dérivée idéale.
- Ti = 0 (l'intégrale serait désactivée).

## À déterminer sur l'installation
`Cycle` (multiple entier de CycleTime), limites de sortie, mise à l'échelle de la mesure, mode de réinitialisation de l'intégrale et comportement sur erreur. Les limiteurs, l'anti-windup et l'échantillonnage réels ne sont pas reproduits : à qualifier sur banc.

## Vérification
Identité algébrique de la fonction de transfert avec la forme idéale de MKR (b = 1, c = 0), 4 fréquences, précision 1e-12 ; signe, échelle en pourcentage, tableau de compatibilité et refus testés.
