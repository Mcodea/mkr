"""PID_INT conversion (Schneider 33002520, p. 141). Documentary, never qualified.

The oracle below is the per-cycle equation of the manual: DOUT = KP [de + TS/(10 TI) e] / 100, in counts [0..10000].
"""
import math
import unittest

from block_adapters import pid_int
from block_catalogue import convert, lookup


def simulate_block(registers, gain, tau, delay_steps, ts, horizon, sp_counts):
    """First-order process in counts driven by the documented incremental PI; returns the PV trajectory."""
    kp, ti_ratio = registers["KP"] / 100, ts / (registers["TI"] / 10)
    out, previous, pv, queue, trajectory = 0.0, 0.0, 0.0, [0.0] * delay_steps, []
    for _ in range(int(horizon / ts)):
        e = sp_counts - pv
        out += kp * ((e - previous) + ti_ratio * e)
        previous = e
        queue.append(out)
        pv += (1 - math.exp(-ts / tau)) * (gain * queue.pop(0) - pv)
        trajectory.append(pv)
    return trajectory


def simulate_mkr(kp, ti, gain, tau, delay, ts, horizon, sp, pv_span, mv_span):
    """MKR continuous ideal PI integrated finely, in physical units, same process."""
    steps = 20
    h = ts / steps
    integral, pv, queue, trajectory = 0.0, 0.0, [0.0] * round(delay / h), []
    for _ in range(int(horizon / ts)):
        for _ in range(steps):
            e = sp - pv
            integral += e * h
            queue.append(kp * (e + integral / ti))
            pv += h / tau * (gain * queue.pop(0) - pv)
        trajectory.append(pv)
    return trajectory


class ConversionEquivalence(unittest.TestCase):
    def test_registers_follow_the_documented_units_and_scaling(self):
        result = pid_int(dict(kp=2.0, tiSeconds=12.5, tdSeconds=0, tsSeconds=0.2, pvSpan=200.0, mvSpan=100.0))
        self.assertEqual(result["parameters"]["KP"], 400)           # KP = 2 x 200 / 100 = 4, x100
        self.assertEqual((result["parameters"]["TI"], result["parameters"]["TS"]), (125, 20))
        self.assertEqual((result["qualification"], result["readyToLoad"]), ("non_qualified", False))

    def test_negative_gain_keeps_its_sign(self):
        self.assertEqual(pid_int(dict(kp=-1.0, tiSeconds=10, tdSeconds=0, tsSeconds=0.1, pvSpan=100, mvSpan=100))["parameters"]["KP"], -100)

    def test_the_block_equation_and_mkr_converge_to_the_same_loop(self):
        # Process gain 1.5 in physical units; PV span 50, MV span 100. Setpoint 10 units = 2000 counts.
        kp, ti, gain, tau, delay, pv_span, mv_span = 0.8, 20.0, 1.5, 40.0, 4.0, 50.0, 100.0
        errors = []
        for ts in (0.4, 0.2, 0.1):
            registers = pid_int(dict(kp=kp, tiSeconds=ti, tdSeconds=0, tsSeconds=ts, pvSpan=pv_span, mvSpan=mv_span))["parameters"]
            counts_gain = gain * mv_span / pv_span            # counts of PV per count of MV
            block = simulate_block(registers, counts_gain, tau, round(delay / ts), ts, 200, 10 / pv_span * 10000)
            mkr = simulate_mkr(kp, ti, gain, tau, delay, ts, 200, 10.0, pv_span, mv_span)
            errors.append(max(abs(b * pv_span / 10000 - m) for b, m in zip(block, mkr)))
        self.assertLess(errors[2], errors[1])
        self.assertLess(errors[1], errors[0])
        self.assertLess(errors[2], 0.06 * 10)

    def test_a_wrong_scaling_is_visible(self):
        good = pid_int(dict(kp=1, tiSeconds=10, tdSeconds=0, tsSeconds=0.1, pvSpan=50, mvSpan=100))["parameters"]["KP"]
        wrong = pid_int(dict(kp=1, tiSeconds=10, tdSeconds=0, tsSeconds=0.1, pvSpan=100, mvSpan=50))["parameters"]["KP"]
        self.assertEqual((good, wrong), (50, 200))


class DocumentedRules(unittest.TestCase):
    def test_what_the_documentation_does_not_settle_is_refused(self):
        base = dict(kp=1, tiSeconds=10, tdSeconds=0, tsSeconds=0.1, pvSpan=100, mvSpan=100)
        cases = {
            "conversion_derivative_undocumented": dict(base, tdSeconds=1),
            "conversion_range": dict(base, kp=500),                       # |KP register| > 10000
            "conversion_parameters": dict(base, tiSeconds=0),
        }
        cases["conversion_range"] = dict(base, tiSeconds=3000)            # TI > 20000 tenths
        for reason, parameters in cases.items():
            with self.subTest(reason=reason):
                with self.assertRaises(ValueError) as caught:
                    pid_int(parameters)
                self.assertEqual(str(caught.exception), reason)
        with self.assertRaises(ValueError):
            pid_int(dict(base, kp=300))                                   # KP register 30000
        with self.assertRaises(ValueError):
            pid_int(dict(base, tsSeconds=400))                            # TS > 32000 hundredths
        with self.assertRaises(ValueError):
            pid_int(dict(kp=1, tiSeconds=10))

    def test_catalogue_entry_is_filled_from_the_document_only(self):
        entry, = lookup({"name": "PID_INT"})["entries"]
        self.assertIn("ΔOUT", entry["equation"])
        self.assertEqual({p["name"] for p in entry["parameters"]}, {"KP", "TI", "TD", "TS", "PV_DEV"})
        self.assertEqual(entry["status"], "to_verify")
        self.assertFalse(entry["selectable"])
        sources = {s["id"]: s for s in lookup({})["sources"]}
        self.assertEqual(sources["SE-33002520K01000"]["edition"], "09/2020 (33002520.20)")

    def test_catalogue_conversion_goes_through_the_adapter(self):
        result = convert(dict(name="PID_INT", target={}, parameters=dict(kp=1, tiSeconds=10, tdSeconds=0, tsSeconds=0.1, pvSpan=100, mvSpan=100)))
        self.assertTrue(result["converted"])
        self.assertFalse(result["conversion"]["readyToLoad"])
        self.assertIn("document_inconsistencies", result["conversion"]["warnings"])


if __name__ == "__main__":
    unittest.main()
