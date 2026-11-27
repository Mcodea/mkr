# Conformité des licences d'ORIAS : synthèse

- **Relevés** : 26 et 27/11/2026, branche `claude/science-engine-dev`, commit `cdc583a`.
- **Nature** : constat factuel. **Ce n'est pas un avis juridique.** Les points marqués **[JURISTE]** demandent une qualification par un conseil.

## Documents

| Fichier | Contenu |
|---|---|
| [inventaire-dependances.md](inventaire-dependances.md) | Contenu réel du paquet Windows. Dépendances NuGet et Python (directes et transitives), test et CI : version, licence lue à la source, obligations, usage réel. §7 : bibliothèques scientifiques candidates |
| [jeux-de-donnees.md](jeux-de-donnees.md) | Jeux de données d'identification : URL, licence citée, droits d'utilisation, de modification et de redistribution, recommandation par jeu, alternatives sous licence claire |

## Résumé

1. **Pile livrée aujourd'hui** : toutes les licences sont **permissives** ou dans le domaine public.
   - MIT : PDFsharp/MigraDoc 6.2.4, Microsoft.Data.Sqlite, Pkcs, runtime .NET et WPF, openpyxl, et-xmlfile.
   - Apache-2.0 : SQLitePCLRaw, tzdata.
   - BSD-3 : numpy, scipy.
   - Domaine public : SQLite natif.
   - PSF : CPython.

   Aucune dépendance copyleft n'est livrée, à une exception près : le runtime GCC lié dans OpenBLAS (roues numpy et scipy), sous GPL-3.0+ avec l'exception GCC Runtime Library 3.1.
2. **Risque principal : changement de licence de PDFsharp.** Les versions **6.5.0 et 7.0.0-preview sont sous `AGPL-3.0-only`** (nuspec sur api.nuget.org). La 6.2.4, utilisée aujourd'hui, est sous MIT. Or la référence actuelle `Version="6.2.4"` autorise toute version ≥ 6.2.4.
3. **Polices Microsoft « Segoe WP »** embarquées dans `PdfSharp.WPFonts.dll` : leur licence propre est restrictive. ORIAS ne les utilise pas, mais les livre.
4. **Aucune notice tierce n'est livrée pour la partie .NET.** Le texte « À propos » affirme pourtant que les licences sont fournies.
5. **openpyxl et et-xmlfile sont livrés sans usage** : seul `mkr_data_import/`, qui n'est pas livré, les importe. En revanche, **System.Security.Cryptography.Pkcs n'est pas orphelin** : PDFsharp en dépend, et la référence directe force une version corrigée. L'audit LOT 0 (C-donnees-architecture §16) est à rectifier sur ce point.
6. **Jeux de données** : DaISy, les données TCLab d'apmonitor.com et `tclab-data.csv` (Notre Dame) n'ont **aucune licence**. Ils restent en test interne seulement.
   - **Un extrait APMonitor est versionné dans le dépôt** : `tests/reference/tclab_dyn_data2_extract.csv`.
   - Des alternatives sous **CC BY 4.0 lue à la source** existent :
     - TCLab Take-Home Labs (Hedengren co-auteur, Zenodo 24286966) ;
     - InTaSet (quatre cuves, boucle ouverte et fermée, Zenodo 17652851) ;
     - PRONTO (écoulement polyphasique, Zenodo 1341583).
7. **Bibliothèques candidates** : python-control, statsmodels, nfoursid et SysIdentPy sont permissives (BSD/MIT). SIPPY et CasADi sont sous **LGPL-3.0** et restent exclues provisoirement. GEKKO : code MIT, mais à examiner pour ses solveurs et son mode serveur distant.

## Décisions à prendre (client)

| # | Décision | Options relevées |
|---|---|---|
| D1 | Stratégie PDF | (a) figer `[6.2.4]` et ajouter une garde de licence en CI, sans correctifs futurs sous MIT ; (b) acheter la licence commerciale d'empira ; (c) changer de bibliothèque |
| D2 | `PdfSharp.WPFonts.dll` (+ Snippets, Quality) | Exclure de la publication après un test de non-régression des PDF, ou faire valider la redistribution |
| D3 | Notice tierce | Générer `THIRD-PARTY-NOTICES` dans le paquet (liste au §6 de l'inventaire) et corriger le libellé « À propos » |
| D4 | openpyxl / et-xmlfile | Les sortir de `engine/requirements.txt` (et du contrôle d'import de `package-windows.ps1`), ou livrer réellement l'import Excel |
| D5 | Extrait TCLab APMonitor versionné | Le retirer et le télécharger en CI, demander une autorisation écrite, ou le remplacer par le jeu Zenodo 24286966 (CC BY 4.0) |
| D6 | Jeux CC BY à intégrer | Choisir parmi TCLab Take-Home (essais seulement, jamais les enquêtes ni les notes), InTaSet, PRONTO, CONSENS. Imposer un fichier `SOURCE` (DOI, version, licence, modifications, SHA-256) |
| D7 | Confirmations à la source | NAWI (OpenEI) et le moteur Mendeley : licence vue seulement en source secondaire ; hôtes refusés dans cette session |
| D8 | Reproductibilité | Figer CPython (version et notice), activer les fichiers de verrouillage NuGet et les hachages pip |

## Points exigeant un juriste

1. Portée de l'**AGPL-3.0** de PDFsharp ≥ 6.5 et conditions de la licence commerciale d'empira.
2. Redistribution des **polices Segoe WP** dans un produit commercial.
3. **Redistribuables Microsoft Visual C++** (`vcruntime140*.dll`, `msvcp140-*.dll`) et composants natifs du runtime WPF autonome.
4. Confirmation que l'**exception GCC Runtime Library 3.1** couvre bien la livraison des roues numpy et scipy.
5. **CC BY 4.0** dans un produit fermé : attribution et interdiction des mesures techniques restrictives. **CC BY-SA 4.0** (Cascaded Tanks) : portée du partage à l'identique.
6. Jeux **sans licence** (DaISy, apmonitor.com, CBE30338) :
   - droit d'auteur et droit *sui generis* des bases de données ;
   - **statut de l'extrait déjà versionné**, selon la visibilité du dépôt `Mcodea/mkr`.
7. **LGPL-3.0** (SIPPY, CasADi, et Snap7 en test) : conditions de redistribution, si l'une d'elles devait un jour être livrée.

## Limites de ce relevé

**Hôtes refusés par le proxy, sans contournement** :
- `ftp.esat.kuleuven.be` (fiches DaISy) ;
- `data.4tu.nl`, `data.openei.org`, `osti.gov` ;
- `data.mendeley.com`, `sites.ualberta.ca`, `sqlite.org`, `docs.pdfsharp.net` ;
- `api.datacite.org`, l'API GitHub.

Leurs points restent marqués « V-sec », « NT » ou « non relu ».

**Non audité** : les avis de sécurité des versions figées.
