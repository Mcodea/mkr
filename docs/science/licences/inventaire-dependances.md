# Inventaire des dépendances et de leurs licences

- **Date de relevé** : 26/11/2026.
- **Branche** : `claude/science-engine-dev`, commit `cdc583a`.
- **Nature du document** : relevé factuel, **pas un avis juridique**. Une qualification « permissive / copyleft » est la catégorie usuelle de la licence citée. Elle ne vaut pas validation pour ORIAS. Les points qui exigent un juriste sont marqués **[JURISTE]**.

## Sources lues

- **Manifestes du projet** : `engine/requirements.txt`, `tests/interop/requirements.txt`, `Directory.Build.props`, `src/**/*.csproj`, `tests/**/*.csproj`, `.github/workflows/{verify,nawi,openmct}.yml` et `scripts/package-windows.ps1`.
- **Graphe NuGet résolu** : `src/Mkr.Infrastructure/obj/project.assets.json` et `tests/Mkr.Communication.Tests/bin/Debug/net10.0/*.deps.json`.
- **Métadonnées NuGet** : fichiers `.nuspec` du cache local `~/.nuget/packages`, recoupés pour PDFsharp avec `https://api.nuget.org/v3-flatcontainer/…` et `https://api.nuget.org/v3/registration5-gz-semver2/pdfsharp/index.json`.
- **Métadonnées Python** : `https://pypi.org/pypi/<nom>/<version>/json`, plus les fichiers `LICENSE*` / `LICENCE*` des roues **win_amd64 / cp312** réellement installées par le paquet Windows, téléchargées avec `pip download --platform win_amd64 --python-version 3.12`.

Il n'y a **aucun fichier de verrouillage** : ni `packages.lock.json`, ni `Directory.Packages.props`, ni lock pip avec hachages. Les versions Python sont figées par `==`, mais **sans hachage**. Les versions NuGet sont écrites `Version="x"`, ce qui veut dire **« x ou plus »** (voir le risque R1).

## 1. Ce qui est réellement livré

Le paquet Windows est produit par `scripts/package-windows.ps1`. Il contient :

1. **La publication .NET autonome** (`dotnet publish … -r win-x64 --self-contained true`). Elle embarque :
   - le runtime .NET 10 et WPF (Microsoft.NETCore.App, Microsoft.WindowsDesktop.App) ;
   - les assemblages des paquets NuGet du §2.
2. **Un CPython 3.12 Windows** copié depuis l'installation du poste de construction. Il comprend :
   - `python.exe`, `python3.dll`, `python312.dll` et `vcruntime*.dll` ;
   - les fichiers `LICENSE*` ;
   - les dossiers `DLLs/` et `Lib/`, sauf `site-packages`, `test`, `idlelib` et `tkinter`.
3. **Les paquets de `engine/requirements.txt`**, installés dans `python/Lib/site-packages` avec `pip --no-deps`. Leurs dossiers `*.dist-info`, qui contiennent les licences, sont donc présents dans le paquet.
4. **Le moteur** : `engine/*.py` (sans `test_*.py`) et `engine/catalogue/*.json`.

Ne sont **pas** livrés : `mkr_data_import/`, `tests/`, `samples/`, `scripts/` et les données de référence de `tests/reference/`.

## 2. Dépendances .NET (NuGet)

Le graphe ci-dessous est celui résolu pour `net10.0`, dans `project.assets.json`.

