# Import CSV — MVP 13

Origine : demande utilisateur « 13. Import CSV ». Le module autonome prend les octets originaux, l'encodage, le séparateur, le séparateur décimal, le nom de colonne temporelle, le format `strptime` et le dictionnaire colonne → unité explicitement fournis. Aucun format de date ou fuseau n'est deviné : une date sans décalage reste naïve. Les valeurs sont des `Decimal`, les horodatages des `datetime`, sans conversion d'unité.

Le résultat conserve les octets d'origine, leur SHA-256, les paramètres, les en-têtes et les cellules lues. Les erreurs structurées identifient code, message, numéro d'enregistrement CSV et colonne lorsque disponibles. Une cellule manquante ou invalide est représentée par `None` dans les enregistrements analysés tout en restant inchangée dans les cellules brutes ; la présence d'erreurs doit être vérifiée avant toute utilisation. Une erreur de syntaxe peut interrompre l'analyse : les octets originaux restent disponibles. Aucun remplissage, tri, suppression ni correction n'est effectué.

Limites : la syntaxe CSV retire les guillemets et l'échappement des cellules analysées ; seuls les octets originaux conservent exactement le fichier. Aucune détection de cadence, doublon, zone horaire, qualité physique ou conversion n'est effectuée. Les unités sont déclaratives et ne sont pas vérifiées contre un catalogue. L'appelant doit conserver le résultat brut et traiter explicitement les erreurs ; aucune intégration IPC ou interface n'est incluse. Pas de limite de taille : l'appelant doit en imposer avant de charger des fichiers non fiables.

Tests : `python -m unittest discover -s tests -p 'test_csv_import.py'`. Les fichiers synthétiques couvrent CSV français, dates et nombres invalides, valeurs manquantes, encodage, colonnes et préservation du brut. Aucun essai industriel ni validation métier n'est allégué.

Le parseur autonome est accessible via `mkr_data_import.io.csv_import`. Ce nom réserve `mkr_engine` au moteur IPC existant. Le parseur conserve ses propres tests et son raccordement au parcours WPF reste à valider.

Depuis la racine, exécuter `python -m unittest discover -s tests -p test_csv_import.py -v`.
