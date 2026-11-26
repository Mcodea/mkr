import sys, math, base64
sys.path.insert(0, "/home/user/mkr/engine")
import numpy as np
from datetime import datetime, timedelta, timezone
import arx
def make(day,u,y,ts):
    start=datetime(2026,6,day,tzinfo=timezone.utc)
    L=["timestamp,mv,pv"]+[f"{(start+timedelta(seconds=i*ts)).isoformat()},{float(u[i])!r},{float(y[i])!r}" for i in range(len(u))]
    return dict(raw=base64.b64encode("\n".join(L).encode()).decode(),delimiter=",",timeColumn="timestamp",mvColumn="mv",pvColumn="pv")
def prbs(n,rng,hold=5): return np.repeat(rng.choice([-1.,1.],size=n//hold+1),hold)[:n]
def fo(u,tau,K,d,ts,noise,rng):
    p=math.exp(-ts/tau); y=np.zeros(len(u))
    for k in range(1,len(u)): y[k]=p*y[k-1]+K*(1-p)*(u[k-1-d] if k-1-d>=0 else 0)
    return y+noise*rng.standard_normal(len(u))
P=lambda t,v,lim=0.2: dict(training=t,validation=v,mvTolerance=1e-6,maximumRmse=lim,loop="open")
rng=np.random.default_rng(1)
# 1 large delay 80 samples
u,v=prbs(800,rng),prbs(800,rng)
r=arx.identify(P(make(1,u,fo(u,20,1.5,80,2,0.02,rng),2),make(2,v,fo(v,20,1.5,80,2,0.02,rng),2)))
print("delay80:",r["decision"],r.get("reasons"))
# 2 integrator
def integ(u,K,d,noise,rng):
    y=np.cumsum(np.concatenate(([0],K*u[:-1]))); y=np.roll(y,d); y[:d]=0; return y+noise*rng.standard_normal(len(u))
r=arx.identify(P(make(1,u,integ(u,0.05,3,0.02,rng),2),make(2,v,integ(v,0.05,3,0.02,rng),2)))
print("integrator:",r["decision"],r.get("reasons"))
# 3 closed loop data declared open: PI on FOPDT, u = PI(sp - y_meas), sp PRBS
def cl(n,seed):
    rg=np.random.default_rng(seed); sp=prbs(n,rg,hold=60); a=math.exp(-2/20); y=np.zeros(n); u=np.zeros(n); m=np.zeros(n); I=0; load=0
    for i in range(n):
        load=0.95*load+rg.normal(0,0.05)
        m[i]=y[i]+rg.normal(0,0.05)
        e=sp[i]-m[i]; I+=e*2/20; u[i]=0.6*(e+I)
        if i+1<n: y[i+1]=a*y[i]+(1-a)*1.5*(u[i-2] if i>=2 else 0)+(1-a)*load
    return u,m
ut,yt=cl(800,3); uv,yv=cl(800,4)
r=arx.identify(P(make(1,ut,yt,2),make(2,uv,yv,2),lim=0.5))
print("closed-loop declared open:",r["decision"],r.get("reasons"), r["selected"]["firstOrder"] if r["selected"] and r["selected"].get("firstOrder") else (r["selected"] and (r["selected"]["na"],r["selected"]["estimator"])))
print("---details")
rng=np.random.default_rng(1)
u,v=prbs(800,rng),prbs(800,rng)
_=fo(u,20,1.5,80,2,0.02,rng); _=fo(v,20,1.5,80,2,0.02,rng)
r=arx.identify(P(make(1,u,integ(u,0.05,3,0.02,rng),2),make(2,v,integ(v,0.05,3,0.02,rng),2)))
s=r["selected"]; print("integ sel", s["na"],s["nb"],s["nk"],s["estimator"],s["poles"],s.get("firstOrder"))
r=arx.identify(P(make(1,ut,yt,2),make(2,uv,yv,2),lim=0.5))
s=r["selected"]; print("cl sel", s["na"],s["nb"],s["nk"],s["estimator"],s["poles"],s["a"],s["b"],s["inputCorrelation"],s["validationSimulationRmse"])
# true: a1=-exp(-0.1)=-0.905, b=1.5*(1-0.905)=0.1427 at nk=3
c11=[c for c in r["candidates"] if c["na"]==1]
for c in c11: print(c["nk"],c["estimator"],c["a"],c["b"],c["reasons"])
