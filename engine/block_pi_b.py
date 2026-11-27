"""Modèle exécutable du bloc Schneider PI_B (EcoStruxure Control Expert, bibliothèque de régulation « Controller »).

Source : 33002535.16 (EN, 09/2020), chapitre 16 « PI_B: Simple PI controller », p. 151-162. Les équations sont des images du PDF ;
elles ont été LUES SUR LES PAGES RENDUES (p. 156, 158, 160, 161) le 2026-11-27, pas déduites d'un résumé. Disponibilité : EFB de la famille
« Controller » sur M340, M580, Quantum, Momentum, Premium (p. 28) ; cela ne dit RIEN d'une CPU ni d'un micrologiciel précis.

Ce que le modèle implémente (page du manuel) :
  * fonction de transfert OUT = kp (1 + 1/(ti p)) IN (p. 156) ;
  * DEV = PV - SP, SP borné à [pv_inf, pv_sup] (p. 159) ; zone morte : tant que |DEV| < dband le calcul utilise 0, au-delà il utilise DEV lui-même
    (courbe « Modified Variance » de la p. 158, qui présente un saut à dband et non un décalage) ;
  * sens d'action (p. 160) : sense = +1 si rev_dir = 1 (« opposite action » : un écart PV - SP positif AUGMENTE la sortie), sense = -1 si rev_dir = 0 ;
  * algorithme absolu, ti = 0 (p. 160) : TermP = sense kp DEV ; OUT = TermP + outbias ; OUTD = OUT(new) - OUT(old) ; OUT = limiter(OUT) ;
  * algorithme incrémental, ti > 0 (p. 161) : OUTD = TermP + TermI ; TermP = sense kp Delta(DEV) ; TermI = sense kp (dt/ti) DEV si kp > 0, sinon (régime
    intégral pur kp = 0) TermI = sense (out_sup - out_inf)/(pv_sup - pv_inf) (dt/ti) DEV ; OUT = OUT(old) + OUTD (en_rcpy = 0) ou RCPY + OUTD (en_rcpy = 1) ;
  * OUT borné à [out_inf, out_sup] dans tous les modes, OUTD NON borné (p. 159) ; avec en_rcpy = 1, OUT n'est pas borné (note p. 158) ;
  * modes automatique / manuel / suivi (p. 159) ; changement manuel -> automatique sans à-coup si ti > 0, avec à-coup si ti = 0 ;
  * mot d'état (p. 162) : bit 4 (SP hors plage, kp ou dband négatif remplacé par 0, outbias hors de [out_inf - out_sup, out_sup - out_inf] ramené à la borne),
    bits 5 et 6 (OUT atteint la borne basse / haute), bit 7 (pv_inf = pv_sup : erreur, OUT, OUTD et DEV restent inchangés) ;
  * entrée non réelle ou erreur de calcul flottant : erreur, sorties inchangées (p. 162).

INTERPRÉTATIONS (le manuel est ambigu ou muet ; ce ne sont PAS des faits documentés — à lever sur banc ou par la documentation en ligne) :
  I1. Ordre du limiteur en incrémental (p. 161 liste « OUT = limiter(OUT) » avant « OUT = OUT(old) + OUTD ») : retenu OUT(new) = limiter(OUT(old) + OUTD),
      seule lecture cohérente avec « OUT est borné » et « OUTD ne tient pas compte du bornage » (p. 159) ; elle donne l'effet anti-emballement attendu.
  I2. Delta(DEV) au premier cycle et après un passage manuel/suivi -> automatique : retenu Delta(DEV) = 0 (écart précédent := écart courant), cohérent avec
      « sans à-coup si ti > 0 » (p. 159). Le manuel ne définit pas l'initialisation.
  I3. Delta(DEV) est calculé sur l'écart après zone morte (celui que « le calcul utilise »).
  I4. En mode suivi, OUT = limiter(TR_I) ; en manuel, OUT = limiter(valeur utilisateur) ; OUTD = OUT(new) - OUT(old) dans ces deux modes. Non documenté.
  I5. kp est en unité de OUT par unité de PV (analyse dimensionnelle de la fonction de transfert) : le manuel n'écrit pas l'unité de kp.
  I6. La résolution du type TIME de ti et la période réelle d'exécution (tâche) ne sont pas modélisées : dt est donné par l'appelant.
Les sorties MA_O n'est pas modélisée (sémantique non documentée dans le chapitre lu).
"""
from dataclasses import dataclass
import math

