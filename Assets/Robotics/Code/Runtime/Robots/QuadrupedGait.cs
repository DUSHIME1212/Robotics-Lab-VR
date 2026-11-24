using UnityEngine;

namespace RoboticsLab
{
    /// <summary>
    /// Teaching gait generator for the Lite3 quadruped: a trot where diagonal leg pairs (FL+HR, FR+HL)
    /// swing together. Exposes frequency and stride so learners can see how the gait parameters change motion.
    /// </summary>
    [RequireComponent(typeof(RobotArticulation))]
    public class QuadrupedGait : MonoBehaviour
    {
        public enum Mode { Stand, Trot, Sit }
        public Mode mode = Mode.Stand;
        [Range(0.2f, 3f)] public float frequency = 1.6f;
        [Range(0f, 30f)] public float strideDeg = 18f;
        [Range(0f, 40f)] public float liftDeg = 25f;
        public float standHip = -40f, standKnee = 80f;
        public bool moveBody = true;
        public float speedPerStride = 0.25f;

        RobotArticulation _robot;
        float _phase;

        void Awake() => _robot = GetComponent<RobotArticulation>();

        public void SetMode(int m) => mode = (Mode)m;

        void Update()
        {
            if (mode == Mode.Trot) _phase += Time.deltaTime * frequency * Mathf.PI * 2;
            foreach (var j in _robot.joints)
            {
                string leg = j.name.Substring(0, 2);
                bool diagonalA = leg == "FL" || leg == "HR";
                float ph = _phase + (diagonalA ? 0 : Mathf.PI);
                float swing = mode == Mode.Trot ? Mathf.Sin(ph) : 0;
                float lift = mode == Mode.Trot ? Mathf.Max(0, Mathf.Cos(ph)) : 0;
                float hip = mode == Mode.Sit && leg[0] == 'H' ? -90 : standHip;
                float knee = mode == Mode.Sit && leg[0] == 'H' ? 150 : standKnee;

                if (j.name.Contains("HipX")) j.SetAngle(0);
                else if (j.name.Contains("HipY")) j.SetAngle(hip + swing * strideDeg - lift * liftDeg * 0.5f);
                else if (j.name.Contains("Knee")) j.SetAngle(knee + lift * liftDeg);
            }
            if (moveBody && mode == Mode.Trot)
                transform.position += transform.forward * (speedPerStride * frequency * Time.deltaTime * strideDeg / 18f);
        }
    }
}
