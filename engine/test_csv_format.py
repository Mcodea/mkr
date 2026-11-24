import base64
import json
import math
import unittest
from datetime import datetime, timedelta, timezone

from csv_format import Format, from_payload, suggest
from csv_quality import process_csv
from fopdt import identify, read_step
from mkr_engine import respond
from step_finder import find
from trial_preview import preview

FRENCH = dict(decimal=",", time=dict(pattern="%d/%m/%Y %H:%M:%S", zone="Europe/Paris"))


def encode(text):
    return base64.b64encode(text.encode()).decode()


class NumberTests(unittest.TestCase):
    def test_decimal_comma_and_point_are_never_mixed(self):
        comma, point = Format(","), Format(".")
        self.assertEqual([comma.number(c) for c in ("12,5", "-,5", "1,5e-3", "7", " 3,25 ")], [12.5, -0.5, 0.0015, 7.0, 3.25])
        self.assertEqual([point.number(c) for c in ("12.5", "-.5", "1.5e-3", "7")], [12.5, -0.5, 0.0015, 7.0])
        for cell in ("1.5", "1,234.5", "1,5,5", "abc", "", "1,5 bar", "nan", "inf"):
            self.assertTrue(math.isnan(comma.number(cell)), cell)       # a point is refused in comma mode
        for cell in ("1,5", "1.234,5", "1;5"):
            self.assertTrue(math.isnan(point.number(cell)), cell)       # a comma is refused in point mode

    def test_a_decimal_comma_cannot_share_the_field_separator(self):
        with self.assertRaisesRegex(ValueError, "csv_format"):
            from_payload(dict(format=dict(decimal=",", time=None)), ",")
        self.assertEqual(from_payload(dict(format=dict(decimal=",", time=None)), ";").decimal, ",")
        for bad in (dict(decimal="x", time=None), dict(decimal=".", time=dict(pattern="%d/%m/%y", zone="UTC")),
                    dict(decimal=".", time=dict(pattern="%d/%m/%Y %H:%M:%S", zone="../etc")), dict(decimal=".")):
            with self.subTest(bad=bad), self.assertRaisesRegex(ValueError, "csv_format"):
                from_payload(dict(format=bad), ";")


class LocalTimeTests(unittest.TestCase):
    def setUp(self):
        self.paris = Format(",", "%d/%m/%Y %H:%M:%S", "Europe/Paris")

    def test_summer_and_winter_offsets(self):
        # Paris is UTC+2 in summer and UTC+1 in winter (EU rule: last Sundays of March and October).
        self.assertEqual(self.paris.timestamp("01/07/2026 12:00:00"), datetime(2026, 7, 1, 10, 0, tzinfo=timezone.utc))
        self.assertEqual(self.paris.timestamp("01/01/2026 12:00:00"), datetime(2026, 1, 1, 11, 0, tzinfo=timezone.utc))
        self.assertEqual(Format(".", "%Y-%m-%d %H:%M:%S", "UTC").timestamp("2026-07-01 12:00:00"),
                         datetime(2026, 7, 1, 12, 0, tzinfo=timezone.utc))

    def test_nonexistent_and_ambiguous_local_times_are_refused_not_guessed(self):
        with self.assertRaisesRegex(ValueError, "nonexistent_local_time"):
            self.paris.timestamp("29/03/2026 02:30:00")                  # clocks jump from 02:00 to 03:00
        with self.assertRaisesRegex(ValueError, "ambiguous_local_time"):
            self.paris.timestamp("25/10/2026 02:30:00")                  # the hour 02:00-03:00 happens twice
        self.assertEqual(self.paris.timestamp("29/03/2026 03:00:00"), datetime(2026, 3, 29, 1, 0, tzinfo=timezone.utc))
        self.assertEqual(self.paris.timestamp("25/10/2026 03:00:00"), datetime(2026, 10, 25, 2, 0, tzinfo=timezone.utc))
        for bad in ("2026-07-01 12:00:00", "31/02/2026 10:00:00", "01/07/2026", ""):
            with self.assertRaises(ValueError):
                self.paris.timestamp(bad)

    def test_unknown_zone_is_refused(self):
        with self.assertRaisesRegex(ValueError, "csv_timezone"):
            Format(".", "%d/%m/%Y %H:%M:%S", "Europe/Atlantis")


