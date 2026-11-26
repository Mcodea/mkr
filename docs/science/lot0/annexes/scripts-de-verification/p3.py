import sys; sys.path.insert(0,"/home/user/mkr/engine")
from recommend import calculate
r=calculate(dict(family="FOPDT", gain=20306.386, tauSeconds=809549.3, delaySeconds=8.0, secondTauSeconds=None, sampleSeconds=2.0,
 standardErrors=dict(gain=55636.0,tauSeconds=2218241.8,delaySeconds=1.0), objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=True))
print(r["decision"], r.get("reasons"))
