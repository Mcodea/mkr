# MKR-CAT — Qualification future

Périmètre d'étude : Control Expert PIDFF, PI_B, PID_INT sur Schneider (« PIFINT » était une erreur de nomenclature du client, corrigée le 2026-11-27), PID_Compact, PID_3Step, PID_Temp sur Siemens, PID et PIDE sur Rockwell. Le registre versionné engine/catalogue/schneider.json consigne les preuves disponibles pour les trois candidats Schneider. Les fiches restent au statut à vérifier. Chaque fiche doit référencer nom exact, environnement, CPU, firmware, version, source officielle, équation, unités, limites, modes, option de conversion et preuve du statut. Les statuts progressent de non étudié à documenté, vérifié en environnement puis qualifié sur matériel.

La commande IPC lookup_blocks accepte un filtre explicite. Un champ de cible inconnu reste visible pour revue avec selectable=false. convert_block_settings renvoie un refus structuré tant que la conversion exacte est indisponible. Chaque constat documentaire est limité au produit et à la section référencés.

## Préparer le relevé de banc

Depuis la fiche du bloc sélectionné, « Copier le relevé de banc » prépare un modèle texte à coller dans le dossier d’essai. Le relevé identifie l’édition du catalogue, liste les paramètres sourcés de ce bloc et laisse les valeurs terrain vides. Le technicien y consigne la configuration exacte, les unités, la tolérance prévue, la mesure et sa référence. La copie ne change aucun statut du catalogue. Le document complété est conservé par l’utilisateur dans son dossier d’essai.