| Paquet | Version résolue | Direct / transitif | Licence (source : nuspec) | Copyright déclaré | Catégorie | Usage réel dans le produit | Livré |
|---|---|---|---|---|---|---|---|
| Microsoft.Data.Sqlite.Core | 10.0.8 | Direct (Infrastructure) | MIT (expression) | © Microsoft Corporation | Permissive | `SqliteProjectStore.cs` (stockage local) | Oui |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | Direct | Apache-2.0 | © 2014-2026 SourceGear, LLC | Permissive (notice Apache) | Liaison native SQLite | Oui |
| SQLitePCLRaw.config.e_sqlite3 | 3.0.5 | Transitif | Apache-2.0 | © SourceGear | Permissive | idem | Oui |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | Transitif | Apache-2.0 | © 2014-2025 SourceGear | Permissive | idem | Oui |
| SQLitePCLRaw.core | 3.0.5 | Transitif (Microsoft.Data.Sqlite.Core demande ≥ 2.1.11 ; 3.0.5 retenu) | Apache-2.0 | © 2014-2025 SourceGear | Permissive | idem | Oui |
| SQLite (bibliothèque native e_sqlite3) | 3.53.4 | Transitif | Fichier `LICENSE.txt` : « SQLite is Public Domain — https://sqlite.org/copyright.html » | © 2014-2026 SourceGear (empaquetage) | Domaine public (code SQLite) | Moteur SQLite natif | Oui |
| PDFsharp-MigraDoc | **6.2.4** | Direct | **MIT** pour 6.2.4 | © 2026 empira | Permissive | `StudySheet.cs` (fiches PDF) | Oui |
| PDFsharp | **6.2.4** | Transitif | **MIT** pour 6.2.4 | © 2026 empira | Permissive | idem | Oui (9 assemblages, dont `PdfSharp.WPFonts.dll`, voir R2) |
| System.Security.Cryptography.Pkcs | 10.0.13 | **Direct, mais forcé** : PDFsharp 6.2.4 demande ≥ 8.0.1 | MIT | © Microsoft Corporation | Permissive | Pas d'appel direct dans `src/`. Requis par PDFsharp (`PdfSharp.Cryptography`). La référence directe remonte la version, commit `1f51340` « Use the patched cryptography dependency for PDF rendering » | Oui |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Transitif (PDFsharp) | MIT | © Microsoft | Permissive | Interne à PDFsharp | Oui |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | Transitif | MIT | © Microsoft | Permissive | Interne | Oui |
| Runtime .NET 10 + WindowsDesktop (WPF) | 10.0.x (SDK de CI `10.0.x`) | Plateforme, publication autonome | MIT. Le nuspec de `Microsoft.WindowsDesktop.App.Ref` 10.0.15 déclare MIT et contient `LICENSE` et `THIRD-PARTY-NOTICES` | © Microsoft | Permissive, avec notices tierces | Exécution de l'application | Oui |

### Paquet signalé « orphelin » par l'audit LOT 0 : à corriger

`docs/science/lot0/annexes/C-donnees-architecture.md` (§16) range `System.Security.Cryptography.Pkcs` parmi les « dépendances livrées sans usage ». **C'est inexact dans les faits.**

- Le paquet est une dépendance de PDFsharp et PDFsharp-MigraDoc 6.2.4 (`System.Security.Cryptography.Pkcs ≥ 8.0.1`).
- La référence directe en 10.0.13 sert à **remonter** cette version transitive (commit `1f51340`).
- Supprimer la ligne ne retirerait pas la DLL : la version 8.0.1 reviendrait. Elle a été remplacée pour un motif de correctif. L'avis de sécurité visé n'a pas été relu ici.

## 3. Dépendances Python livrées (`engine/requirements.txt`)

Les fichiers de licence ont été lus dans les roues Windows cp312.

