import sys; sys.path.insert(0,"/home/user/mkr/engine")
import numpy as np
from test_process_diversity import source, first_order, second_order
import fopdt, sopdt
from model_drift import numerical_errors
from fopdt import read_trials, basis
for fam,mod,resp,truth in (("FOPDT",fopdt,first_order(1.5,20,4),dict(gain=1.5,tauSeconds=20.0)),):
    est={k:[] for k in truth}; se={k:[] for k in truth}; seb=[]
    for seed in range(300,360):
        rng=np.random.default_rng(seed)
        tr=dict(training=source(1,resp,2.0,1.0,0.03,rng,post=120),validation=source(2,resp,-1.5,1.0,0.03,rng,post=120),mvTolerance=0.01,pvTolerance=0.18)
        r=mod.identify(tr)
        if r["standardErrors"] is None: continue
        for k in truth: est[k].append(r[k]); se[k].append(r["standardErrors"][k])
        t,_=read_trials(tr)
        e=numerical_errors(t,("gain","tauSeconds","delaySeconds"),dict(gain=r["gain"],tauSeconds=r["tauSeconds"],delaySeconds=r["delaySeconds"]),lambda v: basis(t.times,v[1],v[2]))
        seb.append(e["gain"] if e else np.nan)
    for k in truth:
        print(fam,k,"n=",len(est[k]),"published mean SE",np.mean(se[k]),"empirical SD",np.std(est[k],ddof=1),"ratio",np.mean(se[k])/np.std(est[k],ddof=1))
    print("gain SE with baseline term (model_drift.numerical_errors):",np.nanmean(seb),"ratio",np.nanmean(seb)/np.std(est["gain"],ddof=1))
