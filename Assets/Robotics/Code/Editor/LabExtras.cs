using System.IO;
using RoboticsLab.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// Scene features built with the requested packages:
    ///  • Timeline          — "Demonstration Ready" finale: a hologram of the facility rises and spins above the hub.
    ///  • Animation Rigging — gallery humanoids track the learner with a MultiAimConstraint on their head/neck yaw joint.
    ///  • Cables            — VerletCable teach-pendant cables to a controller cabinet at every station (Obi Rope stand-in).
    /// (DOTween is used at runtime: door slide, lamp/board punches, caption pop-in.)
    /// </summary>
    public static class LabExtras
    {
        // ------------------------------------------------------------------ cables
        public static void PendantCables(Transform chapters, Material cabinetMat)
        {
            var cableMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Robotics/Art/Materials/PendantCable.mat");
            if (!cableMat)
            {
                cableMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.08f, 0.08f, 0.09f) };
                cableMat.SetFloat("_Smoothness", 0.45f);
                AssetDatabase.CreateAsset(cableMat, "Assets/Robotics/Art/Materials/PendantCable.mat");
            }
            foreach (Transform st in chapters)
            {
                var pendant = st.Find("Teach Pendant");
                if (!pendant) continue;
                // Robot controller cabinet on the floor beside the pendant.
                var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cab.name = "Controller Cabinet"; cab.transform.SetParent(st, false);
                cab.transform.localPosition = new Vector3(-1.25f, 0.45f, 0.95f); cab.transform.localScale = new Vector3(0.45f, 0.9f, 0.4f);
                cab.GetComponent<Renderer>().sharedMaterial = cabinetMat;
                var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(led.GetComponent<Collider>());
                led.name = "Status LED"; led.transform.SetParent(cab.transform, false);
                led.transform.localPosition = new Vector3(0, 0.38f, 0.51f); led.transform.localScale = new Vector3(0.5f, 0.03f, 0.02f);
                led.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Robotics/Art/Materials/LightStrip.mat");

                var a = new GameObject("Cable Anchor (cabinet)").transform; a.SetParent(cab.transform, false); a.localPosition = new Vector3(0, -0.3f, 0.5f);
                var b = new GameObject("Cable Anchor (pendant)").transform; b.SetParent(pendant, false); b.localPosition = new Vector3(0, -0.2f, 0.02f);
                var cable = new GameObject("Pendant Cable", typeof(LineRenderer), typeof(VerletCable));
                cable.transform.SetParent(st, false);
                var lr = cable.GetComponent<LineRenderer>();
                lr.sharedMaterial = cableMat; lr.numCapVertices = 2; lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var vc = cable.GetComponent<VerletCable>(); vc.startAnchor = a; vc.endAnchor = b; vc.slack = 1.35f;
            }
        }

        // ------------------------------------------------------------------ animation rigging
        public static int HeadTracking(Transform gallery)
        {
            int n = 0;
            foreach (var art in gallery.GetComponentsInChildren<RobotArticulation>(true))
            {
                Transform yaw = null;
                foreach (var j in art.joints)
                    if (j.name == "head_yaw_joint" || j.name == "neck_yaw_joint" || j.name == "neck_z_joint") { yaw = j.bone; break; }
                if (!yaw) continue;

                var go = art.gameObject;
                if (!go.GetComponent<Animator>()) go.AddComponent<Animator>();
                var builder = go.GetComponent<RigBuilder>() ?? go.AddComponent<RigBuilder>();
                var rigGo = new GameObject("Head Rig"); rigGo.transform.SetParent(go.transform, false);
                var rig = rigGo.AddComponent<Rig>(); rig.weight = 0;
                builder.layers.Clear(); builder.layers.Add(new RigLayer(rig));

                var target = new GameObject("Head Aim Target").transform;
                target.SetParent(go.transform.parent, false);
                target.position = yaw.position + go.transform.forward * 2f;

                var aimGo = new GameObject("Look At Learner"); aimGo.transform.SetParent(rigGo.transform, false);
                var aim = aimGo.AddComponent<MultiAimConstraint>();
                var data = aim.data;
                data.constrainedObject = yaw;
                var src = new WeightedTransformArray(); src.Add(new WeightedTransform(target, 1f)); data.sourceObjects = src;
                // Bones rotate about their local Y (rig convention) — only that axis may turn.
                data.constrainedXAxis = false; data.constrainedYAxis = true; data.constrainedZAxis = false;
                data.aimAxis = NearestAxis(yaw.InverseTransformDirection(go.transform.forward));
                data.limits = new Vector2(-70f, 70f);
                data.maintainOffset = false;
                aim.data = data;

                var ht = go.AddComponent<HeadTracking>(); ht.aimTarget = target; ht.rig = rig;
                n++;
            }
            return n;
        }

        static MultiAimConstraintData.Axis NearestAxis(Vector3 local)
        {
            local.y = 0; // perpendicular to the yaw axis
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
                return local.x > 0 ? MultiAimConstraintData.Axis.X : MultiAimConstraintData.Axis.X_NEG;
            return local.z > 0 ? MultiAimConstraintData.Axis.Z : MultiAimConstraintData.Axis.Z_NEG;
        }

        // ------------------------------------------------------------------ timeline
        public static PlayableDirector FinaleTimeline(Transform hub, Material hologram)
        {
            Directory.CreateDirectory("Assets/Robotics/Timelines");
            const string tlPath = "Assets/Robotics/Timelines/Finale_DemonstrationReady.playable";
            const string clipPath = "Assets/Robotics/Timelines/Finale_HologramRise.anim";

            // Hologram of the facility: two counter-rotating rings + a core, using the Hologram shader.
            var holo = new GameObject("Facility Hologram");
            holo.transform.SetParent(hub, false); holo.transform.localPosition = new Vector3(0, 1.4f, 0);
            holo.AddComponent<Animator>();
            for (int i = 0; i < 3; i++)
            {
                var part = GameObject.CreatePrimitive(i == 2 ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.name = i == 2 ? "Core" : $"Ring {i + 1}";
                part.transform.SetParent(holo.transform, false);
                part.transform.localScale = i == 2 ? Vector3.one * 0.35f : new Vector3(1.6f - i * 0.5f, 0.015f, 1.6f - i * 0.5f);
                part.transform.localRotation = Quaternion.Euler(i == 1 ? 25 : 0, 0, i == 1 ? 15 : 0);
                part.GetComponent<Renderer>().sharedMaterial = hologram;
                part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            holo.SetActive(false);

            // Animation: rise 1.4 → 2.8 m, scale 0.2 → 1.4, full spin, over 8 s.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.ClearCurves();
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", AnimationCurve.EaseInOut(0, 1.4f, 8, 2.8f));
            foreach (var axis in new[] { "x", "y", "z" })
                clip.SetCurve("", typeof(Transform), "m_LocalScale." + axis, new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(2.5f, 1.5f), new Keyframe(8, 1.4f)));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Linear(0, 0, 8, 360));
            EditorUtility.SetDirty(clip);

            // Timeline: Activation (whole finale) + Animation (rise).
            var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(tlPath);
            if (tl) AssetDatabase.DeleteAsset(tlPath);
            tl = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(tl, tlPath);
            var act = tl.CreateTrack<ActivationTrack>(null, "Hologram Visible");
            var actClip = act.CreateDefaultClip(); actClip.start = 0; actClip.duration = 20;
            var anim = tl.CreateTrack<AnimationTrack>(null, "Hologram Rise");
            var animClip = anim.CreateClip(clip); animClip.start = 0.5; animClip.duration = 8;
            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();

            var dirGo = new GameObject("Finale Director");
            dirGo.transform.SetParent(hub, false);
            var director = dirGo.AddComponent<PlayableDirector>();
            director.playableAsset = tl; director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.SetGenericBinding(act, holo);
            director.SetGenericBinding(anim, holo.GetComponent<Animator>());
            return director;
        }
    }
}
