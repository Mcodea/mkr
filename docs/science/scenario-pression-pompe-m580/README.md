# Scénario de référence ORIAS — pression de réseau par pompe centrifuge à variateur, automate M580

> **DONNÉES SYNTHÉTIQUES.** Toutes les séries de ce scénario sont produites par simulation et ne sont **jamais** des mesures industrielles. Aucune installation réelle n'a été consultée. Aucun automate, aucun variateur et aucun banc n'a été utilisé.

Statut : **PROPOSÉ**, scénario de référence. Il n'a été ni validé fonctionnellement ni qualifié.

## Contenu

| Fichier | Rôle |
|---|---|
| [parametres-theoriques.md](parametres-theoriques.md) | Modèle, unités, linéarisation, K, τ et θ, domaine de validité, vérifications |
| [schneider-m580.md](schneider-m580.md) | PIDFF, PID_INT et « PIFINT » : sources, conventions, contradictions, niveaux de preuve |
| `tests/reference/scenario_pression_pompe/generate.py` | Générateur autonome (numpy et scipy ; aucun import de `engine/`) |
| `tests/reference/scenario_pression_pompe/data/` | 9 CSV, 9 manifestes JSON, `parametres_theoriques.json`, `SHA256SUMS` |

## Scénario

Une pompe centrifuge entraînée par un variateur maintient la pression d'un collecteur muni d'un réservoir hydropneumatique. Un régulateur PI agit sur la consigne de vitesse (MV, en %) pour suivre la consigne de pression (SP, en bar relatif) à partir de la mesure du transmetteur (PV, en bar relatif).

Point nominal : 85 %, 4,0 bar et 13 m³/h.

Paramètres de référence :
- **K = 0,0941 bar/%** en local et 0,0969 bar/% en sécant pour +5 % ;
- **FOPDT équivalent : K = 0,0971 bar/%, τ = 4,67 s, θ = 1,26 s** ;
- **SOPDT équivalent : τ1 = 4,36 s, τ2 = 1,03 s, θ = 0,44 s**.

## Jeux synthétiques

Les jeux sont générés à Ts = 0,1 s et ont pour en-tête `timestamp,SP,PV,MV,MANUAL`. L'horodatage est ISO 8601 UTC au milliseconde (strptime `%Y-%m-%dT%H:%M:%S.%f%z`). Le séparateur est la virgule et le séparateur décimal le point. Chaque fichier fait au plus 307 ko et 6001 lignes, sous les limites du moteur (1 Mio et 10 000 lignes).

| Jeu | Graine | Essai | Intention |
|---|---|---|---|
| `ol_nominal` | 20261101 | Échelon 85 → 90 %, σ = 0,003 bar | accepté (estimation) |
| `ol_validation` | 20261102 | Échelon 85 → 89 %, autre date | accepté (validation **indépendante**, jamais pour ajuster) |
| `ol_validation_reverse` | 20261109 | Échelon 85 → 80 % | accepté sous conditions (gain sécant inférieur de 6 % : non-linéarité) |
| `ol_noisy` | 20261103 | Échelon 85 → 90 %, σ = 0,03 bar | accepté sous conditions |
| `ol_disturbance` | 20261104 | Vanne aval de 0,50 à 0,60 à t = 35 s, non enregistrée | refusé ou signalé |
| `ol_mv_saturation` | 20261105 | Commande à 105 % enregistrée, vitesse réelle limitée à 100 % | refusé (si les limites MV sont déclarées) |
| `ol_insufficient_excitation` | 20261106 | Échelon de 0,3 %, σ = 0,01 bar | refusé |
| `ol_out_of_domain` | 20261107 | Échelon 85 → 55 % : K varie de 0,094 à 0,061 et τ de 4,6 à 10,5 s | refusé comme modèle du point nominal |
| `cl_pi` | 20261108 | Boucle fermée sous PI SIMC ; manuel puis automatique ; échelons de SP ; vanne ; saturation à SP = 6 bar | pour les lots ultérieurs, pas en boucle ouverte |

Chaque manifeste contient :
- l'avertissement `_DONNEES_SYNTHETIQUES` ;
- la graine (`numpy.random.default_rng`, PCG64) et les versions de numpy et scipy ;
- les unités, le format CSV et le bruit ;
- les paramètres vrais, l'intention et sa justification.

**Reproduction :** `python tests/reference/scenario_pression_pompe/generate.py` régénère les fichiers ; l'option `--check` génère deux fois et compare. Les deux exécutions sont identiques à l'octet près (`SHA256SUMS`). L'identité est démontrée avec numpy 2.3.5 et scipy 1.17.0, sur la même plateforme. D'autres versions pourraient changer le flux aléatoire ou les derniers bits des calculs.

## Hypothèses de scénario et faits issus d'une source

**Issus d'une source** (ordres de grandeur ou forme du modèle) :
- lois de similitude, dont les limites sont étudiées dans [SM] ;
- pompe de l'ordre de 16 m³/h, 58 m et 4 kW au point de meilleur rendement [KG] ;
- usage d'un réservoir hydropneumatique dans un système à pression constante [AC], [DT] ;
- rampes ACC/dEC et seconde rampe AC2 de 0 à 6000 s sur Altivar 320 [FA335688], [FA371976] ;
- règle de la moitié et SIMC [S03] ;
- anti-saturation et transfert sans à-coup [AR], [PVH].

