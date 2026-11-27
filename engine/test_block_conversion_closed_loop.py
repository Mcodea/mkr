"""Conversion MKR -> bloc Schneider, vérifiée de bout en bout : adaptateur -> modèle exécutable documenté -> boucle fermée.

Ce que ce test peut démontrer : que SIGNES, ÉCHELLES et UNITÉS de la conversion (block_adapters.pidff / pid_int) donnent, une fois lus
par le comportement documenté du bloc, la boucle voulue par MKR. Il ne démontre PAS que le bloc réel se comporte ainsi (modèle
documentaire, non qualifié : voir docs/science/schneider/).

Oracle indépendant : PI idéal continu u = Kp(e + (1/Ti) intégrale e), e = SP - PV, sur un procédé du premier ordre y' = (K u - y)/T,
intégré par RK4 avec un pas 20 fois plus fin que la période du bloc. Il n'utilise aucun module du dépôt.
Critères fixés a priori : écart de trajectoire <= 3 % de l'échelon pour l'échantillonnage (dt/Ti = 0,005 ici) ; pour PID_INT, erreur finale
bornée par la zone morte de la troncature entière, 1000 TI/(KP TS) comptes (interprétation I3 du modèle) + 5 comptes."""
import unittest

from block_adapters import pid_int, pidff
from block_pid_int import PidIntModel
from block_pidff import PidffModel


def ideal_loop(kp, ti, gain, tau, sp, y0, u0, t_end, dt_out, substeps=20):
    """PI idéal continu, RK4. Retourne y aux instants k*dt_out."""
    h = dt_out / substeps
    y, integral = y0, 0.0
    ys = []

    def deriv(y_, i_):
        e = sp - y_
        u = u0 + kp * (e + i_ / ti)
        return (gain * (u - u0) - (y_ - y0)) / tau, e       # procédé en écart autour de (u0, y0)

    for _ in range(int(round(t_end / dt_out))):
        for _ in range(substeps):
            k1 = deriv(y, integral)
            k2 = deriv(y + h / 2 * k1[0], integral + h / 2 * k1[1])
            k3 = deriv(y + h / 2 * k2[0], integral + h / 2 * k2[1])
            k4 = deriv(y + h * k3[0], integral + h * k3[1])
            y += h / 6 * (k1[0] + 2 * k2[0] + 2 * k3[0] + k4[0])
            integral += h / 6 * (k1[1] + 2 * k2[1] + 2 * k3[1] + k4[1])
        ys.append(y)
    return ys


def plant_step(y, u, u0, y0, gain, tau, dt):
    """Procédé du premier ordre, solution exacte pour u constant sur dt (ZOH)."""
    import math
    target = y0 + gain * (u - u0)
    return target + (y - target) * math.exp(-dt / tau)


class PidffClosedLoop(unittest.TestCase):
    def run_case(self, kp, ti, gain, tau):
        dt, t_end, sp, y0, u0, span = 0.1, 40.0, 60.0, 50.0, 30.0, 1000.0
        conversion = pidff(dict(kp=kp, tiSeconds=ti, tdSeconds=0.0))
        p = conversion["parameters"]
        para = dict(pv_inf=-span, pv_sup=span, out_inf=-span, out_sup=span, rev_dir=p["rev_dir"], mix_par=p["mix_par"], aw_type=False,
                    en_rcpy=False, kp=p["kp"], ti=p["ti"]["seconds"], td=0.0, kd=1.0, pv_dev=p["pv_dev"], bump=False, dband=0.0,
                    gain_kp=0.0, ovs_att=0.0, outbias=0.0, out_min=-span, out_max=span, outrate=0.0, ff_inf=0.0, ff_sup=0.0,
                    otff_inf=0.0, otff_sup=0.0)
        block = PidffModel(para, out_init=u0)
        block.step(y0, y0, dt)                              # cycle d'initialisation : écart nul (I2)
        y, ys = y0, []
        for _ in range(int(t_end / dt)):
            out = block.step(y, sp, dt).out
            y = plant_step(y, out, u0, y0, gain, tau, dt)
            ys.append(y)
        return ys, ideal_loop(kp, ti, gain, tau, sp, y0, u0, t_end, dt), sp - y0

    def test_positive_gain_plant(self):
        ys, ref, step = self.run_case(kp=0.8, ti=10.0, gain=1.5, tau=8.0)
        # le bloc agit sur un retard d'un cycle (la sortie calculée sur PV(k) agit sur [k, k+1]) : même décalage que le RK4 ci-dessus
        self.assertLessEqual(max(abs(a - b) for a, b in zip(ys, ref)), 0.03 * step)

    def test_negative_gain_plant_uses_rev_dir(self):
        ys, ref, step = self.run_case(kp=-0.8, ti=10.0, gain=-1.5, tau=8.0)
        self.assertLessEqual(max(abs(a - b) for a, b in zip(ys, ref)), 0.03 * step)
        self.assertTrue(pidff(dict(kp=-0.8, tiSeconds=10.0, tdSeconds=0.0))["parameters"]["rev_dir"])


class PidIntClosedLoop(unittest.TestCase):
    def test_counts_loop_matches_ideal_pi_and_integer_dead_zone_bound(self):
        kp, ti, ts_s, gain, tau = 3.0, 10.0, 0.1, 1.0, 8.0
        conversion = pid_int(dict(kp=kp, tiSeconds=ti, tdSeconds=0.0, tsSeconds=ts_s, pvSpan=10000.0, mvSpan=10000.0))
        regs = conversion["parameters"]
        self.assertEqual((regs["KP"], regs["TI"], regs["TS"]), (300, 100, 10))
        y0, u0, sp = 4000, 3000, 5000
        model = PidIntModel(100)
        model.para[0:9] = [y0, u0, regs["KP"], regs["TI"], 0, regs["TS"], 10000, 0, 1 << 4]
        model.call(y0)
        model.para[1] = u0
        model.call(y0)                                       # échantillon manuel : OUT = OUT_MAN
        model.auto = True
        model.para[0] = sp                                   # échelon de consigne au premier échantillon automatique (Δε non nul)
        y, ys = float(y0), []
        for _ in range(400):                                 # 40 s à 100 ms
            out = model.call(round(y))
            y = plant_step(y, out, u0, y0, gain, tau, ts_s)
            ys.append(y)
        ref = ideal_loop(kp, ti, gain, tau, sp, y0, u0, 40.0, ts_s)
        step = sp - y0
        self.assertLessEqual(max(abs(a - b) for a, b in zip(ys, ref)), 0.03 * step + 5)   # + quelques comptes de troncature entière
        dead_zone = 1000 * regs["TI"] / (regs["KP"] * regs["TS"])
        self.assertLessEqual(abs(sp - ys[-1]), dead_zone + 5)
        self.assertGreater(dead_zone, 30)                    # la quantification entière de PID_INT est un fait à AFFICHER à l'utilisateur


if __name__ == "__main__":
    unittest.main()
