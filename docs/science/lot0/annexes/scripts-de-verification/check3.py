import sys, math
sys.path.insert(0, "/home/user/mkr/engine")
import numpy as np
from recommend import calculate
# Discrete Ms of the sampled loop (ZOH plant, forward-Euler PI as in pi_simulation) vs announced continuous Ms with h/2 delay
def discrete_ms(K,tau,delay,h,kp,ti):
    m = int(delay//h); f = delay - m*h   # delay = m h + f
    p = math.exp(-h/tau)
    # ZOH with fractional delay: y[k+1] = p y[k] + K(b1 u[k-m] + b2 u[k-m-1]); b1 = 1-exp(-(h-f)/tau), b2 = exp(-(h-f)/tau)-p
    b1 = 1-math.exp(-(h-f)/tau); b2 = math.exp(-(h-f)/tau)-p
    w = np.linspace(1e-5, math.pi/h, 200000); z = np.exp(1j*w*h)
    G = K*(b1 + b2/z)/(z-p) * z**(-m)
    C = kp*(1 + (h/ti)/(z-1))
    return float(np.max(1/np.abs(1+C*G)))
for (K,tau,d,h) in [(1,10,1,0.1),(1,10,1,1.0),(1,10,1,2.0),(1,10,0.5,2.0),(1,10,0,1.0),(1,10,0,3.0)]:
    r = calculate(dict(family="FOPDT", gain=K, tauSeconds=tau, delaySeconds=d, secondTauSeconds=None, sampleSeconds=h, standardErrors=None, objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
    c = r["recommendation"]["controller"]
    print((K,tau,d,h), "announced", round(r["recommendation"]["worstPeakSensitivity"],3), "discrete", round(discrete_ms(K,tau,d,h,c["kp"],c["tiSeconds"]),3))
print("--- backward-Euler integral variant")
def discrete_ms_be(K,tau,delay,h,kp,ti):
    p = math.exp(-h/tau); w = np.linspace(1e-5, math.pi/h, 200000); z = np.exp(1j*w*h)
    m = int(delay//h); f = delay-m*h; b1 = 1-math.exp(-(h-f)/tau); b2 = math.exp(-(h-f)/tau)-p
    G = K*(b1 + b2/z)/(z-p) * z**(-m); C = kp*(1 + (h/ti)*z/(z-1))
    return float(np.max(1/np.abs(1+C*G)))
for (K,tau,d,h) in [(1,10,0,1.0),(1,10,0,3.0)]:
    r = calculate(dict(family="FOPDT", gain=K, tauSeconds=tau, delaySeconds=d, secondTauSeconds=None, sampleSeconds=h, standardErrors=None, objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
    c = r["recommendation"]["controller"]
    print((K,tau,d,h), "tauc", round(r["recommendation"]["responseSeconds"],3), "kp", round(c["kp"],3), "ti", round(c["tiSeconds"],3), "BE discrete", round(discrete_ms_be(K,tau,d,h,c["kp"],c["tiSeconds"]),3))