**Hypothèses** (non sourcées, choisies pour le scénario) :
- H0 = 72 m, conduite de 30 m en DN65 et pertes de 3 m ;
- gaz de 50 L à 4 bar, κ = 1 et prégonflage à 1 bar ;
- LSP = 30 % et HSP = 100 % ;
- rampe de 10 %/s, τ_m = 0,5 s et retard de transmission de 0,1 s ;
- capteur de 0 à 10 bar avec τ_s = 0,2 s, résolution de 0,001 bar et bruit σ de 0,003 à 0,03 bar ;
- Ts = 0,1 s ;
- eau à 998,2 kg/m³, module de compressibilité de 2,2 GPa et acier à 200 GPa ;
- loi de vanne Q = Cq·u·√h.

**Aucune valeur ne provient d'une fiche technique de transmetteur, de variateur ou de module d'entrée M580.** Ces documents n'ont pas été lus.

## Références vérifiées

La vérification porte sur l'existence et les métadonnées, par Crossref ou OpenAlex, le 26 et le 27/11/2026. **La lecture n'a porté que sur ce qui est précisé.**

- [S03] Skogestad, S. « Simple analytic rules for model reduction and PID controller tuning ». *J. Process Control* 13 (2003) 291–309, doi:10.1016/S0959-1524(02)00062-8. Le **PDF a été lu** sur la page de l'auteur (skoge.folk.ntnu.no) : règle de la moitié au §2.1, p. 293, éq. (10) et (11) ; SIMC aux éq. (23) à (25), p. 296.
- [G] Gülich, J. F. *Centrifugal Pumps*, Springer, 2014, doi:10.1007/978-3-642-40114-5. Métadonnées seulement : les lois de similitude y sont présentées, mais **le chapitre et la page ne sont pas relus**.
- [SM] Simpson, A. R. et Marchi, A. « Evaluating the approximation of the affinity laws… ». *J. Hydraul. Eng.* 139(12) (2013) 1314–1317, doi:10.1061/(ASCE)HY.1943-7900.0000776. Résumé lu : approximations des lois de similitude, surtout pour le rendement des petites pompes.
- [KG] Khayatzadeh, F. et Ghafouri, J. « Dynamical modeling of frequency controlled variable speed parallel multistage centrifugal pumps ». *Arch. Mech. Eng.* 62(3) (2015) 347–362, doi:10.1515/meceng-2015-0020. Résumé lu : 16 m³/h, 58 m et 4 kW au point de meilleur rendement, régulation PID par variateur.
- [AC] Andrade-Cedeño, R. J. et al. « Numerical study of constant pressure systems with variable speed electric pumps ». *Energies* 15(5) (2022) 1918, doi:10.3390/en15051918. Résumé lu : réservoir hydropneumatique, variateurs, régulateur de pression.
- [DT] Dindorf, R., Takosoglu, J. et Wos, P. « Review of hydro-pneumatic accumulator models… ». *Energies* 16(18) (2023) 6472, doi:10.3390/en16186472. Résumé lu.
- [EH] Europump et Hydraulic Institute. *Variable Speed Pumping*, Elsevier, 2005. Chapitres « Pumping system hydraulic characteristics » (pp. 5–10, doi:10.1016/B978-185617449-7/50002-1), « Variable speed drives » (pp. 59–66) et « Control principles » (pp. 67–77). Métadonnées seulement.
- [AR] Åström, K. J. et Rundqwist, L. « Integrator windup and how to avoid it ». ACC 1989, pp. 1693–1698, doi:10.23919/ACC.1989.4790464. Métadonnées seulement.
- [PVH] « Anti-windup, bumpless, and conditioned transfer techniques for PID controllers ». *IEEE Control Systems* 16(4) (1996) 48–57, doi:10.1109/37.526915. Métadonnées seulement ; Crossref ne renvoie pas d'auteurs.
- FAQ Schneider lues sur se.com : FA410692, FAQ000279181, FAQ000279186, FAQ000256113, FA343611, FA335688, FA371146 et FA371976.

La formule de Korteweg et la capacité d'un gaz polytropique relèvent de la physique classique. Elles sont utilisées sans référence de page, et aucune source n'est inventée pour elles.

## Limites

- Les PDF Schneider n'ont pas pu être téléchargés : `download.se.com` et `download.schneider-electric.com` sont refusés par le proxy, et mdpi.com l'est aussi. Ces refus n'ont pas été contournés. Voir [schneider-m580.md](schneider-m580.md).
- Le modèle est à paramètres localisés et monopompe, sans hauteur statique, sans cavitation ni rendement. Le gain p ∝ n² est donc un cas particulier : avec une hauteur statique, K et sa non-linéarité changent.
- Le PI de `cl_pi` est générique et n'est **pas** un modèle du PIDFF ni du PID_INT.
- Les intentions « accepté » et « refusé » sont des **attentes de scénario**. Le contrôle externe du moteur ORIAS montre deux divergences à arbitrer (voir parametres-theoriques.md, §7) :
  - `ol_disturbance` est accepté avec un simple avertissement ;
  - la saturation et la non-linéarité ne sont pas détectées en tant que telles.
- Aucun essai n'a été conduit sur un automate réel.
