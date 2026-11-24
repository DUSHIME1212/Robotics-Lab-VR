using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoboticsLab
{
    /// <summary>
    /// Drives a robot rigged by Tools/Blender/rig_simready.py.
    /// Every joint bone points along its rotation axis, so a joint angle is a rotation about the bone's local Y.
    /// Limits come from the exported &lt;Robot&gt;.joints.json (originally the SimReady UsdPhysics limits).
    /// </summary>
    public class RobotArticulation : MonoBehaviour
    {
        [Serializable] class JointJson { public string name; public string type; public float lower; public float upper; public string parent; }
        [Serializable] class RobotJson { public string robot; public JointJson[] joints; }

        [Serializable]
        public class Joint
        {
            public string name;
            public Transform bone;
            public float lower = -180, upper = 180;
            [NonSerialized] public Quaternion rest;
            [SerializeField] float angle;
            public float Angle => angle;
            public float SetAngle(float a) { angle = Mathf.Clamp(a, lower, upper); Apply(); return angle; }
            public void Apply() { if (bone) bone.localRotation = rest * Quaternion.AngleAxis(angle, Vector3.up); }
            public Vector3 WorldAxis => bone ? bone.parent.rotation * rest * Vector3.up : Vector3.up;
        }

        public TextAsset jointDefinition;
        [Tooltip("Optional: tip of the tool. Defaults to the last joint's bone.")]
        public Transform endEffector;
        [Tooltip("Degrees per second when moving towards a commanded pose.")]
        public float maxJointSpeed = 90f;
        public List<Joint> joints = new();

        public event Action Changed;
        float[] _targets;

        void Awake() => Build();

        public void Build()
        {
            if (jointDefinition == null) return;
            var def = JsonUtility.FromJson<RobotJson>(jointDefinition.text);
            var bones = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name, t);
            joints.Clear();
            foreach (var j in def.joints)
            {
                if (!bones.TryGetValue(j.name, out var b)) { Debug.LogWarning($"[{name}] bone '{j.name}' not found"); continue; }
                joints.Add(new Joint { name = j.name, bone = b, lower = j.lower, upper = j.upper, rest = b.localRotation });
            }
            if (endEffector == null && joints.Count > 0) endEffector = joints[^1].bone;
            _targets = new float[joints.Count];
        }

        public int Count => joints.Count;
        public float GetAngle(int i) => joints[i].Angle;

        public void SetAngle(int i, float deg)
        {
            joints[i].SetAngle(deg);
            if (_targets != null) _targets[i] = joints[i].Angle;
            Changed?.Invoke();
        }

        /// <summary>Smoothly move all joints to a pose (joint-space interpolation, like a MoveJ instruction).</summary>
        public void MoveTo(float[] pose)
        {
            for (int i = 0; i < Mathf.Min(pose.Length, _targets.Length); i++)
                _targets[i] = Mathf.Clamp(pose[i], joints[i].lower, joints[i].upper);
            _moving = true;
        }

        public void Home() => MoveTo(new float[joints.Count]);
        public bool IsMoving => _moving;
        bool _moving;

        void Update()
        {
            if (!_moving) return;
            bool done = true;
            for (int i = 0; i < joints.Count; i++)
            {
                float a = Mathf.MoveTowards(joints[i].Angle, _targets[i], maxJointSpeed * Time.deltaTime);
                joints[i].SetAngle(a);
                if (!Mathf.Approximately(a, _targets[i])) done = false;
            }
            _moving = !done;
            Changed?.Invoke();
        }

        public float[] CurrentPose()
        {
            var p = new float[joints.Count];
            for (int i = 0; i < p.Length; i++) p[i] = joints[i].Angle;
            return p;
        }

        void OnDrawGizmosSelected()
        {
            foreach (var j in joints)
            {
                if (!j.bone) continue;
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(j.bone.position, j.bone.up * 0.15f);
            }
        }
    }
}
