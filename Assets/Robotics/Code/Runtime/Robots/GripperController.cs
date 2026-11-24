using UnityEngine;

namespace RoboticsLab
{
    /// <summary>
    /// Robotiq 2F-85 style parallel gripper. One command value (0 = open, 1 = closed) drives the
    /// linked joints the way the real 4-bar mechanism does: outer knuckles rotate, inner fingers counter-rotate
    /// so the pads stay parallel. Objects inside the grasp zone are picked up when closed.
    /// </summary>
    [RequireComponent(typeof(RobotArticulation))]
    public class GripperController : MonoBehaviour
    {
        [Range(0, 1)] public float closed;
        public float maxKnuckleDeg = 45f;
        public float speed = 1.5f;
        public Vector3 graspZoneSize = new(0.05f, 0.04f, 0.04f);
        public Transform graspCenter;

        RobotArticulation _robot;
        float _target;
        Rigidbody _held;

        void Awake() => _robot = GetComponent<RobotArticulation>();

        public void Open() => _target = 0;
        public void Close() => _target = 1;
        public void Toggle() => _target = _target > 0.5f ? 0 : 1;
        public bool IsHolding => _held != null;

        void Update()
        {
            closed = Mathf.MoveTowards(closed, _target, speed * Time.deltaTime);
            float a = closed * maxKnuckleDeg;
            foreach (var j in _robot.joints)
            {
                bool right = j.name.StartsWith("right");
                float s = right ? -1 : 1;
                if (j.name.Contains("outer_knuckle") || j.name == "finger_joint") j.SetAngle(right ? a : a);
                else if (j.name.Contains("inner_finger_joint")) j.SetAngle(-a * s);
                else if (j.name.Contains("inner_finger_knuckle")) j.SetAngle(a * s);
            }

            if (closed > 0.9f && _held == null) TryGrab();
            if (closed < 0.5f && _held != null) Release();
        }

        void TryGrab()
        {
            var c = graspCenter ? graspCenter : transform;
            foreach (var col in Physics.OverlapBox(c.position, graspZoneSize * 0.5f, c.rotation))
            {
                if (col.attachedRigidbody == null || col.attachedRigidbody.isKinematic) continue;
                _held = col.attachedRigidbody;
                _held.isKinematic = true;
                _held.transform.SetParent(c, true);
                return;
            }
        }

        void Release()
        {
            _held.transform.SetParent(null, true);
            _held.isKinematic = false;
            _held = null;
        }

        void OnDrawGizmosSelected()
        {
            var c = graspCenter ? graspCenter : transform;
            Gizmos.matrix = c.localToWorldMatrix;
            Gizmos.color = new Color(0, 1, 0, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, graspZoneSize);
        }
    }
}
