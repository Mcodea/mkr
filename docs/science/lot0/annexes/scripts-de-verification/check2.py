import sys, math
sys.path.insert(0, "/home/user/mkr/engine")
import numpy as np
from scipy.optimize import brentq
from pi_tuning_integrator import loop_margins
from fopdt_simulation import plant_step
from scipy import signal

def exact(slope, delay, kp, ti):
    k = abs(kp*slope)
    L = lambda w: k*(1+1/(1j*ti*w))/(1j*w)*np.exp(-1j*w*delay)
    wc = brentq(lambda w: abs(L(w))-1, 1e-8, 1e4)
    ph = lambda w: -math.pi/2 - math.atan(1/(ti*w)) - w*delay
    pm = math.degrees(ph(wc)+math.pi)
    w180 = brentq(lambda w: ph(w)+math.pi, 1e-8, 1e4)
    gm = 1/abs(L(w180))
    from scipy.optimize import minimize_scalar
    ws = np.logspace(-6,4,400000); ms = float(np.max(1/np.abs(1+L(ws))))
    return gm, pm, ms
for args in [(0.05,3.0,1/(0.05*9),36.0),(0.02,5.0,1/(0.02*10),40.0),(1.0,0.01,1/(1*1.01),4.04),(0.5,0.0,1/(0.5*2),8.0)]:
    try:
        print(args, "code", [round(x,5) if x else x for x in loop_margins(*args)], "exact", [round(x,5) for x in exact(*args)] if args[1]>0 else "delay0")
    except Exception as e:
        print(args, "err", e)

# plant_step accuracy with fractional delay vs scipy dlsim exact on a fine grid
gain,tau,delay,h = 2.0,5.0,1.37,0.2
adv = plant_step(gain,tau,delay,h,100)
u=[]; pv=0.0; out=[]
for k in range(200):
    u.append(1.0 if k>=10 else 0.0); out.append(pv); pv=adv(pv,u)
t=np.arange(200)*h
exactpv = np.where(t>2.0+delay, gain*(1-np.exp(-(t-2.0-delay)/tau)),0)
print("plant_step max err", float(np.max(np.abs(np.array(out)-exactpv))))
