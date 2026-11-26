import sys, math, itertools
sys.path.insert(0, "/home/user/mkr/engine")
import numpy as np
from recommend import calculate, COVERAGE
from stability_margins import analyze

def ms(k, taus, d, kp, ti, td=0.0, tf=0.0):
    w = np.logspace(-5, 3, 200000); s = 1j*w
    p = k*np.exp(-s*d)
    for t in taus: p = p/(t*s+1)
    c = kp*(1+1/(ti*s)+(td*s/(tf*s+1) if td>0 else 0))
    return float(np.max(1/np.abs(1+c*p)))

# 1) corner assumption: dense interior grid of the box vs corners
for (g,t,d,e) in [(2.0,40.0,10.0,dict(gain=0.3,tauSeconds=10.0,delaySeconds=3.0)),(1.0,5.0,4.0,dict(gain=0.15,tauSeconds=1.5,delaySeconds=0.8)),(1.0,100.0,2.0,dict(gain=0.2,tauSeconds=30.0,delaySeconds=0.5))]:
    r = calculate(dict(family="FOPDT", gain=g, tauSeconds=t, delaySeconds=d, secondTauSeconds=None, sampleSeconds=1.0, standardErrors=e, objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
    c = r["recommendation"]["controller"]
    hg,ht,hd = [COVERAGE*e[k] for k in ("gain","tauSeconds","delaySeconds")]
    worst = (0,None)
    for a,b,cc in itertools.product(np.linspace(-1,1,7),repeat=3):
        v = ms(g+a*hg,[t+b*ht],max(d+cc*hd,0)+0.5,c["kp"],c["tiSeconds"])
        if v>worst[0]: worst=(v,(a,b,cc))
    print("box", g,t,d, "announced", round(r["recommendation"]["worstPeakSensitivity"],4), "dense worst", round(worst[0],4), worst[1])

# 2) half rule: Ms on true SOPDT vs announced (reduced model)
for (k,t1,t2,d) in [(1.0,10.0,4.0,5.0),(1.0,10.0,5.0,5.0),(1.0,20.0,8.0,10.0),(1.0,5.0,4.9,5.0)]:
    r = calculate(dict(family="SOPDT", gain=k, tauSeconds=t1, delaySeconds=d, secondTauSeconds=t2, sampleSeconds=0.2, standardErrors=None, objective="robust", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
    c = r["recommendation"]["controller"]
    true = ms(k,[t1,t2],d+0.1,c["kp"],c["tiSeconds"],c["tdSeconds"],c["filterSeconds"])
    print("halfrule", (k,t1,t2,d), r["structure"], "announced", round(r["recommendation"]["worstPeakSensitivity"],4), "true SOPDT", round(true,4))

# 3) SOPDT PID: announced uses filtered derivative ; ok. Check second pole uncertainty impact: +-30% on tau2
r = calculate(dict(family="SOPDT", gain=1.0, tauSeconds=20.0, delaySeconds=2.0, secondTauSeconds=6.0, sampleSeconds=0.2, standardErrors=dict(gain=0.02,tauSeconds=0.5,delaySeconds=0.1), objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
c = r["recommendation"]["controller"]
for f in (0.7,1.0,1.3):
    print("tau2 x",f, round(ms(1.0,[20.0,6.0*f],2.1,c["kp"],c["tiSeconds"],c["tdSeconds"],c["filterSeconds"]),4), "announced", round(r["recommendation"]["worstPeakSensitivity"],4))
