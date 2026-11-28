"""Download, verify and describe the REAL open datasets considered for the LOT 4 study.

Runs ONLY on demand (a sub-command is required; importing this module does nothing).
No raw data is written to the repository: every download goes to ``--dest`` (one new
sub-folder per dataset). Archives are read in memory with ``zipfile``; nothing that is
downloaded is extracted to disk or executed.

Usage (from any directory, with an isolated interpreter):
    python -I fetch_and_describe.py fetch    --dest /path/to/scratch/real [--include-large]
    python -I fetch_and_describe.py verify   --dest /path/to/scratch/real
    python -I fetch_and_describe.py describe --dest /path/to/scratch/real
    python -I fetch_and_describe.py screen   --dest /path/to/scratch/real

Integrity: Zenodo publishes an MD5 per file (``checksum`` field of the record API); it
does not publish SHA-256. ``verify`` checks BOTH the MD5 published by Zenodo and the
SHA-256 recorded by ORIAS at the first download (2026-11-28). The SHA-256 values are
ours, not the publisher's.

``screen`` applies the step-screening rule declared below (written before the screening
was run) and prints every candidate; it never chooses a segment silently.
xlsx files need openpyxl (pinned in engine/requirements.txt).
"""
import argparse
import csv
import hashlib
import io
import json
import statistics
import sys
import urllib.request
import zipfile
from pathlib import Path

ZENODO_API = "https://zenodo.org/api/records/"

# key: (zenodo record, file key, published md5, ORIAS sha256 of 2026-11-28, size, sub-folder, large?)
DATASETS = {
    "tclab_takehome": (24286966, "tclab_takehome_labs_dataset_v1.0.zip", "143ac608400a06d2ffc26535d507c447",
                       "a12f6fe10ca4491fb1728293d50b18e9e0bd88ff760393a76dd9938d52da47e4", 834181, "tclab_takehome", False),
    "intaset": (17652851, "InTaSet.zip", "0fe80940a3c9520e665b769f4b8d5ab8",
                "a0aac3b0985bec232afa8f9e95cd87612515904f5e574bc483777e4d532e8c85", 6107043, "intaset", False),
    "cstr_manipal": (22753301, "CSTR_24aug_PRBS_dataset.xlsx", "524c0276a5d199cd10f341827135684e",
                     "0e42ce8af6e05cdcd3c8a39eb1e0d54c9478a9623475d1bc64408e3c23d3d3bd", 297991, "cstr_manipal", False),
    "distil_manipal": (22763516, "DistillationColumn (1).zip", "fdcef796282deca5ed89f4b1c15c3e6e",
                       "3b19f8dbeb5b74f68b0c732fe27533bde9bf1080e7f6a799761b2b3dab5517df", 1843892, "distil_manipal", False),
    "consens_vars": (1438233, "matlab_variables_explanation.xlsx", "3d4fe9c47f220226e361b10f1d26b024",
                     "79a9ffc8da5b65b8326478d1497bd6ac7230d64585b4e6399c7de95acefaf732", 12395, "consens", False),
    "consens_pcs": (1438233, "PCS_data_csv.zip", "f8e1c5aed2a77ef0a77479d0a8bf3da9",
                    "ebf36fbcdd8340c6f06f9e66eed1ac9b59a63dcb4ad96bb49dfe8d5cad8faa68", 42679134, "consens", True),
}

# Step-screening rule (declared before screening; same rule for every dataset):
#  * a single manipulated input changes at row c while the other inputs stay constant;
#  * baseline = last min(30 s, previous plateau) before c, at least 10 s long; its linear drift
#    over its own length must be < 10 % of the response amplitude |dY|;
#  * the new plateau lasts at least 30 s and the drift over its last 10 s is < 5 % of |dY|
#    ("response sufficiently settled");
#  * |dY| >= 10 x standard deviation of the baseline.
RULE = dict(pre_max_s=30.0, pre_min_s=10.0, post_min_s=30.0, end_s=10.0, base_drift=0.10, end_drift=0.05, snr_min=10.0)


