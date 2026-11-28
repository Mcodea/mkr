"""Outillage du dispositif de qualification indépendante (protocole directeur §7, §21). Ce script NE réalise PAS la validation à l'aveugle : il fournit les
preuves qui la rendent possible et vérifiable. L'évaluateur indépendant l'exécute, pas le développeur.

  freeze   REPO MANIFEST       fige le moteur : commit Git, empreinte SHA-256 de chaque fichier du moteur et des critères, versions des bibliothèques
  verify   REPO MANIFEST       refuse (code 2) si l'arbre de travail diffère du manifeste (moteur modifié depuis le gel)
  seal     RESULTS_DIR SEAL    scelle les résultats d'ORIAS (empreintes + date) AVANT toute révélation des références
  check    RESULTS_DIR SEAL    vérifie que les résultats n'ont pas changé depuis le scellement
  reveal-allowed RESULTS_DIR SEAL   code 0 seulement si le scellement existe ET correspond : la comparaison aux références est alors permise

Aucune étape ne lit de références. Le scellement est un horodatage déclaratif + empreintes : il détecte une modification, il ne prouve pas l'indépendance
de l'évaluateur (qui relève de l'organisation : accès séparés, données hors du dépôt de développement).
"""
import hashlib, json, subprocess, sys
from datetime import datetime, timezone
from pathlib import Path

FROZEN_DIRS = ("engine", "mkr_data_import", "contracts")
FROZEN_SUFFIXES = (".py", ".json")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def tracked_files(repo):
    files = []
    for d in FROZEN_DIRS:
        base = Path(repo) / d
        if base.is_dir():
            files += [p for p in base.rglob("*") if p.is_file() and p.suffix in FROZEN_SUFFIXES and "__pycache__" not in p.parts and not p.name.startswith("test_")]
    return sorted(files)


def git_commit(repo):
    try:
        return subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return None


def libraries():
    out = {"python": sys.version.split()[0]}
    for name in ("numpy", "scipy"):
        try:
            out[name] = __import__(name).__version__
        except ImportError:
            out[name] = None
    return out


def freeze(repo, manifest):
    repo = Path(repo)
    entries = {p.relative_to(repo).as_posix(): sha256(p) for p in tracked_files(repo)}
    body = dict(kind="orias-engine-freeze", git_commit=git_commit(repo), libraries=libraries(), files=entries,
                tree_sha256=hashlib.sha256(json.dumps(entries, sort_keys=True).encode()).hexdigest())
    Path(manifest).write_text(json.dumps(body, indent=1, sort_keys=True))
    return body


def verify(repo, manifest):
    frozen = json.loads(Path(manifest).read_text())
    repo = Path(repo)
    now = {p.relative_to(repo).as_posix(): sha256(p) for p in tracked_files(repo)}
    changed = sorted(k for k in set(now) | set(frozen["files"]) if now.get(k) != frozen["files"].get(k))
    return changed


def seal(results_dir, seal_path):
    files = {p.relative_to(results_dir).as_posix(): sha256(p) for p in sorted(Path(results_dir).rglob("*")) if p.is_file()}
    if not files:
        raise SystemExit("Aucun résultat à sceller.")
    body = dict(kind="orias-results-seal", sealed_at_utc=datetime.now(timezone.utc).isoformat(), files=files,
                seal_sha256=hashlib.sha256(json.dumps(files, sort_keys=True).encode()).hexdigest())
    Path(seal_path).write_text(json.dumps(body, indent=1, sort_keys=True))
    return body


def check(results_dir, seal_path):
    sealed = json.loads(Path(seal_path).read_text())
    now = {p.relative_to(results_dir).as_posix(): sha256(p) for p in sorted(Path(results_dir).rglob("*")) if p.is_file()}
    return sorted(k for k in set(now) | set(sealed["files"]) if now.get(k) != sealed["files"].get(k))


def main(argv):
    if len(argv) != 4 or argv[1] not in ("freeze", "verify", "seal", "check", "reveal-allowed"):
        print(__doc__); return 64
    command, a, b = argv[1], argv[2], argv[3]
    if command == "freeze":
        body = freeze(a, b); print("figé :", body["git_commit"], body["tree_sha256"]); return 0
    if command == "verify":
        changed = verify(a, b); print("écarts :", changed or "aucun"); return 2 if changed else 0
    if command == "seal":
        body = seal(a, b); print("scellé :", body["sealed_at_utc"], body["seal_sha256"]); return 0
    changed = check(a, b)
    if command == "check":
        print("écarts :", changed or "aucun"); return 2 if changed else 0
    print("comparaison aux références PERMISE" if not changed else "comparaison REFUSÉE : les résultats ont changé depuis le scellement", changed or "")
    return 2 if changed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
