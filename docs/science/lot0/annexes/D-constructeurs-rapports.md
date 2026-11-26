> **Annexe du LOT 0 — rapport d'audit indépendant (sous-agent, lecture seule), révision auditée c7915cd.** Les constats ont été vérifiés par échantillonnage par le responsable du lot (voir 00-synthese-audit.md, §1) ; tout constat non cité dans la synthèse reste à confirmer avant d'être retenu.

# Lot 0 — D : Constructeurs, blocs, conversions et rapports

Dépôt `/home/user/mkr`, branche `claude/science-engine-dev`, révision `c7915cd` (vérifiée par `git log`). Revue en lecture seule : aucun fichier du dépôt modifié (`git status` propre après les tests, `__pycache__` ignoré par `.gitignore:3`).

Convention : **[F]** = fait vérifié (fichier:ligne ou exécution), **[H]** = hypothèse ou jugement non prouvé dans le dépôt.

Tests ciblés lancés (`python3 -I -m pytest -q -p no:cacheprovider`) : `test_block_catalogue.py` 10 réussis, `test_pidff_conversion.py` 9 réussis (+5 sous-tests), `test_pidint_conversion.py` 7 réussis (+3), `test_pidcompact_conversion.py` 10 réussis (+8). Les tableaux d'écarts publiés dans `docs/catalogue/pidff-conversion.md:58-62` et `pidint-conversion.md:29` ont été recalculés et concordent (0,0064/0,0032/0,0016 ; 0,0051/0,0025/0,0013 ; 0,0562/0,028/0,014).

---

## 1. Synthèse