def _sha256_md5(path):
    h, m = hashlib.sha256(), hashlib.md5()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
            m.update(chunk)
    return h.hexdigest(), m.hexdigest()


def _get(url, timeout=300):
    with urllib.request.urlopen(url, timeout=timeout) as r:
        return r.read()


def cmd_fetch(dest, include_large):
    for key, (rec, fkey, md5, sha, size, sub, large) in DATASETS.items():
        if large and not include_large:
            print(f"[skip] {key}: {size} bytes, use --include-large")
            continue
        meta = json.loads(_get(ZENODO_API + str(rec), 60))
        lic = meta.get("metadata", {}).get("license", {}).get("id")
        files = {f["key"]: f for f in meta.get("files", [])}
        if fkey not in files:
            raise SystemExit(f"{key}: file {fkey!r} no longer in record {rec}")
        pub_md5 = files[fkey]["checksum"].split(":", 1)[1]
        print(f"{key}: record {rec} license.id={lic} published md5={pub_md5}")
        if pub_md5 != md5:
            raise SystemExit(f"{key}: published MD5 changed ({pub_md5}); the record was revised, re-inspect it")
        folder = dest / sub
        folder.mkdir(parents=True, exist_ok=True)
        target = folder / fkey
        if not target.exists():
            target.write_bytes(_get(files[fkey]["links"]["self"]))
        _check(key, target, md5, sha)


def _check(key, path, md5, sha):
    got_sha, got_md5 = _sha256_md5(path)
    ok = got_md5 == md5 and got_sha == sha
    print(f"  {'OK ' if ok else 'BAD'} {path.name} md5={got_md5} sha256={got_sha}")
    if not ok:
        raise SystemExit(f"{key}: checksum mismatch")


def cmd_verify(dest):
    for key, (_, fkey, md5, sha, _, sub, _) in DATASETS.items():
        p = dest / sub / fkey
        if p.exists():
            _check(key, p, md5, sha)
        else:
            print(f"  --- {key}: not downloaded")


# ---------- readers (in memory, no extraction) ----------
def _csv_from_zip(zpath, member, delimiter=","):
    with zipfile.ZipFile(zpath) as z, z.open(member) as fh:
        return list(csv.reader(io.TextIOWrapper(fh, encoding="utf-8-sig"), delimiter=delimiter))


def _xlsx_rows(data_or_path):
    import openpyxl  # pinned in engine/requirements.txt
    src = io.BytesIO(data_or_path) if isinstance(data_or_path, bytes) else data_or_path
    wb = openpyxl.load_workbook(src, read_only=True, data_only=True)
    return {ws.title: list(ws.iter_rows(values_only=True)) for ws in wb.worksheets}


def _changes(v):
    return [(i, v[i - 1], v[i]) for i in range(1, len(v)) if v[i] != v[i - 1]]


def _slope(v, dt):
    n = len(v)
    xm, ym = (n - 1) / 2, statistics.mean(v)
    return sum((i - xm) * (y - ym) for i, y in enumerate(v)) / sum((i - xm) ** 2 for i in range(n)) / dt