| Paquet | Version | Licence (source) | Composants embarqués et licences (fichier `LICENSE.txt` de la roue) | Catégorie | Usage réel | Livré |
|---|---|---|---|---|---|---|
| numpy | 2.3.5 | BSD-3-Clause (« Copyright (c) 2005-2025, NumPy Developers », classificateur BSD) | **OpenBLAS** (BSD-3-Clause), **LAPACK** (BSD-3-Clause-Open-MPI), **runtime GCC** (« GPL-3.0-or-later WITH GCC-exception-3.1 », lié statiquement dans `libscipy_openblas*.dll`), lapack-lite (BSD-3), dragon4 (MIT), libdivide (Zlib), etc. La roue embarque aussi `numpy.libs/msvcp140-*.dll` (runtime Microsoft Visual C++) | Permissive, plus un composant GPL couvert par l'exception GCC | 42 imports dans `engine/` | Oui |
| scipy | 1.17.0 | BSD-3-Clause (« Copyright (c) 2001-2002 Enthought, Inc. 2003, SciPy Developers ») | OpenBLAS (BSD-3), LAPACK (BSD-3-Open-MPI), runtime GCC (GPL-3.0+ WITH GCC-exception-3.1), pocketfft, DOP, uarray (fichiers LICENSE propres). Le `LICENSE.txt` fait 912 lignes | Permissive, plus exception GCC | `scipy.optimize`, `scipy.signal`, etc. (15 imports) | Oui |
| openpyxl | 3.1.5 | MIT (« Copyright (c) 2010 openpyxl ») | — | Permissive | **Aucun dans le produit livré.** Importé seulement par `mkr_data_import/io/excel_import.py`, qui n'est pas livré, et par `tests/test_excel_import.py`. `package-windows.ps1` importe `openpyxl` uniquement pour vérifier le paquet | **Oui, inutilement** |
| et-xmlfile | 2.0.0 | MIT (`LICENCE.rst`) + `LICENCE.python` (code repris de la bibliothèque standard Python, licence PSF, texte inclus) | — | Permissive | Dépendance d'openpyxl seulement | **Oui, inutilement** |
| tzdata | 2025.2 | Apache-2.0 (« Copyright (c) 2020, Paul Ganssle (Google) ») | Base IANA des fuseaux horaires (non relu ici) | Permissive (notice Apache) | `zoneinfo` dans `engine/csv_format.py` (ADR 0005 : Windows n'a pas de base de fuseaux) | Oui |
| CPython 3.12 (runtime) | 3.12.x du poste de construction (non figé) | PSF License 2.0. Le `LICENSE.txt` Windows regroupe les licences tierces : OpenSSL, libffi, zlib, expat, bzip2, xz, SQLite, mpdecimal, Tcl/Tk… (texte non relu ici) | `vcruntime140*.dll` copiés depuis l'installation | Permissive, plus redistribuables Microsoft | Interpréteur du moteur | Oui (`LICENSE*` copiés) |

## 4. Outillage de test et de CI uniquement (non livré)

| Composant | Version | Licence (source) | Remarque | Où |
|---|---|---|---|---|
| pymodbus | 3.8.6 | BSD-3-Clause (PyPI et `LICENSE` de la roue : « Copyright 2008-2023 Pymodbus ») | Aucune dépendance obligatoire (seulement des extras) | `tests/interop/requirements.txt`, étape CI « serveurs de référence Modbus et S7 » |
| python-snap7 | 2.0.2 | Enveloppe Python sous **MIT** (« Copyright (c) 2013 Gijs Molenaar, Stephan Preeker ») | La roue win_amd64 **embarque `snap7/lib/snap7.dll`**, la bibliothèque native Snap7 de Davide Nardella. Cette bibliothèque est diffusée par son auteur sous **LGPL-3.0**, mention non relue ici : le site sourceforge n'a pas été consulté. La roue ne contient pas de texte LGPL | `tests/interop/requirements.txt` |
| pypdf | 5.9.0 | BSD-3-Clause (PyPI, `license_expression`) | — | Étape CI « lecteur PDF de vérification » |
| GitHub Actions (`actions/checkout@v5`, `setup-python@v6`, `setup-dotnet@v5`, `upload-artifact@v4`) | étiquettes majeures | MIT (licence usuelle de ces actions ; non relue ici) | Exécutées sur le runner, rien n'est livré | `.github/workflows/*.yml` |
| Projets `tests/Mkr.*` | — | Code du projet | Aucune `PackageReference` propre | `tests/` |
| Données de référence téléchargées en CI | — | Voir `jeux-de-donnees.md` | `jckantor/CBE30338-book`, OpenEI NAWI, Mendeley | `verify.yml`, `nawi.yml`, `openmct.yml` |

**Python sur le poste de développement** : `pypdfium2` 5.14.0 et `pypdf` 6.17.0 sont présents dans l'environnement local, mais aucun manifeste du dépôt ne les référence. Ils ne sont pas livrés.

## 5. Risques et constats

| # | Constat | Gravité pour une distribution fermée | Action proposée (décision du client) |
|---|---|---|---|
| **R1** | **PDFsharp et PDFsharp-MigraDoc ont changé de licence.** 6.2.4 (publiée le 06/01/2026) : MIT. **6.5.0 (14/10/2026) et 7.0.0-preview-1/2 : `AGPL-3.0-only`**, d'après les nuspec sur api.nuget.org. `Version="6.2.4"` signifie « ≥ 6.2.4 ». NuGet retient aujourd'hui la plus basse version admissible, mais une mise à jour assistée (correctif NuGetAudit, Dependabot, montée manuelle) basculerait vers une version AGPL. | **Élevée** : l'AGPL est un copyleft fort, incompatible en pratique avec un produit fermé sans licence commerciale de l'éditeur **[JURISTE]** | Figer en version exacte `[6.2.4]`, ajouter une vérification CI de la licence, et décider : rester sur la dernière version MIT (sans correctifs), acheter la licence commerciale d'empira, ou changer de bibliothèque |
| **R2** | `PdfSharp.WPFonts.dll`, livré avec PDFsharp 6.2.4, **embarque les polices Microsoft « Segoe WP »**. Texte relevé dans l'assemblage : « 2010 Microsoft Corporation. All Rights Reserved. … You may use this font as permitted by the EULA for the product in which this font is included to display and print content. You may only (i) embed this font in content as permitted by the embedding restrictions… ». ORIAS n'utilise pas ces polices : `StudySheet.cs` emploie « Arial » avec `UseWindowsFontsUnderWindows`. Seuls `PdfSharp.Snippets.dll` et `PdfSharp.Quality.dll` y font référence, pas `PdfSharp.dll`. | Moyenne **[JURISTE]** : la licence MIT de PDFsharp ne couvre pas, à elle seule, ces polices | Faire qualifier la redistribution. Techniquement, on peut exclure `PdfSharp.WPFonts.dll`, `PdfSharp.Snippets.dll` et `PdfSharp.Quality.dll` de la publication, après un test de non-régression des PDF |
| R3 | **Aucune notice tierce n'est livrée pour la partie .NET.** `dotnet publish` ne copie ni les licences des paquets NuGet ni `THIRD-PARTY-NOTICES` du runtime. Le texte « À propos » (`Localization.cs`, clé `about.thirdparty.text`) affirme que « les licences sont celles de chaque composant, fournies avec sa distribution ». C'est vrai pour Python (`*.dist-info`, `LICENSE*`), pas pour .NET. MIT et Apache-2.0 exigent de joindre le texte de licence et la mention de copyright (et, pour Apache, le NOTICE s'il existe). | Moyenne (obligation de notice) | Produire un fichier `THIRD-PARTY-NOTICES` dans le paquet, avec tous les textes. Corriger le libellé « À propos » |
| R4 | **openpyxl et et-xmlfile sont livrés sans usage** dans le produit : seul du code non livré (`mkr_data_import/`) les importe. | Faible (licences MIT). Coût : taille du paquet et surface de maintenance | Les sortir de `engine/requirements.txt` vers un fichier de test, et retirer `openpyxl` du contrôle d'import de `package-windows.ps1`. Ou bien livrer réellement l'import Excel. Décision produit |
| R5 | Les roues numpy et scipy contiennent un **runtime GCC** sous GPL-3.0+ avec l'exception GCC Runtime Library 3.1, lié statiquement dans OpenBLAS. | Faible en pratique (l'exception vise ce cas) **[JURISTE]** pour confirmation | Conserver les `LICENSE.txt` des roues (c'est déjà le cas) et les reprendre dans la notice globale |
| R6 | **Redistribuables Microsoft Visual C++** : `vcruntime140*.dll` copiés depuis l'installation Python, et `msvcp140-*.dll` dans `numpy.libs`. Leur redistribution relève des conditions Microsoft (« Distributable Code »), pas de la licence PSF ni BSD. | Faible à moyenne **[JURISTE]** | Faire confirmer le cadre de redistribution par un tiers (ORIAS) |
| R7 | Le CPython embarqué n'est **pas figé** : c'est la version 3.12.x du poste de construction. Son `LICENSE.txt` (et donc la liste des composants tiers, dont OpenSSL) dépend de cette version. | Faible (traçabilité) | Figer la version de CPython et archiver la notice avec chaque livraison |
| R8 | **Absence de verrouillage** : pas de hachages pip, pas de `packages.lock.json`. | Faible (licence), moyenne (chaîne d'approvisionnement) | `pip --require-hashes` et `RestorePackagesWithLockFile` |
| R9 | Le contrôle `python -m pip check` passe en CI, mais SysIdentPy 0.9.0, candidat (voir le §7), borne `numpy<=2.3.5`. | Information | — |
| R10 | Le moteur natif SQLite (3.53.4) est dans le domaine public. Le site sqlite.org/copyright.html (refusé par le proxy, non relu) traite du cas des organisations qui exigent une licence écrite : à lire si un client final l'exige. | Information **[JURISTE]** si un client final l'exige | — |

## 6. Obligations de notice à livrer (relevé)

Le paquet doit joindre un recueil de notices tierces. Ce recueil n'existe pas aujourd'hui pour la partie .NET. Il doit contenir :

- **MIT** : PDFsharp, PDFsharp-MigraDoc, Microsoft.Data.Sqlite.Core, System.Security.Cryptography.Pkcs, Microsoft.Extensions.* et le runtime .NET / WPF (avec son `THIRD-PARTY-NOTICES.TXT`). Si elles restent dans le paquet : openpyxl et et-xmlfile (avec la partie PSF).
- **Apache-2.0** : SQLitePCLRaw.* (texte de licence ; vérifier l'existence d'un `NOTICE`, aucun n'est présent dans les paquets du cache) et tzdata.
- **BSD-3-Clause** : numpy, scipy et leurs composants embarqués (OpenBLAS, LAPACK…, avec les `LICENSE.txt` des roues).
- **GCC Runtime Library Exception 3.1 et GPL-3.0** : le texte joint aux roues numpy et scipy.
- **PSF** : le `LICENSE.txt` de CPython (déjà copié).
- **Domaine public** : SQLite (mention).
- **Polices Segoe WP** : selon la décision R2.

## 7. Bibliothèques scientifiques candidates (non utilisées aujourd'hui)

Sources :
- `https://pypi.org/pypi/<nom>/json`, consulté le 26/11/2026 (version courante, date de la dernière publication, licence déclarée, dépendances obligatoires) ;
- pour les derniers commits, le relevé de `docs/science/lot0/annexes/E-referentiel.md` §2, fait le même jour sur des clones git. Il n'a pas été refait ici, car l'API GitHub n'est pas ouverte pour ces dépôts dans cette session.

| Bibliothèque (nom PyPI) | Licence déclarée | Version courante (date) | Maintenance | Dépendances obligatoires et leurs licences usuelles | Compatibilité avec une distribution fermée | Risque / remarque |
|---|---|---|---|---|---|---|
| NumPy (`numpy`) | `BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0` (2.5.5) | 2.5.5 (15/11/2026) ; ORIAS : 2.3.5 | Très active | — (roues : OpenBLAS, runtime GCC, voir §3) | Permissive | Retard d'un an de la version figée. L'expression de licence de 2.5.x est plus détaillée : à reprendre dans la notice en cas de montée |
| SciPy (`scipy`) | BSD-3-Clause (texte) | 1.18.1 (21/08/2026) ; ORIAS : 1.17.0 | Très active | numpy | Permissive | idem |
| python-control (`control`) | BSD-3-Clause | 0.10.2 (05/07/2025), « 4 - Beta » | Commit du 27/10/2026 selon E-referentiel | numpy, scipy, **matplotlib** (3.11.2 : classificateur PyPI « Python Software Foundation License », accord de licence propre à matplotlib ; ses dépendances, dont pillow et fonttools, et les polices embarquées n'ont pas été relues ici) | Permissive | Pas une boîte d'identification. Utile comme calcul croisé (marges, Ms). matplotlib alourdirait le paquet |
| SIPPY (`sippy-unipi`) | **LGPL-3.0-or-later** | 1.0.1 (02/09/2025), « 4 - Beta » | Commit du 05/06/2026 selon E-referentiel | casadi (**LGPL-3.0+**), control (BSD-3), sympy 1.14.0 (BSD), tf2ss 2.1.0 (MIT) | **Copyleft faible** : la redistribution impose de permettre le remplacement de la bibliothèque, de livrer sa licence et de fournir son source ou une offre de source **[JURISTE]** | **Exclu provisoirement** (décision du client). Usage possible en interne, comme référence croisée non livrée. Attention : le nom PyPI `sippy` désigne une autre bibliothèque (VoIP, BSD) |
| SysIdentPy (`sysidentpy`) | BSD-3-Clause (« Copyright (c) 2019, Wilson Rocha; Luan Pascoal… ») | 0.9.0 (13/06/2026), « 5 - Production/Stable » | Commit du 11/08/2026 selon E-referentiel | numpy **≤ 2.3.5**, scipy, matplotlib | Permissive | La borne numpy bloque toute montée de version de numpy. Orientée NARMAX, peu adaptée au FOPDT à retard continu |
| statsmodels (`statsmodels`) | BSD-3-Clause | 0.15.0 (27/08/2026), « 4 - Beta » | Active | numpy, scipy, pandas 3.1.1 (BSD-3-Clause), patsy 1.0.3 (« 2-clause BSD »), packaging 26.4 (Apache-2.0 OR BSD-2-Clause), formulaic 1.2.2 (MIT) — licences lues sur PyPI le 27/11/2026, dépendances de second rang non relues | Permissive | Tests de résidus (Ljung-Box), ARIMA/ARMAX. Lourd (pandas) |
| nfoursid (`nfoursid`) | MIT | 1.0.2 (24/07/2025), « 5 - Production/Stable » | Faible (petit projet) | matplotlib, numpy, pandas | Permissive | Sous-espaces N4SID ; possible référence croisée. Mainteneur unique |
| harold (`harold`) | MIT | 1.0.3 (04/12/2022) | **Inactive** depuis 2022 (dernière publication PyPI) | scipy, matplotlib, tabulate (MIT) | Permissive | Risque de maintenance |
| PySINDy (`pysindy`) | MIT | 2.1.0 (08/01/2026), « 5 - Production/Stable » | Active | scikit-learn (BSD-3-Clause), numpy, derivative 0.6.3 (MIT), scipy, typing_extensions (non relu) | Permissive | Identification parcimonieuse non linéaire : hors du besoin FOPDT/SOPDT |
| CasADi (`casadi`) | **LGPL-3.0+** | 3.8.2 (27/10/2026) | Active | numpy ; embarque des solveurs tiers (dont IPOPT, licence EPL ; non relu ici) | Copyleft faible, plus licences des solveurs **[JURISTE]** | Seulement via SIPPY. À éviter dans le produit livré |
| GEKKO (`gekko`) | MIT | 1.3.3 (23/10/2026) | Active (BYU-PRISM) | numpy | Code MIT ; les **solveurs** (APOPT, etc.) et le **mode serveur distant**, actif par défaut selon la documentation (non relue ici), relèvent d'autres conditions **[JURISTE]** | À ne pas livrer sans examen : risque d'envoi de données vers un serveur tiers |
| scikit-learn (`scikit-learn`) | BSD-3-Clause | 1.9.1 (10/09/2026) | Très active | numpy, scipy, joblib 1.6.0 (BSD-3-Clause), threadpoolctl 3.8.0 (BSD-3-Clause), narwhals (non relu) | Permissive | Seulement si un besoin de régression ou de validation croisée l'exige |
| nonlinear-benchmarks (`nonlinear-benchmarks`) | Champ licence : « BSD 3-Clause License » ; classificateur PyPI : « Other/Proprietary » (contradiction) ; dépôt `MaartenSchoukens/nonlinear_benchmarks` : `LICENSE` BSD-3-Clause, « Copyright (c) 2023-2024, Gerben Beintema » (commit `f9fb388`, 19/06/2026) | 1.0.1 (20/10/2025) | Active | numpy, scipy, requests, tqdm, progressbar, **rarfile** | Code permissif. **Ne couvre pas les données téléchargées** (voir `jeux-de-donnees.md`) | Outil de test interne seulement |

**Lecture d'ensemble** : la pile actuelle (NumPy, SciPy) reste permissive et suffit au besoin. Les candidats permissifs (python-control, statsmodels, nfoursid) conviennent comme **références croisées** dans les tests, sans être livrés. SIPPY et CasADi (LGPL) restent hors du produit livré tant qu'un juriste ne s'est pas prononcé.
