import sys
from pxr import Usd, UsdPhysics, UsdGeom
path = sys.argv[sys.argv.index("--")+1]
st = Usd.Stage.Open(path)
print("upAxis", UsdGeom.GetStageUpAxis(st), "mpu", UsdGeom.GetStageMetersPerUnit(st))
for p in st.Traverse():
    if p.IsA(UsdPhysics.RevoluteJoint) or p.IsA(UsdPhysics.PrismaticJoint):
        j = UsdPhysics.Joint(p)
        ax = p.GetAttribute("physics:axis").Get()
        lo = p.GetAttribute("physics:lowerLimit").Get(); hi = p.GetAttribute("physics:upperLimit").Get()
        print(p.GetTypeName(), p.GetPath(), j.GetBody0Rel().GetTargets(), j.GetBody1Rel().GetTargets(), ax, lo, hi, j.GetLocalPos0Attr().Get())