def screen_steps(t, inputs, outputs, label):
    """inputs/outputs: dict name -> list of floats (same length as t). Prints candidates per RULE."""
    dt = statistics.median(t[i] - t[i - 1] for i in range(1, len(t)))
    allch = sorted({i for v in inputs.values() for i, _, _ in _changes(v)})
    found = 0
    for u_name, u in inputs.items():
        for c, a, b in _changes(u):
            if any(w[c] != w[c - 1] for n, w in inputs.items() if n != u_name):
                continue
            prev = max([i for i in allch if i < c], default=0)
            nxt = min([i for i in allch if i > c], default=len(t))
            pre_n = min(int(RULE["pre_max_s"] / dt), c - prev)
            post_n, end_n = nxt - c, int(RULE["end_s"] / dt)
            if pre_n * dt < RULE["pre_min_s"] or post_n * dt < RULE["post_min_s"]:
                continue
            for y_name, y in outputs.items():
                pre, end = y[c - pre_n:c], y[nxt - end_n:nxt]
                dy, sd0 = statistics.mean(end) - statistics.mean(pre), statistics.pstdev(pre)
                if abs(dy) < RULE["snr_min"] * max(sd0, 1e-12):
                    continue
                base_ok = abs(_slope(pre, dt)) * pre_n * dt < RULE["base_drift"] * abs(dy)
                end_ok = abs(_slope(end, dt)) * RULE["end_s"] < RULE["end_drift"] * abs(dy)
                verdict = "CANDIDATE" if base_ok and end_ok else ("baseline-drift" if not base_ok else "not-settled")
                found += verdict == "CANDIDATE"
                # Diagnostic only (not part of the declared rule): end drift per 100 s relative to |dY|.
                # The rule's windows are absolute (seconds); for a slow process they can call "settled"
                # a response that is still moving. This figure makes that visible.
                drift100 = abs(_slope(end, dt)) * 100.0 / abs(dy)
                print(f"  {label} {u_name} row {c} t={t[c]:g} {a}->{b} pre {pre_n*dt:.0f}s post {post_n*dt:.0f}s "
                      f"{y_name} dY={dy:.3f} sd0={sd0:.3f} SNR={abs(dy)/max(sd0,1e-12):.0f} "
                      f"end-drift/100s={drift100:.0%} -> {verdict}")
    print(f"  {label}: {found} candidate segment(s) under the declared rule")


# ---------- per-dataset description / screening ----------
TCLAB_FILES = ["data/experiment_siso_model_validation.csv", "data/experiment_mimo_robust_control_real.csv"]
INTASET_ID = ["InTaSet/InTaSet-ID/InTaSet-ID-2T/InTaSet-ID-2T_training.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-2T/InTaSet-ID-2T_testing.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-3T/InTaSet-ID-3T_Training.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-3T/InTaSet-ID-3T_Testing.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-4T-M1/InTaSet-ID-4T-M1_Training/InTaSet-ID-4T-M1_Training.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-4T-M1/InTaSet-ID-4T-M1_Testing/InTaSet-ID-4T-M1_Testing.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-4T-M2/InTaSet-ID-4T-M2_Training/InTaSet-ID-4T-M2_Training.xlsx",
              "InTaSet/InTaSet-ID/InTaSet-ID-4T-M2/InTaSet-ID-4T-M2_Testing/InTaSet-ID-4T-M2_Testing.xlsx"]


def _tclab(dest, do_screen):
    z = dest / "tclab_takehome" / DATASETS["tclab_takehome"][1]
    for m in TCLAB_FILES:
        rows = _csv_from_zip(z, m)
        hdr, data = rows[0], rows[1:]
        print(f"## tclab_takehome {m}: {len(data)} data rows, columns {hdr}")
        col = {h: i for i, h in enumerate(hdr)}
        for h in hdr:
            v = [r[col[h]] for r in data]
            try:
                f = [float(x) for x in v]
                print(f"   {h}: min {min(f):g} max {max(f):g} changes {len(_changes(f))}")
            except ValueError:
                print(f"   {h}: values {sorted(set(v))[:5]}")
        if do_screen and "siso" in m:
            t = [float(r[col["time_s"]]) for r in data]
            ins = {q: [float(r[col[q]]) for r in data] for q in ("Q1_pct", "Q2_pct")}
            outs = {q: [float(r[col[q]]) for r in data] for q in ("T1_meas_C", "T2_meas_C")}
            # row 0 is the TCLab model default 23/23 (not a measurement): excluded
            screen_steps(t[1:], {k: v[1:] for k, v in ins.items()}, {k: v[1:] for k, v in outs.items()},
                         "tclab_siso(row offset 1)")