1. Trois adaptateurs existent : **PIDFF** (Schneider, bibliothèque Control), **PID_INT** (Schneider, bibliothèque Standard) et **PID_Compact V2.0 à V2.4 et V3.0** (Siemens) (`engine/block_adapters.py:35, 93, 170`). Ils dépendent tous d'une seule lecture de document et ne sont **jamais qualifiés** : `QUALIFICATION = "non_qualified"` et `readyToLoad=False` sont codés en dur (`block_adapters.py:12, 71, 124, 209`), et le contrat C# vérifie ces deux valeurs (`PythonDiagnostics.cs:428-432, 450-453`).
2. Aucune fiche du catalogue n'a le statut `verified`. Les sept sont à `to_verify` (`schneider.json:217, 329, 349, 400, 452, 545, 661`), et `selectable` vaut toujours `false`. **Aucune** CPU, aucun firmware, aucune version de Control Expert, de TIA Portal ou de bibliothèque Schneider n'est renseigné : tous ces champs sont `null`.
3. **Défaut majeur (masquage possible d'incompatibilité) [F] :** le filtrage par cible est décoratif.
   - `lookup` traite un champ `null` comme un joker (`block_catalogue.py:29-30`), et `convert` convertit dès qu'une entrée correspond (`block_catalogue.py:49-57`).
   - Résultats d'exécution : PID_Compact converti pour `cpu="M580"` → `converted=True` ; PIDFF converti pour `cpu="S7-1500"` → `True` ; cible `libraryVersion="V3.x"` avec des paramètres `version="V2.4"` → `True`, la source annonçant V2.4 ; V3.0 sur `firmware="2.0"` fourni dans la cible (et non dans les paramètres) → `True`.
   - Côté C#, la cible envoyée est **toujours vide** (`PythonDiagnostics.cs:421` et `:443` : `target = new { }`). Le catalogue ne contrôle donc jamais rien sur le parcours utilisateur.
4. L'interface n'expose que des conversions de **PI** : les trois appels C# passent `td=0` et `filterSeconds=null` (`IdentificationWorkspace.cs:209, 229, 241-244`). La conversion PID avec dérivée n'est disponible que dans le moteur.
5. Le libellé exact « Conversion non qualifiée » n'existe nulle part (`grep` vide). Les libellés réels sont :
   - « NON QUALIFIÉ : valeurs documentaires à relire et à saisir à la main. Aucune n'est prête à charger. » (`BlockConversion.cs:35, 94`) ;
   - le bandeau `ui.valeurs_documentaires_non_qualifiees_*` (`Texts/ui.fr.json:492-493`) ;
   - le statut « Valeurs affichées. Non qualifiées. » (`cs.fr.json:726`).
6. **Aucune écriture vers l'automate [F]** :
   - Modbus n'émet que les fonctions `{0x03, 0x04}` (`Plc/ModbusTcpReader.cs:14`) ;
   - S7 n'émet que `{0xF0, 0x04}`, avec un garde avant chaque trame (`Plc/S7Reader.cs:17, 122`) ;
   - un test vérifie les fonctions S7 (`tests/Mkr.Communication.Tests/Program.cs:414`) ;
   - aucune conversion n'est transmise à un automate : affichage et presse-papiers seulement.
7. Le rapport PDF « Fiche de mise au point » ne contient **aucune valeur de bloc**. Sa section « Bloc et mise au point » est une phrase fixe (`StudySheet.cs:115-116`). Il n'existe ni fiche de préparation de mise au point, ni fiche de paramètres persistée : seulement du texte copiable (relevé de banc, valeurs de bloc).
8. Le cahier des charges n'est pas dans le dépôt. `docs/requirements/traceabilite.md:3` indique que seuls les §§1-19 de MKR-SPEC-001 (14 pages) ont été examinés. **Les sections 22 et 24 ne sont donc pas vérifiables ici** : je les compare au contenu attendu exprimé par le dépôt lui-même (REP-001 §13, `audit-refonte.md:29, 83`, `kit-qualification.md`) et aux éléments de la demande.

---

## 2. Fiches bloc par bloc

### 2.1 Schneider — PIDFF (Control Expert, bibliothèque « Control »)

| Rubrique | Contenu dans le dépôt | Réf. |
|---|---|---|
| Constructeur / famille | Schneider Electric, Control Expert, bibliothèque Control | `schneider.json:163-166`, `block_adapters.py:13-14` |
| Référence documentaire | « Schneider Electric, EcoStruxure Control Expert - Régulation - Bibliothèque de blocs, 33002536 », édition « 09/2020 (33002536.16) », chap. 17, p. 167-187 ; PDF fourni par le mainteneur et lu le 2026-11-24 | `block_adapters.py:13-14`, `schneider.json:116-122` |
| CPU / firmware / version logicielle / version de bibliothèque | **null** partout : « la version de Control Expert, le firmware et la CPU restent à identifier » | `schneider.json:167-170, 121` |
| Structure mathématique | Mixte : `OUT = kp × [1 + 1/(ti·p) + td·p/(1+(td/kd)·p)] × IN`, IN = PV − SP (p. 173). Parallèle avec α = (out_sup−out_inf)/(pv_sup−pv_inf) consignée, mais **refusée** | `schneider.json:171`, `block_adapters.py:47-50` |
| Algorithme par cycle | Incrémental pour ti > 0 et aw_type = 0 (p. 183), transcrit d'images | `schneider.json:281` |
| Convention de gain | kp ≥ 0 en unités physiques ; le signe passe par `rev_dir` ; un kp négatif donne le statut 080 | `block_adapters.py:40-42, 73-74` |
| Unités | Unités physiques (p. 168) ; ti et td de type TIME, convertis en `T#…ms` | `schneider.json:305`, `block_adapters.py:25-32` |
| Ti / Td | ti = Ti ; td = Td ; kd = td/Tf, refusé si kd < 1 | `block_adapters.py:56-64` |
| Sens d'action | rev_dir = 1 : PV−SP > 0 fait monter la sortie (p. 182). L'incohérence des libellés p. 171 / p. 182 est signalée par l'avertissement `label_inconsistency_rev_dir` | `schneider.json:293, 297`, `block_adapters.py:67` |
| Période d'exécution | `dt` = temps écoulé depuis la dernière exécution ; renvoyé à « à déterminer » ; **jamais demandé dans l'interface** | `block_adapters.py:76` |
| Filtrage | Dérivée filtrée au 1er ordre, constante td/kd (p. 173, 178) ; dérivée sur la mesure (`pv_dev=0`) | `schneider.json:305` |
| Saturation | out_min/out_max en automatique, out_inf/out_sup en manuel et suivi (p. 179) ; outrate ≥ 0 (p. 172) ; tout est « à déterminer » | `schneider.json:309`, `block_adapters.py:76` |
| Anti-windup | `aw_type` mentionné (FAQ000279186) ; **non modélisé**, « à déterminer » | `schneider.json:224-225`, `pidff_reference.py:7-9` |
| Tracking / modes / manuel-auto | TR_S prioritaire sur MAN_AUTO ; bump = 0 sans à-coup (p. 172, 180) ; non reproduit | `schneider.json:313`, `pidff-conversion.md:52` |
| Initialisation | Non documentée pour PIDFF (rien sur le démarrage à froid) | — |
| Valeurs neutres imposées | ovs_att = 0, dband = 0, ff_sup = ff_inf, outbias = 0, en_rcpy = 0 | `block_adapters.py:75` |
| Niveau réel | **Documenté + conversion implémentée + équivalence de forme vérifiée numériquement**, sur PI et PID, gain ±. **Non vérifié sur banc. Non qualifié.** | — |
| Oracle | `engine/pidff_reference.py`, écrit par le projet depuis les équations du manuel (oracle de test seulement), comparé à `pid_simulation.calculate` de MKR. **Il n'est pas indépendant** : même lecteur, même transcription d'images, et l'interprétation de `TermD` est « la seule cohérente » avec p. 173, retenue parce qu'elle converge (`pidff_reference.py:11-14`). Pas de simulateur Control Expert ni d'automate. | `test_pidff_conversion.py:178-229` |

Particularités et défauts relevés :

- **[F] Sources contradictoires dans le catalogue :**
  - le fait `schneider.json:277` dit « Document non lu : aucune équation n'en est tirée », alors que les faits 281-313 et la source (`:121`) disent qu'il a été lu ;
  - les paramètres renvoient à `SE-PIDFF-167` = document **33002535**K01000, « p. 167 et paramétrage p. 172–173 » (`:34-40`), alors que les équations viennent de **33002536** aux mêmes numéros de pages ;
  - `blocs-observes.md:75` dit encore « l'aide PIDFF est le document 33002535 ».
  - [H] 33002535 et 33002536 sont probablement deux langues ou deux éditions du même manuel : à confirmer, puis à unifier.
- **[F] Règles reprises d'une FAQ « relayée par un moteur de recherche, à relire à la source »** (`schneider.json:76`) mais appliquées comme règles bloquantes : le refus `kd < 1` (`block_adapters.py:63-64`) et le refus de kp < 0 / statut 080 (`:42`).
- **[F] Résidus d'extraction de texte dans des faits affichés :** « Le PI_B (p. 4805 du texte) » (`schneider.json:297`) et « (p. 245 ss, 12655) » (`:561, :673`). Ce sont des numéros de ligne d'un texte extrait, pas des pages.
- **[F] `_time_literal`** limite TIME à 2³² ms (`block_adapters.py:27-28`) **sans page citée**. [H] La plage de TIME dans Control Expert est probablement non signée sur 32 bits : la source manque.
- **[F] Les valeurs neutres sont codées en dur dans le texte C#** (`BlockConversion.cs:46`) au lieu d'être lues dans `Neutral`. Si le moteur change, le texte affiché ne suit pas.
- **[F] Le texte de `Describe`** affiche toujours « (dérivée sur la mesure) » (`BlockConversion.cs:44`) ; c'est vrai aujourd'hui.

### 2.2 Schneider — PID_INT (Control Expert, bibliothèque « Standard »)

| Rubrique | Contenu | Réf. |
|---|---|---|
| Référence | « Schneider Electric, EcoStruxure Control Expert - Bibliothèque de blocs standard, 33002520 », « 09/2020 (33002520.20) », chap. 30, p. 135-141 | `block_adapters.py:15-16`, `schneider.json:124-131` |
| CPU / version | null | `schneider.json:357-360` |
| Structure | Incrémentale mixte : `DOUT = KP[Δε + TS/(10·TI)·ε]/100`, ε = SP − PV ; TI = 0 donne `OUT = KP·ε/100 + 5000` | `schneider.json:361`, `block_adapters.py:130-133` |
| Gain / unités | Signaux INT [0..10000] ; registre KP = Kp·pvSpan/mvSpan ×100 (±10000) ; TI en 0,1 s (1..20000) ; TS en 0,01 s (1..32000) | `block_adapters.py:111-116` |
| Sens | Signe de KP conservé (« négatif : avant, positif : arrière ») | `schneider.json:408`, `block_adapters.py:98-99` |
| Td | **Refusé** : la dérivée Dt n'a pas d'équation dans le document | `block_adapters.py:109-110` |
| Période | TS est ramené au multiple de la période de tâche le plus proche ; la période de tâche n'est pas demandée (avertissement seulement) | `schneider.json:428`, `block_adapters.py:117` |
| Saturation / AW / bumpless | OUT_MAX, OUT_MIN, NO_BUMP « à déterminer » ; non modélisés | `block_adapters.py:128` |
| Initialisation | Démarrage à froid : mode manuel, sortie à 0 | `schneider.json:432` |
| Incohérences du document | « TI < 0 », « série / parallèle » sans paramètre de choix, échelle [0-1000] contre [0..10000] | `schneider.json:416-424` |
| Niveau | Documenté + conversion implémentée (PI) + convergence numérique. **Non qualifié.** | — |
| Oracle | **Faible** : l'oracle du bloc et la référence « MKR » sont tous deux écrits dans le fichier de test (`test_pidint_conversion.py:12-38`). La référence n'est pas la simulation de production de MKR. L'arithmétique est flottante, pas entière. La tolérance est large : erreur < 0,6 pour une consigne de 10, soit 6 % (`:63`). | — |

Défauts :

- **[F] Arrondi bancaire Python** (`round`) des registres (`block_adapters.py:90`) : 12,5 donne 12. [H] Cela peut différer de l'arrondi appliqué par la saisie dans l'automate. L'avertissement `register_rounding` se déclenche au-delà de 0,1 %.
- **[F] Unités des registres masquées à l'affichage :** `BlockSettings.Describe` saute les valeurs de type objet (`BlockConversion.cs:97`). Le dictionnaire `units` (KP ×100, TI en 0,1 s, TS en 0,01 s ; `block_adapters.py:126`) **n'est jamais affiché**, alors que `gain` brut est affiché sans unité.
- **[F] `blocs-observes.md:69`** dit encore « 33002520K01000 pour PID_INT (non lu) », ce qui contredit le §5 du même fichier (`:84`).
- [H] La disponibilité de PID_INT sur M580 n'est pas documentée dans le dépôt.

### 2.3 Siemens — PID_Compact V2.x / V3.x (TIA Portal)

| Rubrique | Contenu | Réf. |
|---|---|---|
| Référence | « Siemens, PID control, Function Manual, A5E35300227-AJ », 11/2025, chap. 6.2 (p. 88-93) et 11.1 (p. 245-271), lu le 2026-11-24 ; l'URL est générique (`support.industry.siemens.com/`) | `block_adapters.py:137`, `schneider.json:152-158` |
| Versions | V2.0 à V2.4 et V3.0 acceptées ; table de compatibilité CPU/firmware (p. 246-247) | `block_adapters.py:139-150, 183` |
| Structure | `y = Kp[(b·w−x) + 1/(Ti·s)(w−x) + Td·s/(a·Td·s+1)(c·w−x)]` ; b = 1, c = 0, a = Tf/Td | `block_adapters.py:173-176, 211` |
| Gain / unités | Gain = \|Kp\| × outputPercentPerMvUnit (sortie en %) ; Ti et Td en s (≤ 100000) | `block_adapters.py:191-192, 211` |
| Sens | `InvertControl = Kp < 0` (p. 92, 270) | `block_adapters.py:212` |
| Période | `Cycle` (multiple de CycleTime) « à déterminer » | `block_adapters.py:214` |
| Saturation / AW / reset intégral | « limits_and_antiwindup_unqualified » ; IntegralResetMode (V2.3 et suivantes) et le changement de signe de V2.4 sont décrits dans les faits **mais pas dans les avertissements** | `schneider.json:577`, `block_adapters.py:205` |
| V3 | DeadZone imposée à 0 ; limitation active de l'intégrale non reproduite | `block_adapters.py:206-212` |
| Niveau | Documenté + conversion implémentée. **Aucun oracle temporel.** | — |
| « Vérification » | Identité algébrique de fonctions de transfert à 1e-12 (`test_pidcompact_conversion.py:12-25`). **C'est tautologique** : la même formule avec les valeurs substituées. Aucun oracle échantillonné, pas de PLCSIM, pas de banc. | — |

Défauts :

- **[F] La compatibilité matérielle n'est contrôlée que si l'utilisateur renseigne la CPU**, champ marqué « facultatif » dans l'interface (`BlockTargetView.xaml:38-46`, `BlockTargetView.xaml.cs:56-61`). Sans CPU, aucun avertissement « compatibilité non vérifiée » n'est ajouté (`block_adapters.py:185-188, 205`). C'est un masquage possible.
- **[F] Trois entrées PID_Compact portent le même nom** dans la fenêtre catalogue : la liste affiche `Name` (`BlockCatalogueWindow.xaml`, `DisplayMemberPath="Name"`) et la carte n'affiche pas `libraryVersion` (`BlockCatalogueWindow.xaml.cs:114-115`). Elles ne se distinguent pas. Le relevé de banc n'indique pas non plus la version (`:70-72`).
- **[F] Commentaires périmés :** `test_block_catalogue.py:99` dit « PID_Compact: documentation not read yet » ; `qualification-bloc-banc.md:5` dit « aucune équation de ces blocs n'est enregistrée » ; `:27` parle d'un niveau de preuve « relayé ».

### 2.4 Autres entrées et blocs absents

| Bloc | État [F] |
|---|---|
| PI_B (Schneider) | Une seule affirmation, tirée du catalogue Premium 2012 ; library, equation et parameters vides (`schneider.json:318-336`) ; conversion refusée (`test_pidff_conversion.py:261-264`) |
| PIFINT (Schneider) | **Aucun fait, aucune source** (`schneider.json:338-351`), mais affiché dans le catalogue |
| PID_Compact sans version | Faits « relayés » et capture d'écran ; pas d'équation |
| « EB_PID » (bloc vu en capture) | Identité non établie ; aucune fiche (`blocs-observes.md:67-71`) |
| Siemens PID_3Step, PID_Temp ; Rockwell PID, PIDE ; ABB | **Absents** du catalogue (ils ne sont cités que dans `qualification.md:3` et `feuille-de-route.md:17`) |
| Outils de lecture de type `lookup_blocks` / `recommend_blocks` | Opérations IPC existantes (`mkr_engine.py:46-49`), **jamais appelées depuis C#** (`grep` vide). La fenêtre catalogue lit le JSON directement (`BlockCatalogueWindow.xaml.cs:50`) et ignore `selectable` |

---

## 3. Ce qui est exposé à l'utilisateur (Desktop)

- **Valeurs PIDFF** (`IdentificationView.xaml.cs:625-645`, `IntegratorWindow.xaml.cs:84-97`) : Lambda et SIMC en PI uniquement, affichés dans `BlockValuesWindow`. Aucune saisie de CPU, de version, de période `dt`, des plages ou de l'unité de OUT.
  - **[F]** Aucun avertissement ne dit que l'OUT du PIDFF doit être exprimé dans l'unité de la MV de l'étude : kp est en « mvUnit/pvUnit » (`BlockConversion.cs:37`).
  - [H] Si la MV a été enregistrée en % et que le bloc travaille en Hz, kp est faux d'un facteur d'échelle, sans alerte.
- **Valeurs PID_INT / PID_Compact** (`BlockTargetView`) : étendues PV/MV et TS, ou version, %/unité, CPU facultative et firmware. Rien n'est pré-rempli (`BlockTargetView.xaml.cs:1-2`).
- **Catalogue** (`BlockCatalogueWindow`) : cartes avec le statut « Compatibilité à vérifier », les preuves et leurs sources, et le bouton « Copier le relevé de banc ».
- **[F] Contradiction d'ergonomie :** le bandeau dit « jamais par copie automatique » (`ui.fr.json:492`), mais `BlockValuesWindow` propose « Copier » vers le presse-papiers (`BlockValuesWindow.xaml.cs:15-26`).
- **[F] Textes de conversion codés en dur en français** dans `Mkr.Core` (`BlockConversion.cs:34-79, 93-144`) : ils ne sont pas localisés, contrairement au reste de l'interface. Les nombres sont formatés en `G6` avec la culture courante, donc avec une virgule décimale en français (`BlockConversion.cs:28, 111`). [H] Risque de ressaisie : Control Expert et TIA attendent un point décimal pour les REAL.
- **[F] Code de refus non traduit :** `no_matching_entry` n'est pas traduit (`BlockConversion.cs:69-79`) et s'affiche « Conversion refusée : no_matching_entry ». Cas inaccessible aujourd'hui, puisque la cible est vide.
- **[F] Traçabilité :** le journal d'audit enregistre « block.values.shown » sans les valeurs elles-mêmes (`IdentificationWorkspace.cs:229-230, 244-245`). Les paramètres présentés ne sont ni persistés ni retrouvables.
- **[F]** `pidff_reference.py` (oracle de test) est livré dans le dossier `engine` de l'application (`src/Mkr.Desktop/bin/*/engine/pidff_reference.py`), sans être utilisé hors des tests.

---

## 4. Rapport PDF, fiche de préparation, fiche de paramètres

### 4.1 Contenu actuel de `StudySheet.Export` [F]

Le rapport n'est générable que pour une étude PI FOPDT (IMC/SIMC) avec sa simulation : la signature exige `PiStudy` et `PiSimulationStudy` (`StudySheet.cs:14-16`). Il n'existe pas pour un intégrateur, un SOPDT/PID ou un ARX.

Contenu présent :
- en-tête (logo, « ÉTUDE DE RÉGULATION »), nom du projet, date de la simulation (`:28-42`) ;
- fiche de boucle : repère, grandeur, point de fonctionnement, objectif, description (`:43-56`) ;
- modèle : famille, K, τ, θ et unités ; pour chaque essai (estimation et validation) : fichier, colonnes, plage, nombre d'échantillons, RMSE, MAE, origine d'acquisition, lacunes et SHA-256 (`:57-78`) ;
- choix du modèle et seuils (`:79-87`) ; décision de validation acceptée ou refusée (`:88-95`) ;
- réglages PI Lambda et SIMC : Kp, Ti, marges, IAE, ISE, durée de saturation, erreur finale. Ils sont explicitement **« SIMULÉS sur le modèle identifié ; ils ne sont pas des performances mesurées »** (`:96-107`) ;
- conditions de simulation : consigne, pas, durée, bornes et vitesse de MV, perturbation (`:108-114`) ;
- « Bloc et mise au point » : **phrase fixe**, « Correspondance constructeur non établie… à vérifier sur banc » (`:115-116`) ;
- traçabilité : identifiants et versions d'algorithme et de moteur ; avertissements sous forme de **codes bruts** ; revues avec les **noms d'énumération anglais** (`Calculated`, `Verified`…) (`:117-125`) ;
- page « Validation indépendante » : mesure observée contre prédiction (`:130-153`) ; page « Réponses simulées » PV et MV (`:154-178`).

Distinction mesuré / simulé :
- **[F]** Elle est correcte : le graphique de validation est étiqueté « Mesure observée » contre « prédiction », et les indicateurs de réglage sont dits simulés.
- **Il n'existe aucune performance mesurée en boucle fermée**, puisque le rapport ne prévoit pas d'essai réel.

Statuts de validation :
- **[F]** Le rapport donne la décision sur le modèle et la liste des revues.
- Il ne donne pas de tableau des statuts par étape (`ReviewStage` Data/Model/Strategy/Block/Tuning/Trials, `StudyReview.cs:3`).
- Il ne mentionne pas le statut de qualification d'un bloc, ni l'absence d'écriture vers l'automate.

### 4.2 Manques du rapport

Le dépôt les reconnaît en partie lui-même (`audit-refonte.md:29, 83`, Lot D). La liste de la section 24 n'est pas dans le dépôt : à confronter au texte du cahier des charges.

| Élément | État |
|---|---|
| Valeurs de bloc constructeur, avec la mention « non qualifiée » et la source (doc, page, édition, version) | **Absent** |
| Cible matérielle : CPU, firmware, version de Control Expert ou TIA, bibliothèque | **Absent** (inconnus dans le catalogue) |
| Version du catalogue | **Absent** |
| Version de l'application MKR / empreinte du paquet | **Absent** (moteur seulement) |
| Date et auteur de l'export | **Absents** (seule la date de simulation figure) ; l'export est tracé dans l'audit (`IdentificationWorkspace.cs:20`) |
| ITAE, dépassement, temps d'établissement et de montée | Absents du PDF (ITAE est calculé ailleurs selon `feuille-de-route.md:15`) |
| Effort actionneur détaillé, vitesse de la MV dans les résultats | Partiel (durée de saturation seulement) |
| Comparaison réglage actuel / proposé | Absente |
| Statuts lisibles en français, avertissements traduits | Absents (énumérations et codes bruts) |
| Pagination, numéro de page, mention « aucune écriture automate » | Absents |
| Références bibliographiques des méthodes | Absentes |

### 4.3 Fiche de préparation de mise au point (section 22)

**[F] Elle n'existe pas en tant que document.** L'équivalent le plus proche est le « relevé de banc », un texte copié dans le presse-papiers depuis le catalogue (`BlockCatalogueWindow.xaml.cs:68-113`, `docs/catalogue/qualification.md:7-9`).
- Ce qu'il contient : version du catalogue, constructeur et logiciel, contexte de boucle, champs vides (CPU, firmware, logiciel, bibliothèque, instance et période, plages et unités PV/MV, sens d'action, unités de Ti/Td), nom de chaque paramètre suivi de « valeur observée : », et une rubrique essai et revue.
- Ce qui lui manque :
  - il n'est **pas relié à une étude** : il ne reprend ni l'identifiant de l'étude, ni le modèle, ni les **valeurs proposées par MKR**, ni la tolérance ;
  - il n'est ni persisté, ni exporté en PDF ;
  - il n'est pas mentionné dans l'audit ;
  - il ne liste pas les essais E0 à E7 et leurs critères (`qualification-bloc-banc.md:29-41`, `kit-qualification.md`).
