"""Critère d'erreur AUTOMATIQUE pour l'identification ARX (excitation arbitraire, boucle ouverte) : remplace la limite `maximumRmse` saisie par l'utilisateur.

Principe (comme la voie par échelons, `fopdt.suggest_criteria` : limite = max(3 sigma, ...)) : l'erreur de simulation libre d'un modèle ADÉQUAT est de l'ordre du bruit
de mesure ; on accepte jusqu'à 3 sigma. Le bruit est estimé SANS MODÈLE, pour ne pas être contaminé par l'erreur de structure (un sigma tiré des résidus d'un modèle
inadéquat serait gonflé et ferait accepter ce modèle) :
  pseudo-résidus  e_i = (y_{i-1} + y_{i+1}) / 2 - y_i ,  Var(e_i) = 1,5 sigma^2 pour un bruit blanc sur une sortie localement lisse (Gasser, Sroka & Jennen-Steinmetz,
  « Residual variance and residual pattern in nonlinear regression », Biometrika 73 (1986) 625-633) ; estimation ROBUSTE par la MAD (Hampel 1974), de sorte que les
  fronts raides de la réponse ne gonflent pas l'estimation : sigma = 1,4826 MAD(e) / sqrt(1,5).
Limites connues : suppose un bruit blanc et un échantillonnage assez fin devant la dynamique ; un bruit coloré ou quantifié sous-estime ou surestime sigma ; aucune garantie
sur l'aptitude à la régulation (la porte reste fondée sur la prédiction, la blancheur et l'absence de corrélation résidus/entrée).
"""
import numpy as np

VERSION = "1.0.0"
MULTIPLIER = 3.0                 # même multiple que la voie par échelons (3 sigma)
MAD_TO_SIGMA = 1.4826


def noise_sigma(y):
    y = np.asarray(y, dtype=float)
    if len(y) < 20 or not np.all(np.isfinite(y)):
        raise ValueError("arx_criteria_data")
    e = 0.5 * (y[:-2] + y[2:]) - y[1:-1]
    mad = float(np.median(np.abs(e - np.median(e))))
    return MAD_TO_SIGMA * mad / np.sqrt(1.5)


def maximum_rmse(y_training, y_validation):
    """Limite d'erreur proposée : 3 sigma ; sigma poolé des DEUX séries (variance moyenne) : la validation ne sert qu'à estimer le bruit, jamais à estimer le modèle."""
    sigma = float(np.sqrt(0.5 * (noise_sigma(y_training) ** 2 + noise_sigma(y_validation) ** 2)))
    return MULTIPLIER * sigma, sigma
