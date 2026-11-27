import math
import unittest

from block_adapters import pidff
from block_catalogue import convert
from fopdt_simulation import plant_step
from pid_simulation import calculate as simulate_mkr
from pidff_reference import PidffReference


def mkr_response(gain, tau, delay, kp, ti, td, tf, dt, duration, setpoint=1.0):
    payload = dict(gain=gain, tauSeconds=tau, delaySeconds=delay, sampleSeconds=dt, durationSeconds=duration, setpoint=setpoint,
                   minimumMv=-1e9, maximumMv=1e9, disturbance=0.0, disturbanceAtSeconds=0.0,
                   controller=dict(kp=kp, tiSeconds=ti, tdSeconds=td, filterSeconds=tf or 0.0))
    return [p["pv"] for p in simulate_mkr(payload)["points"][:-1]]


def block_response(gain, tau, delay, converted, dt, duration, setpoint=1.0):
    """Closed loop of the converted PIDFF parameters, written from the manual's cycle equations, on MKR's exact plant."""
    values = converted["parameters"]
    td = values["td"]["seconds"]
    block = PidffReference(values["kp"], values["ti"]["seconds"], td, values["kd"] or 1.0, values["rev_dir"], dt)
    block.prime(0.0, 0.0)
    advance = plant_step(gain, tau, delay, dt, duration)
    pv, inputs, trace = 0.0, [], []
    for _ in range(round(duration / dt)):
        trace.append(pv)
        inputs.append(block.step(pv, setpoint))
        pv = advance(pv, inputs)
    return trace


def deviation(first, second):
    return max(abs(a - b) for a, b in zip(first, second))


class ConversionEquivalence(unittest.TestCase):
    """Oracle: MKR's continuous-form simulation versus a block built from the manual's own per-cycle equations."""

    def check(self, gain, kp, ti, td, tf, tau=8.0, delay=1.0, duration=40.0):
        converted = pidff(dict(kp=kp, tiSeconds=ti, tdSeconds=td, filterSeconds=tf))
        errors = []
        for dt in (0.1, 0.05, 0.025):
            mkr = mkr_response(gain, tau, delay, kp, ti, td, tf, dt, duration)
            block = block_response(gain, tau, delay, converted, dt, duration)
            errors.append(deviation(mkr, block))
        return converted, errors

    def test_pi_positive_gain_converges_to_the_same_loop(self):
        converted, errors = self.check(gain=2.0, kp=0.9, ti=6.0, td=0.0, tf=None)
        self.assertFalse(converted["parameters"]["rev_dir"])               # K > 0: a positive deviation must LOWER the output
        self.assertLess(errors[1], 0.7 * errors[0])                          # first order in the cycle time: the gap halves
        self.assertLess(errors[2], 0.7 * errors[1])
        self.assertLess(errors[2], 0.01)                                     # < 1 % of the 1.0 setpoint step at dt = 25 ms

    def test_pid_with_derivative_on_the_measurement_converges_too(self):
        converted, errors = self.check(gain=2.0, kp=0.8, ti=6.0, td=1.2, tf=0.3)
        self.assertEqual(converted["parameters"]["kd"], 4.0)                 # kd = td / Tf
        self.assertLess(errors[1], 0.75 * errors[0])
        self.assertLess(errors[2], 0.75 * errors[1])
        self.assertLess(errors[2], 0.012)

    def test_negative_process_gain_uses_the_absolute_kp_and_the_other_direction(self):
        converted, errors = self.check(gain=-2.0, kp=-0.9, ti=6.0, td=0.0, tf=None)
        self.assertEqual(converted["parameters"]["kp"], 0.9)                 # the block refuses a negative kp (status 080)
        self.assertTrue(converted["parameters"]["rev_dir"])                  # rev_dir = 1: PV - SP > 0 raises the output
        self.assertLess(errors[1], 0.7 * errors[0])
        self.assertLess(errors[2], 0.7 * errors[1])
        self.assertLess(errors[2], 0.01)

    def test_the_wrong_direction_would_drive_the_loop_away(self):
        # Negative control of the oracle: flipping rev_dir turns the same loop into positive feedback.
        converted = pidff(dict(kp=0.9, tiSeconds=6.0, tdSeconds=0.0))
        flipped = dict(converted, parameters=dict(converted["parameters"], rev_dir=True))
        good = block_response(2.0, 8.0, 1.0, converted, 0.05, 40.0)
        bad = block_response(2.0, 8.0, 1.0, flipped, 0.05, 40.0)
        self.assertLess(abs(good[-1] - 1.0), 0.05)
        self.assertGreater(abs(bad[-1]), 10)

    def test_mixed_form_transfer_function_matches_the_documented_expression(self):
        # Frequency response of MKR's ideal form against the manual's formula (p. 173), at several complex frequencies.
        kp, ti, td, tf = 1.3, 7.0, 2.0, 0.5
        values = pidff(dict(kp=kp, tiSeconds=ti, tdSeconds=td, filterSeconds=tf))["parameters"]
        kd = values["kd"]
        for p in (0.05j, 0.4j, 2j, 0.3 + 1.1j):
            manual = values["kp"] * (1 + 1 / (values["ti"]["seconds"] * p) + values["td"]["seconds"] * p / (1 + values["td"]["seconds"] / kd * p))
            mkr = kp * (1 + 1 / (ti * p) + td * p / (tf * p + 1))
            self.assertAlmostEqual(abs(manual - mkr), 0.0, places=12)


