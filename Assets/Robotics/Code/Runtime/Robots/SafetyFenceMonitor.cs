using UnityEngine;

namespace RoboticsLab
{
    /// <summary>
    /// Simulated light curtain for the Comau cell. If the learner's head enters the fenced volume while the
    /// robot is moving, the robot is stopped (simulated E-stop), a beacon turns red and the violation is reported.
    /// This teaches awareness only; it is not a real safety system.
    /// </summary>
    public class SafetyFenceMonitor : MonoBehaviour
    {
        public RobotArticulation robot;
        public Bounds cell = new(Vector3.zero, new Vector3(2, 3, 2));
        public Light beacon;
        public Renderer beaconRenderer;
        [Tooltip("Light-curtain planes using the RoboticsLab/EnergyField shader; their _Alarm rises on a violation.")]
        public Renderer[] curtains;
        public float alarmHoldSeconds = 2.5f;
        public bool Violated { get; private set; }
        public int ViolationCount { get; private set; }
        public event System.Action Tripped;

        static readonly int AlarmId = Shader.PropertyToID("_Alarm");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Transform _head;
        bool _inside;
        float _alarm, _alarmUntil;
        MaterialPropertyBlock _mpb;

        void Update()
        {
            if (_head == null && Camera.main) _head = Camera.main.transform;
            if (_head == null) return;
            bool inside = cell.Contains(transform.InverseTransformPoint(_head.position));
            if (inside && !_inside)
            {
                Violated = true;
                ViolationCount++;
                if (robot) robot.MoveTo(robot.CurrentPose()); // freeze in place
                Tripped?.Invoke();
            }
            _inside = inside;
            if (inside) _alarmUntil = Time.time + alarmHoldSeconds;
            var c = inside ? Color.red : (robot && robot.IsMoving ? new Color(1f, 0.6f, 0f) : Color.green);
            if (beacon) beacon.color = c;

            _mpb ??= new MaterialPropertyBlock();
            if (beaconRenderer)
            {
                beaconRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorId, c);                // PulseGlow glow colour
                _mpb.SetColor(BaseColorId, c * 0.15f);    // and its dark base
                beaconRenderer.SetPropertyBlock(_mpb);
            }
            _alarm = Mathf.MoveTowards(_alarm, Time.time < _alarmUntil ? 1f : 0f, Time.deltaTime * 3f);
            if (curtains != null)
                foreach (var r in curtains)
                {
                    if (!r) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(AlarmId, _alarm);
                    r.SetPropertyBlock(_mpb);
                }
        }

        public void Acknowledge() => Violated = false;

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 0.2f, 0.1f, 0.3f);
            Gizmos.DrawWireCube(cell.center, cell.size);
        }
    }
}