def table(rows, decimal=",", fmt="%d/%m/%Y %H:%M:%S", zone="Europe/Paris", start=datetime(2026, 7, 1, 8, 0, 0), period=10):
    """Same process signals written as a French export: local naive timestamps and a decimal comma."""
    lines = ["Horodatage;Commande;Mesure"]
    for i, (u, y) in enumerate(rows):
        local = (start + timedelta(seconds=i * period)).strftime(fmt)
        lines.append(f"{local};{str(u).replace('.', decimal)};{repr(y).replace('.', decimal)}")
    return "\n".join(lines) + "\n"


def signals(n=70, step_at=6, tau=40.0, delay=15.0, gain=-3.0, amplitude=2.0, period=10):
    rows = []
    for i in range(n):
        t = (i - step_at) * period
        y = 7 + gain * amplitude * (1 - math.exp(-max(t - delay, 0) / tau))
        rows.append((amplitude if t >= 0 else 0.0, y))
    return rows


def iso_table(rows, start=datetime(2026, 7, 1, 6, 0, 0, tzinfo=timezone.utc), period=10):
    lines = ["Horodatage,Commande,Mesure"]
    for i, (u, y) in enumerate(rows):
        lines.append(f"{(start + timedelta(seconds=i * period)).isoformat()},{u!r},{y!r}")
    return "\n".join(lines) + "\n"


class FrenchExportEquivalenceTests(unittest.TestCase):
    """The same recording read as ISO/UTC and as a French local export must give identical results."""

    def sources(self, rows, **kw):
        french = dict(raw=encode(table(rows, **kw)), delimiter=";", timeColumn="Horodatage", mvColumn="Commande", pvColumn="Mesure", format=FRENCH)
        iso = dict(raw=encode(iso_table(rows)), delimiter=",", timeColumn="Horodatage", mvColumn="Commande", pvColumn="Mesure")
        return french, iso

    def test_quality_preview_and_step_reader_agree_with_the_iso_reading(self):
        rows = signals()
        french, iso = self.sources(rows)
        quality = lambda s: process_csv(dict(raw=s["raw"], delimiter=s["delimiter"], timeColumn="Horodatage", valueColumn="Mesure",
                                             role="PV", unit="bar", **({"format": s["format"]} if "format" in s else {})), False)["quality"]
        q_fr, q_iso = quality(french), quality(iso)
        self.assertEqual((q_fr["validValues"], q_fr["invalidValues"], q_fr["invalidTimestamps"]), (70, 0, 0))
        for key in ("validValues", "minimum", "maximum", "minPositiveStepSeconds", "maxPositiveStepSeconds", "duplicateTimestamps"):
            self.assertEqual(q_fr[key], q_iso[key], key)
        # Without the declared format the same French file is refused, never misread.
        refused = process_csv(dict(raw=french["raw"], delimiter=";", timeColumn="Horodatage", valueColumn="Mesure", role="PV", unit="bar"), False)
        self.assertEqual((refused["quality"]["validValues"], refused["quality"]["invalidTimestamps"]), (0, 70))
        p_fr = preview({k: v for k, v in french.items()})
        p_iso = preview(iso)
        self.assertEqual([(p["seconds"], p["mv"], p["pv"]) for p in p_fr["points"]], [(p["seconds"], p["mv"], p["pv"]) for p in p_iso["points"]])
        self.assertEqual(p_fr["firstUtc"], p_iso["firstUtc"])               # 08:00 in Paris in July is 06:00 UTC

    def test_identification_is_identical_across_the_two_notations(self):
        rows_a, rows_b = signals(amplitude=2.0), signals(amplitude=-1.5)
        fa, ia = self.sources(rows_a)
        fb, ib = self.sources(rows_b, start=datetime(2026, 7, 2, 8, 0, 0))
        ib = dict(ib, raw=encode(iso_table(rows_b, start=datetime(2026, 7, 2, 6, 0, 0, tzinfo=timezone.utc))))
        french = identify(dict(training=fa, validation=fb, mvTolerance=0.01, pvTolerance=0.01))
        iso = identify(dict(training=ia, validation=ib, mvTolerance=0.01, pvTolerance=0.01))
        for key in ("gain", "tauSeconds", "delaySeconds"):
            self.assertAlmostEqual(french[key], iso[key], places=12, msg=key)
        self.assertAlmostEqual(french["gain"], -3.0, places=5)
        self.assertEqual(french["validation"]["rmse"], iso["validation"]["rmse"])

    def test_step_search_and_ipc_accept_the_declared_format(self):
        rows = signals(n=200) 
        french, _ = self.sources(rows)
        result = find(dict(source=french, mvTolerance=0.01, pvTolerance=0.01))
        self.assertEqual([c["stepRow"] for c in result["candidates"]], [6])
        reply = respond(json.dumps(dict(version=1, requestId="fr", operation="find_steps",
                                        payload=dict(source=french, mvTolerance=0.01, pvTolerance=0.01))).encode())
        self.assertEqual(len(reply["result"]["candidates"]), 1)
        bad = dict(french, format=dict(decimal=",", time=dict(pattern="%d/%m/%Y %H:%M:%S", zone="Europe/Atlantis")))
        with self.assertRaisesRegex(ValueError, "fit_mapping"):
            read_step(bad, 0.01, 0.01)


