import sys, math, base64
sys.path.insert(0, "/home/user/mkr/engine")
import numpy as np
from datetime import datetime, timedelta, timezone
import broida, fopdt, sopdt, validation_gate, arx

def src(day, resp, amp=2.0, n=200, pre=10, dt=1.0, sigma=0.0, seed=0):
    rng=np.random.default_rng(seed+day)
    start=datetime(2026,1,day,tzinfo=timezone.utc); L=["t,mv,pv"]
    for i in range(n):
        t=(i-pre)*dt
        L.append(f"{(start+timedelta(seconds=i*dt)).isoformat()},{amp if t>=0 else 0},{50+amp*resp(t)+sigma*rng.standard_normal()!r}")
    return dict(raw=base64.b64encode("\n".join(L).encode()).decode(),delimiter=",",timeColumn="t",mvColumn="mv",pvColumn="pv")
fo=lambda K,tau,th: (lambda t: K*(1-math.exp(-max(t-th,0)/tau)))
print("== Broida bias on exact FOPDT, dt=0.1")
for th in (0.5, 2, 5, 10, 20):
    r=broida.identify(dict(training=src(1,fo(1.5,10,th),n=1500,dt=0.1*1), validation=src(2,fo(1.5,10,th),amp=-1.5,n=1500,dt=0.1),mvTolerance=1e-3,pvTolerance=1e-3)) if False else None
for th in (0.5,2,5,10,20):
    p=dict(training=src(1,fo(1.5,10,th),n=1200,dt=0.1), validation=src(2,fo(1.5,10,th),amp=-1.5,n=1200,dt=0.1),mvTolerance=1e-3,pvTolerance=1e-3)
    try:
        r=broida.identify(p); print(th, round(r["tauSeconds"],4), round(r["delaySeconds"],4), round(r["gain"],5))
    except ValueError as e: print(th,"refused",e)
print("== FOPDT large dead time theta/tau=5, noisy")
for th in (30, 50, 70):
    p=dict(training=src(1,fo(1.5,10,th),n=200,sigma=0.01), validation=src(2,fo(1.5,10,th),amp=-1.5,n=200,sigma=0.01),mvTolerance=1e-3,pvTolerance=0.06)
    try:
        r=fopdt.identify(p); print(th, round(r["tauSeconds"],3), round(r["delaySeconds"],3), r["warnings"])
    except ValueError as e: print(th,"refused",e)
print("== underdamped 2nd order (zeta=0.3, wn=0.2) fed to FOPDT/SOPDT via gate")
z,wn=0.3,0.2
def osc(t,th=3):
    x=t-th
    if x<=0: return 0.0
    wd=wn*math.sqrt(1-z*z)
    return 1.2*(1-math.exp(-z*wn*x)*(math.cos(wd*x)+z/math.sqrt(1-z*z)*math.sin(wd*x)))
p=dict(training=src(1,osc,n=250,sigma=0.01), validation=src(2,osc,amp=-1.5,n=250,sigma=0.01),mvTolerance=1e-3,pvTolerance=0.06)
c=validation_gate.suggest(p)
for fam in ("FOPDT","SOPDT","BroidaFOPDT"):
    try:
        r=validation_gate.assess(dict(family=fam,trials=p,maximumRmse=c["maximumRmse"],maximumResidualLagOne=c["maximumResidualLagOne"]))
        print(fam, r["assessment"])
    except ValueError as e: print(fam,"refused",e)
print("== non-steady initial condition (PV still relaxing before step) -> FOPDT")
def relax(t): return (0.5*math.exp(-(t+10)/10)/2.0) + fo(1.5,10,3)(t)
p=dict(training=src(1,relax,n=200,sigma=0.005), validation=src(2,fo(1.5,10,3),amp=-1.5,n=200,sigma=0.005),mvTolerance=1e-3,pvTolerance=0.2)
try:
    r=fopdt.identify(p); print(round(r["gain"],3), round(r["tauSeconds"],3), round(r["delaySeconds"],3), r["warnings"])
except ValueError as e: print("refused",e)