class DocumentedRules(unittest.TestCase):
    def test_parameters_names_units_and_the_rules_from_the_manual(self):
        result = convert(dict(name="PIDFF", target={}, parameters=dict(kp=-1.5, tiSeconds=12.5, tdSeconds=2.0, filterSeconds=0.5)))
        self.assertTrue(result["converted"])
        values = result["parameters"]
        self.assertEqual((values["kp"], values["rev_dir"], values["mix_par"], values["pv_dev"], values["kd"]), (1.5, True, False, False, 4.0))
        self.assertEqual((values["ti"]["iec"], values["td"]["iec"]), ("T#12500ms", "T#2000ms"))
        conversion = result["conversion"]
        self.assertEqual((conversion["qualification"], conversion["readyToLoad"]), ("non_qualified", False))
        self.assertEqual(conversion["neutral"]["ovs_att"], 0.0)              # 0 = the classical deviation (p. 177)
        self.assertIn("outrate", " ".join(conversion["toDetermine"]))        # its neutral value is not documented: not invented
        self.assertEqual({e["page"] for e in conversion["equations"]}, {173, 182, 183})
        self.assertIn("label_inconsistency_rev_dir", conversion["warnings"])

    def test_what_the_documentation_does_not_settle_is_refused_not_guessed(self):
        cases = {
            "conversion_parallel_undocumented": dict(kp=1, tiSeconds=5, tdSeconds=0, structure="parallel"),
            "conversion_kd_below_one": dict(kp=1, tiSeconds=5, tdSeconds=2, filterSeconds=3),      # Tf > td: the block would substitute 1
            "conversion_filter_required": dict(kp=1, tiSeconds=5, tdSeconds=2),
            "conversion_parameters": dict(kp=0, tiSeconds=5, tdSeconds=0),
            "conversion_time_resolution": dict(kp=1, tiSeconds=0.0004, tdSeconds=0),
        }
        for reason, parameters in cases.items():
            with self.subTest(reason=reason):
                result = convert(dict(name="PIDFF", target={}, parameters=parameters))
                self.assertEqual((result["converted"], result["reason"], result["parameters"]), (False, reason, None))
        for bad in (dict(kp=1, tiSeconds=-1, tdSeconds=0), dict(kp=math.nan, tiSeconds=5, tdSeconds=0), dict(kp=1, tiSeconds=5)):
            self.assertFalse(convert(dict(name="PIDFF", target={}, parameters=bad))["converted"])

    def test_other_blocks_stay_refused_until_their_documentation_is_read(self):
        for name in ("PI_B", "PIFINT"):   # PIFINT : nom inexistant (erreur de nomenclature du client, c'est PID_INT) -> refusé comme bloc inconnu
            result = convert(dict(name=name, target={}, parameters=dict(kp=1, tiSeconds=5, tdSeconds=0)))
            self.assertEqual((result["converted"], result["parameters"]), (False, None), name)

    def test_time_values_that_the_millisecond_resolution_changes_are_flagged(self):
        result = pidff(dict(kp=1, tiSeconds=0.0123456, tdSeconds=0))
        self.assertIn("time_rounded_to_milliseconds", result["warnings"])
        self.assertEqual(result["parameters"]["ti"]["iec"], "T#12ms")


if __name__ == "__main__":
    unittest.main()