class DaylightSavingTests(unittest.TestCase):
    def test_a_recording_across_the_spring_change_keeps_a_uniform_period(self):
        # One reading per minute from 01:50 to 03:10 local on 29/03/2026: local times skip 02:00-03:00.
        lines, local = ["Horodatage;Commande;Mesure"], datetime(2026, 3, 29, 1, 50)
        utc_instant = Format(",", "%d/%m/%Y %H:%M:%S", "Europe/Paris").timestamp(local.strftime("%d/%m/%Y %H:%M:%S"))
        for i in range(21):
            instant = utc_instant + timedelta(minutes=i)
            from zoneinfo import ZoneInfo
            shown = instant.astimezone(ZoneInfo("Europe/Paris")).strftime("%d/%m/%Y %H:%M:%S")
            lines.append(f"{shown};0;{20 + i / 10}".replace(".", ","))
        raw = encode("\n".join(lines) + "\n")
        report = process_csv(dict(raw=raw, delimiter=";", timeColumn="Horodatage", valueColumn="Mesure", role="PV", unit="bar",
                                  format=FRENCH), False)["quality"]
        self.assertEqual((report["invalidTimestamps"], report["reversedTimestamps"]), (0, 0))
        self.assertEqual((report["minPositiveStepSeconds"], report["maxPositiveStepSeconds"]), (60.0, 60.0))
        # Read as UTC (the usual mistake) the same file would show a one-hour jump.
        naive = process_csv(dict(raw=raw, delimiter=";", timeColumn="Horodatage", valueColumn="Mesure", role="PV", unit="bar",
                                 format=dict(decimal=",", time=dict(pattern="%d/%m/%Y %H:%M:%S", zone="UTC"))), False)["quality"]
        self.assertEqual(naive["maxPositiveStepSeconds"], 3660.0)

    def test_autumn_ambiguous_hour_is_flagged_as_invalid_timestamps(self):
        lines = ["Horodatage;Mesure"] + [f"25/10/2026 02:{m:02d}:00;{m}" for m in (10, 20, 30)] + ["25/10/2026 03:05:00;9"]
        report = process_csv(dict(raw=encode("\n".join(lines) + "\n"), delimiter=";", timeColumn="Horodatage", valueColumn="Mesure",
                                  role="PV", unit="bar", format=FRENCH), False)["quality"]
        self.assertEqual(report["invalidTimestamps"], 3)                 # refused, not silently placed before or after the change


class SuggestionTests(unittest.TestCase):
    def test_the_first_rows_propose_a_format_without_applying_it(self):
        rows = [["01/07/2026 08:00:00", "0", "20,5"], ["01/07/2026 08:00:10", "0", "20,6"]]
        proposal = suggest(["Horodatage", "Commande", "Mesure"], rows)
        self.assertEqual(proposal, dict(decimal=",", timeColumn="Horodatage", timePattern="%d/%m/%Y %H:%M:%S", isoWithOffset=False))
        iso = suggest(["t", "v"], [["2026-07-01T08:00:00Z", "1.5"], ["2026-07-01T08:00:10Z", "1.6"]])
        self.assertEqual(iso, dict(decimal=".", timeColumn="t", timePattern=None, isoWithOffset=True))
        self.assertEqual(suggest(["a", "b"], [["x", "y"]]), dict(decimal=".", timeColumn=None, timePattern=None, isoWithOffset=False))
        # A flat integer-valued start must not hide the decimal comma that appears later in the file.
        long = [[f"01/07/2026 08:{i // 60:02d}:{i % 60:02d}", "8" if i < 400 else f"8,{i}"] for i in range(600)]
        self.assertEqual(suggest(["Horodatage", "Mesure"], long)["decimal"], ",")


if __name__ == "__main__":
    unittest.main()