- Le contenu exact de la section 22 est non vérifiable (cahier absent).

### 4.4 Fiche de paramètres

**[F] Elle n'existe pas comme livrable.** C'est le texte de `BlockValuesWindow` ou de `BlockTargetView`, éphémère et copiable. Ce texte n'apparaît pas dans le PDF, et ses valeurs ne sont pas enregistrées dans l'audit (§3).

### 4.5 Absence d'écriture automate

**[F] Confirmée** (§1, point 6, et `docs/exploitation.md:25`). Aucune conversion ne porte d'adresse ni de mécanisme de chargement. Le contrat refuse `readyToLoad=true`.

---

## 5. Scénario « pression, pompe à variateur, Schneider M580 »

Ce qui existe :
- acquisition Modbus TCP 03/04 compatible M340/M580 (`Acquisition.cs:13`) ;
- identification FOPDT / intégrateur et réglage PI ;
- valeurs documentaires **PIDFF** (mixte, PI) ou **PID_INT** (PI, en comptes 0..10000).

Ce qui manque ou bloque :
1. **Aucune preuve de compatibilité M580** : le catalogue ne contient aucune CPU, aucun firmware, aucune version de Control Expert ni de bibliothèque. Le document 33002536 est de 09/2020, sans lien avec une version de Control Expert installée.
2. **PIDFF : paramètres structurants non fournis par MKR**, tous « à déterminer » : période d'exécution `dt` (tâche MAST ou FAST), `out_min`/`out_max` (vitesse minimale de la pompe, risque de désamorçage), `outrate`, à cohérer avec la rampe du variateur, `aw_type`, `bump`, et les plages `pv_inf`/`pv_sup`/`out_inf`/`out_sup`. [H] Pour une boucle de pression rapide, `dt` comparé au retard et l'écart d'ordre dt/ti (avertissement `integral_uses_current_deviation`) peuvent être significatifs.
3. **Unité de OUT** : aucun contrôle que l'OUT du PIDFF (Hz, %, tr/min) correspond à l'unité de la MV identifiée.
4. **Actionneur** : `ActuatorProfile` (saturation, vitesse, zone morte, hystérésis) existe (`EquipmentProfiles.cs:10-14`) mais ne sert qu'à valider `PhysicalLoop`. Il n'alimente ni la conversion, ni `toDetermine`, ni le rapport.
5. **Anti-windup, suivi, passage manuel → automatique, démarrage** : non modélisés pour PIDFF ; essais E5 et E6 à faire.
6. **Sens d'action** : sur une pompe de surpression, le gain du procédé est positif, donc rev_dir = 0. Le mapping est documenté (p. 182), mais l'incohérence des libellés impose l'essai E4.
7. **Règles FAQ relayées** (kd < 1, statut 080) à relire à la source.
8. **Requis pour qualifier** : identifier la CPU M580 et son firmware, la version de Control Expert et de la bibliothèque Control ; lire la doc de cette version ; dérouler E0-E7 sur banc ou sur le simulateur Control Expert avec un rapport signé (`qualification-bloc-banc.md:43-45`) ; ajouter un oracle indépendant (traces du simulateur ou de l'automate réel).

---

## 6. Paramètres et seuils codés en dur (périmètre)

| Fichier:ligne | Valeur | Justification |
|---|---|---|
| `block_adapters.py:27` | 2³² ms, plage de TIME | **Aucune page citée** |
| `block_adapters.py:30` | 1 ms minimum | Résolution TIME, sans page citée |
| `block_adapters.py:32` | 1e-3 relatif, seuil d'avertissement d'arrondi | Choix interne (« noticeably »), non justifié |
| `block_adapters.py:63` | kd ≥ 1 | FAQ FA410692 **relayée, non lue** |
| `block_adapters.py:88` | 2³¹, borne de registre | Garde générique |
| `block_adapters.py:115` | KP ±10000 ; TI 1..20000 ; TS 1..32000 | Doc 33002520 (PARA[2], [3], [5]), page non précisée dans le code (p. 140 ou 141 selon les équations) |
| `block_adapters.py:119` | 1e-3 relatif (arrondi de registre) | Interne, non justifié |
| `block_adapters.py:139-150` | Table de compatibilité PID_Compact | A5E35300227-AJ p. 246-247 (11.1.2) |
| `block_adapters.py:183` | Versions V2.0 à V2.4 et V3.0 | Périmètre choisi ; V1.x exclue |
| `block_adapters.py:192` | Ti, Td ≤ 100000 s | Catalogue (A5E35300227-AJ) |
| `block_adapters.py:211` | PWeighting = 1, DWeighting = 0 | Choix de correspondance avec MKR (dérivée sur la mesure) ; les défauts constructeur sont 1/1 (`schneider.json:565`) |
| `block_adapters.py:75` | Valeurs neutres ovs_att, dband, ff, outbias, en_rcpy | p. 177-182 et FAQ relayée pour ovs_att |
| `pidff_reference.py:27` | Domaine ovs_att 0..1, kd ≥ 1 | FAQ relayée |
| `test_pidff_conversion.py:193-202` | Rapports 0,7 / 0,75 ; erreurs < 0,01 / 0,012 | Tolérances internes, non justifiées par une exigence |
| `test_pidint_conversion.py:63` | 0,06 × 10 (6 % de la consigne) | Large, non justifiée |
| `test_pidcompact_conversion.py:25` | `places=12` | Identité algébrique |
| `BlockConversion.cs:28, 111` | Format `G6` avec la culture courante | 6 chiffres significatifs : choix d'affichage, virgule décimale en français |
| `BlockConversion.cs:46` | Liste des neutres dupliquée en dur | Risque de dérive par rapport au moteur |
| `StudySheet.cs:12` | Culture `fr-FR` forcée, `G6` | PDF toujours en français |
| `StudySheet.cs:143-177` | Géométrie (44, 490, 250, 190, 148, 398, 273), police Arial | Mise en page, sans enjeu scientifique |
| `BlockTargetView.xaml.cs:11` | Liste des versions PID_Compact | Dupliquée par rapport au moteur |

---

## 7. Incohérences documentaires (synthèse)

- `schneider.json:277` (« Document non lu ») contredit `:121` et `:281-313`.
- 33002535 et 33002536 sont cités aux mêmes pages (`schneider.json:6-14, 34-40` contre `:116-122`).
- `blocs-observes.md:43` (« correspondance rev_dir ↔ signe … pas lue ») est périmé depuis la lecture de la p. 182.
- `blocs-observes.md:69, 75` sont périmés.
- `qualification-bloc-banc.md:5, 27` sont périmés.
- `qualification.md:3` parle de « trois candidats Schneider » alors qu'il y en a 4, et cite PID_3Step, PID_Temp, PID et PIDE, absents du catalogue.
- `audit-refonte.md:26` marque « Sélection du bloc constructeur ✔ ». C'est **surévalué** : aucune sélection par cible n'existe et le contrôle de compatibilité est inopérant (§1, point 3).
- `audit-refonte.md:56` dit « Simulation du bloc constructeur ◐ (PIDFF…) ». La simulation PIDFF n'existe qu'en oracle de test, ce qui est aussi surévalué.
- `audit-refonte.md:29` dit « manquent ISE » : l'ISE figure désormais dans le PDF (`StudySheet.cs:106`), le constat est partiellement périmé.
- `docs/exploitation.md` et `docs/feuille-de-route.md:17` sont cohérents avec le code sur l'absence d'écriture et le statut non qualifié.
- `docs/equipment-profiles-mvp.md` reconnaît qu'aucune fiche constructeur n'a été examinée (`:9`).

---

## 8. Liste priorisée

### P0 (bloquant pour un usage industriel ou une affirmation de compatibilité)
1. **Rendre le contrôle de cible effectif** :
   - `lookup`/`convert` ne doivent pas traiter `null` comme compatible pour une conversion ;
   - refuser un bloc d'un autre constructeur que la CPU déclarée ;
   - refuser une `libraryVersion` de cible différente de `parameters.version` ;
   - l'interface doit transmettre une cible réelle (constructeur, CPU, firmware, logiciel, bibliothèque). Aujourd'hui `target = {}` (`PythonDiagnostics.cs:421, 443`).
2. **Rendre la compatibilité non vérifiée visible** quand la CPU et le firmware ne sont pas fournis : ajouter un avertissement explicite, ou rendre le champ obligatoire. C'est le cas de PID_Compact (CPU facultative) et de PIDFF et PID_INT (aucun champ).
3. **Scénario M580** : aucune donnée de compatibilité M580 et aucune version de Control Expert ou de bibliothèque. Sans cela, aucune revendication « Schneider M580 » n'est défendable. À obtenir et à documenter avant tout essai E0-E7.
4. **PIDFF : imposer la cohérence d'unités OUT/MV** (demander l'unité et les plages de OUT) et demander la période d'exécution `dt`. Aujourd'hui, un kp faux d'un facteur d'échelle peut être affiché sans alerte.