def _intaset(dest, do_screen):
    z = dest / "intaset" / DATASETS["intaset"][1]
    with zipfile.ZipFile(z) as arc:
        names = [n for n in arc.namelist() if n.endswith(".xlsx")]
        for n in names:
            sheets = _xlsx_rows(arc.read(n))
            for title, rows in sheets.items():
                hdr = [str(h).strip(" -") if h is not None else ("Time" if i == 0 else f"unnamed{i}")
                       for i, h in enumerate(rows[0])]
                data = [r for r in rows[1:] if r[1] is not None]
                t = [r[0] for r in data]
                print(f"## intaset {n} [{title}]: {len(data)} data rows, t {t[0]}..{t[-1]}, columns {hdr}")
                if do_screen and n in INTASET_ID:
                    col = {h: i for i, h in enumerate(hdr)}
                    ins = {h: [r[col[h]] for r in data] for h in hdr if "Pump" in h}
                    outs = {h: [r[col[h]] for r in data] for h in hdr if "Tank" in h}
                    screen_steps(t, ins, outs, n.rsplit("/", 1)[-1])


def _manipal(dest, do_screen=False):
    rows = _xlsx_rows(str(dest / "cstr_manipal" / DATASETS["cstr_manipal"][1]))["CSTR Dataset"]
    data = [r for r in rows[2:] if r[0] is not None]
    print(f"## cstr_manipal: header {rows[1]}; {len(data)} data rows (sheet rows 3..), t {data[0][0]}..{data[-1][0]} s")
    if do_screen:
        t = [float(r[0]) for r in data]
        ins = {str(rows[1][c]).strip(): [float(r[c]) for r in data] for c in range(1, 5)}
        screen_steps(t, ins, {"Reactor Temp. Tr (degC)": [float(r[5]) for r in data]}, "cstr_manipal")
        return
    for c in range(1, 5):
        v = [r[c] for r in data]
        print(f"   {str(rows[1][c]).strip()}: levels {sorted(set(v))}, changes {[(i, a, b) for i, a, b in _changes(v)]}")
    with zipfile.ZipFile(dest / "distil_manipal" / DATASETS["distil_manipal"][1]) as arc:
        for n in arc.namelist():
            sheet = next(iter(_xlsx_rows(arc.read(n)).values()))
            print(f"## distil_manipal {n}: {len(sheet)} rows x {max(len(r) for r in sheet)} column(s), first {sheet[0]}, "
                  f"no header, no time column, no input column")


def _consens(dest):
    z = dest / "consens" / DATASETS["consens_pcs"][1]
    if not z.exists():
        print("## consens: PCS_data_csv.zip not downloaded (use fetch --include-large)")
        return
    with zipfile.ZipFile(z) as arc:
        for n in arc.namelist():
            if n.endswith(".csv"):
                with arc.open(n) as fh:
                    hdr = fh.readline().decode("latin-1").strip()
                print(f"## consens {n}: {hdr[:200]}")


def cmd_describe(dest, do_screen):
    _tclab(dest, do_screen)
    _intaset(dest, do_screen)
    _manipal(dest, do_screen)
    if not do_screen:
        _consens(dest)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["fetch", "verify", "describe", "screen"])
    ap.add_argument("--dest", required=True, type=Path, help="scratch folder OUTSIDE the repository")
    ap.add_argument("--include-large", action="store_true", help="also fetch CONSENS PCS_data_csv.zip (42.7 MB)")
    a = ap.parse_args(argv)
    repo = Path(__file__).resolve().parents[4]
    if repo in a.dest.resolve().parents or a.dest.resolve() == repo:
        raise SystemExit("--dest must be outside the repository (no raw data in the repository)")
    if a.command == "fetch":
        cmd_fetch(a.dest, a.include_large)
    elif a.command == "verify":
        cmd_verify(a.dest)
    else:
        cmd_describe(a.dest, a.command == "screen")


if __name__ == "__main__":
    sys.exit(main())
