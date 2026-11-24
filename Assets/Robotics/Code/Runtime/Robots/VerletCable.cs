using UnityEngine;

namespace RoboticsLab
{
    /// <summary>
    /// Lightweight physical cable (Verlet integration + distance constraints), used for teach-pendant cables.
    /// A free, Quest-cheap stand-in for Obi Rope: sags under gravity, swings when the pendant or cabinet moves,
    /// collides with the floor plane. Rendered as a LineRenderer tube.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class VerletCable : MonoBehaviour
    {
        public Transform startAnchor, endAnchor;
        [Range(4, 40)] public int segments = 18;
        [Tooltip("Cable length as a multiple of the anchor distance (>1 = slack).")]
        public float slack = 1.25f;
        [Range(1, 20)] public int iterations = 8;
        public float damping = 0.985f;
        public float floorY = 0.01f;
        public float radius = 0.012f;

        Vector3[] _pos, _prev;
        LineRenderer _line;
        float _segLen;

        void Start()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = segments + 1;
            _line.startWidth = _line.endWidth = radius * 2;
            _line.useWorldSpace = true;
            _pos = new Vector3[segments + 1];
            _prev = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
                _pos[i] = _prev[i] = Vector3.Lerp(startAnchor.position, endAnchor.position, i / (float)segments);
        }

        void FixedUpdate()
        {
            if (!startAnchor || !endAnchor) return;
            _segLen = Vector3.Distance(startAnchor.position, endAnchor.position) * slack / segments;
            var g = Physics.gravity * (Time.fixedDeltaTime * Time.fixedDeltaTime);
            for (int i = 1; i < segments; i++)
            {
                var v = (_pos[i] - _prev[i]) * damping;
                _prev[i] = _pos[i];
                _pos[i] += v + g;
            }
            for (int k = 0; k < iterations; k++)
            {
                _pos[0] = startAnchor.position;
                _pos[segments] = endAnchor.position;
                for (int i = 0; i < segments; i++)
                {
                    var d = _pos[i + 1] - _pos[i];
                    float len = d.magnitude;
                    if (len < 1e-5f) continue;
                    var corr = d * ((len - _segLen) / len) * 0.5f;
                    if (i != 0) _pos[i] += corr;
                    if (i + 1 != segments) _pos[i + 1] -= corr;
                }
                for (int i = 1; i < segments; i++) if (_pos[i].y < floorY + radius) _pos[i].y = floorY + radius;
            }
        }

        void LateUpdate() { if (_line) _line.SetPositions(_pos); }
    }
}