BIT4, BIT5, BIT6, BIT7 = 1 << 4, 1 << 5, 1 << 6, 1 << 7


@dataclass(frozen=True)
class PiBResult:
    out: float
    outd: float
    dev: float
    status: int
    error: str | None = None


class PiBModel:
    """Un bloc PI_B : l'état (OUT précédent, écart précédent) vit dans l'instance, comme dans l'automate."""

    def __init__(self, pv_inf, pv_sup, out_inf, out_sup, rev_dir, kp, ti, dband=0.0, outbias=0.0, en_rcpy=False, out_init=0.0):
        values = (pv_inf, pv_sup, out_inf, out_sup, kp, ti, dband, outbias, out_init)
        if not all(isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v) for v in values):
            raise ValueError("pi_b_parameters")
        if not out_inf < out_sup or ti < 0:
            raise ValueError("pi_b_parameters")
        self.pv_inf, self.pv_sup, self.out_inf, self.out_sup = float(pv_inf), float(pv_sup), float(out_inf), float(out_sup)
        self.sense = 1.0 if rev_dir else -1.0                 # p. 160
        self.kp, self.ti, self.dband, self.outbias = float(kp), float(ti), float(dband), float(outbias)
        self.en_rcpy = bool(en_rcpy)
        self.out = min(max(float(out_init), self.out_inf), self.out_sup)
        self.outd = 0.0
        self.dev = 0.0
        self._previous_dev = None                              # None : Delta(DEV) = 0 au prochain cycle automatique (I2)
        self.status = 0

    def _limit(self, value):
        return min(max(value, self.out_inf), self.out_sup)

    def execute(self, pv, sp, *, auto=True, tracking=False, tr_i=0.0, rcpy=0.0, out_manual=None, dt=1.0):
        """Un cycle. Erreur => OUT, OUTD, DEV inchangés (p. 162)."""
        unchanged = lambda error, status=0: PiBResult(self.out, self.outd, self.dev, status, error)
        if not all(isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v) for v in (pv, sp, tr_i, rcpy, dt)) or dt <= 0:
            return unchanged("float_input")
        if out_manual is not None and not math.isfinite(out_manual):
            return unchanged("float_input")
        if self.pv_inf == self.pv_sup:
            self.status = BIT7
            return unchanged("pv_range_identical", BIT7)
        status = 0
        low, high = min(self.pv_inf, self.pv_sup), max(self.pv_inf, self.pv_sup)
        setpoint = sp
        if not low <= sp <= high:                              # p. 159, 162 : bit 4
            setpoint = min(max(sp, low), high)
            status |= BIT4
        kp, dband, outbias = self.kp, self.dband, self.outbias
        if kp < 0:                                             # p. 162 : remplacé par 0
            kp = 0.0
            status |= BIT4
        if dband < 0:
            dband = 0.0
            status |= BIT4
        bound = self.out_sup - self.out_inf
        if not -bound <= outbias <= bound:
            outbias = min(max(outbias, -bound), bound)
            status |= BIT4
        dev = pv - setpoint
        effective = dev if abs(dev) >= dband else 0.0          # p. 158
        previous_out = self.out
        if tracking or not auto:
            new_out = self._limit(tr_i if tracking else (self.out if out_manual is None else out_manual))   # I4
            self._previous_dev = None
            outd = new_out - previous_out
        elif self.ti == 0:                                     # p. 160
            term_p = self.sense * kp * effective
            new_out = self._limit(term_p + outbias)
            outd = (term_p + outbias) - previous_out
            self._previous_dev = effective
        else:                                                  # p. 161
            delta = 0.0 if self._previous_dev is None else effective - self._previous_dev      # I2, I3
            term_p = self.sense * kp * delta
            if kp > 0:
                term_i = self.sense * kp * (dt / self.ti) * effective
            else:
                term_i = self.sense * bound / (self.pv_sup - self.pv_inf) * (dt / self.ti) * effective
            outd = term_p + term_i
            new_out = rcpy + outd if self.en_rcpy else self._limit(previous_out + outd)         # I1 ; note p. 158 : pas de bornage avec en_rcpy
            self._previous_dev = effective
        if not (self.en_rcpy and auto and not tracking and self.ti != 0):     # OUT non borné avec en_rcpy : pas de bit de butée
            if new_out <= self.out_inf:
                status |= BIT5
            if new_out >= self.out_sup:
                status |= BIT6
        self.out, self.outd, self.dev, self.status = new_out, outd, dev, status
        return PiBResult(new_out, outd, dev, status)
