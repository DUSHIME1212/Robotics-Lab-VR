"""Extract physical data from SimReady USDs: link masses, joint count/types, limits and velocity limits.
blender -b --factory-startup --python extract_usd_data.py -- <list.tsv> <out.json>   (tsv: name<TAB>usd path)"""
import sys, json
from pxr import Usd, UsdPhysics
a = sys.argv[sys.argv.index("--") + 1:]
out = {}
for line in open(a[0], encoding="utf-8"):
    if not line.strip(): continue
    name, path = line.rstrip("\n").split("\t")[:2]
    try:
        st = Usd.Stage.Open(path)
    except Exception as e:
        print("[usd] fail", name, e); continue
    mass, links, joints = 0.0, 0, []
    for p in st.Traverse():
        if p.HasAPI(UsdPhysics.MassAPI):
            m = UsdPhysics.MassAPI(p).GetMassAttr().Get()
            if m and m > 0: mass += m; links += 1
        if p.IsA(UsdPhysics.RevoluteJoint) or p.IsA(UsdPhysics.PrismaticJoint):
            v = p.GetAttribute("physxJoint:maxJointVelocity").Get() if p.GetAttribute("physxJoint:maxJointVelocity") else None
            lo, hi = p.GetAttribute("physics:lowerLimit").Get(), p.GetAttribute("physics:upperLimit").Get()
            joints.append({"name": p.GetName(), "type": "revolute" if p.IsA(UsdPhysics.RevoluteJoint) else "prismatic",
                           "lower": lo, "upper": hi, "maxVelocity": v})
    out[name] = {"usdMassKg": round(mass, 2), "massBodies": links, "jointCount": len(joints),
                 "revolute": sum(j["type"] == "revolute" for j in joints), "prismatic": sum(j["type"] == "prismatic" for j in joints),
                 "joints": joints}
    print("[usd]", name, round(mass, 1), "kg", len(joints), "joints")
json.dump(out, open(a[1], "w"), indent=1)