### P1
5. Corriger les contradictions du catalogue (`schneider.json:277`, 33002535/33002536, résidus « 4805 » et « 12655 ») et les docs périmées (`blocs-observes.md:43, 69, 75`, `qualification-bloc-banc.md:5, 27`, `test_block_catalogue.py:99`).
6. Relire à la source les FAQ FA410692, FAQ000279181 et FAQ000256113 : les règles bloquantes kd ≥ 1 et kp ≥ 0 en dépendent.
7. Afficher les unités des registres PID_INT (`BlockConversion.cs:97` masque `units`) et ajouter une unité à `gain`.
8. Persister la fiche de paramètres affichée : valeurs, source et page, version du catalogue, avertissements. L'inscrire dans le PDF (section « Bloc et mise au point ») avec « Conversion non qualifiée » et la cible déclarée, et enregistrer les valeurs dans l'audit.
9. Créer une vraie fiche de préparation de mise au point : relevé de banc relié à l'étude, valeurs proposées, tolérances E7 fixées avant l'essai, essais E0-E7, export PDF.
10. Compléter le PDF : version MKR et version du catalogue, date et auteur de l'export, statuts par étape lisibles en français, avertissements traduits, ITAE, dépassement et établissement, mention « aucune écriture automate ». Étendre aux études intégrateur et SOPDT.
11. Construire des oracles indépendants : simulateur Control Expert ou PLCSIM, ou traces de banc. Pour PID_Compact, un oracle échantillonné. Pour PID_INT, comparer à la simulation de production de MKR et en arithmétique entière.
12. Distinguer les trois cartes PID_Compact (afficher la version) dans `BlockCatalogueWindow` et dans le relevé de banc.
13. Ajouter les avertissements spécifiques à chaque version de PID_Compact (IntegralResetMode V2.3 et suivantes, signe V2.4 avec OverwriteInitialOutputValue, limitation de l'intégrale en V3).
14. Corriger `audit-refonte.md:26, 56` (surévaluation).

### P2
15. Localiser `BlockConversion.Describe` et `RefusalText` ; traduire `no_matching_entry`.
16. Format numérique : proposer un point décimal ou l'afficher explicitement, pour éviter les erreurs de saisie des REAL.
17. Lever la contradiction entre le bouton « Copier » et « jamais par copie automatique », ou reformuler.
18. Lire les valeurs neutres depuis `Neutral` plutôt que de les coder en dur (`BlockConversion.cs:46`).
19. Arrondi des registres : documenter ou remplacer l'arrondi bancaire.
20. Ne pas livrer `pidff_reference.py` dans le paquet applicatif.
21. Catalogue : retirer PIFINT, qui n'a aucune preuve, ou le marquer « non étudié » ; ajouter les fiches PID_3Step, PID_Temp et Rockwell PID/PIDE au statut « non étudié » plutôt que de les laisser absentes.
22. Justifier ou nommer les tolérances de test (0,7, 0,75, 0,01, 0,06) et le seuil d'arrondi de 1e-3.
