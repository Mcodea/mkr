"""Per-cycle PIDFF controller written from the manufacturer's own equations. TEST ORACLE ONLY.

Source: Schneider Electric, EcoStruxure Control Expert - Régulation - Bibliothèque de blocs, 33002536 (09/2020, .16),
chapter PIDFF: transfer function p. 173, "Equations détaillées : régulateur PID à algorithme incrémentiel" p. 183 (aw_type = 0),
weighting of the setpoint p. 177, direction of action p. 182, parameter table p. 171-172.

Scope kept deliberately narrow: ti > 0 (incremental algorithm), aw_type = 0, en_rcpy = 0, mixed structure (mix_par = 0),
dead band and feed forward inactive, no output limits. Limits, anti-windup and tracking are NOT reproduced: this model
exists to check the transfer-function equivalence of a conversion, never to predict the behaviour of a real block.

Interpretation made explicit (the manual uses the symbol TermD for two things): in the incremental algorithm the bracket
of TermD is the absolute filtered derivative component X(k), and TermD(k) = X(k) - X(k-1). This is the only reading
consistent with the transfer function of p. 173 (it is also checked numerically by the tests).
"""


class PidffReference:
    def __init__(self, kp, ti, td, kd, reverse_direction_input, dt, pv_dev=0, ovs_att=0.0):
        if not (kp >= 0 and ti > 0 and td >= 0 and dt > 0 and 0 <= ovs_att <= 1 and (td == 0 or kd >= 1)):
            raise ValueError("pidff_reference_domain")
        # sense: +1 when rev_dir = 1 (a positive deviation PV - SP raises the output), -1 when rev_dir = 0 (p. 182).
        self.sense = 1.0 if reverse_direction_input else -1.0
        self.kp, self.ti, self.td, self.kd, self.dt, self.pv_dev, self.ovs_att = kp, ti, td, kd, dt, pv_dev, ovs_att
        self.out = 0.0
        self._first = True
        self._weighted = self._var = self._derivative = 0.0

    def prime(self, pv, sp):
        """Previous-cycle values of a loop at rest: the next call sees a genuine change of setpoint or measurement."""
        self._weighted, self._var, self._first = pv - (1 - self.ovs_att) * sp, pv if self.pv_dev == 0 else pv - sp, False

    def step(self, pv, sp):
        """One execution of the block. Returns OUT (absolute value)."""
        dev = pv - sp                                            # Info_PIDFF.dev = PV - SP (p. 172)
        weighted = pv - (1 - self.ovs_att) * sp                  # DEV_WGH (p. 177)
        var = pv if self.pv_dev == 0 else dev                    # VAR (p. 182)
        if self._first:
            self._weighted, self._var, self._first = weighted, var, False
        term_p = self.sense * self.kp * (weighted - self._weighted)
        term_i = self.sense * self.kp * self.dt / self.ti * dev
        # The derivative component is kept in its absolute, unsigned form so that the sign is applied once.
        if self.td > 0:
            unsigned = (self.td * self._derivative + self.kp * self.td * self.kd * (var - self._var)) / (self.kd * self.dt + self.td)
        else:
            unsigned = 0.0
        term_d = self.sense * (unsigned - self._derivative)
        self._derivative = unsigned
        self._weighted, self._var = weighted, var
        self.out += term_p + term_i + term_d                     # OUT(new) = OUT(old) + OUTD(new), no limiter
        return self.out
