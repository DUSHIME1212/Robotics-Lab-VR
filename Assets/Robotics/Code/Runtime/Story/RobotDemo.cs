using UnityEngine;

namespace RoboticsLab.Story
{
    /// <summary>Plays a verified demonstration motion for the finale (smooth joint waves, gripper cycling, trot).</summary>
    public class RobotDemo : MonoBehaviour
    {
        public RobotArticulation robot;
        public GripperController gripper;
        public QuadrupedGait gait;
        public float amplitude = 0.35f; // fraction of each joint's range
        public float speed = 0.5f;
        bool _on;
        float _t;

        public void Play(bool on)
        {
            _on = on;
            if (gait) gait.SetMode(on ? 1 : 0);
            if (!on && robot && !gait && !gripper) robot.Home();
        }

        void Update()
        {
            if (!_on) return;
            _t += Time.deltaTime * speed;
            if (gripper) { if (Mathf.Repeat(_t, 2f) < 1f) gripper.Close(); else gripper.Open(); return; }
            if (gait || robot == null) return;
            for (int i = 0; i < robot.Count; i++)
            {
                var j = robot.joints[i];
                float mid = Mathf.Clamp(0, j.lower, j.upper);
                float range = Mathf.Min(j.upper - j.lower, 180f) * 0.5f * amplitude;
                j.SetAngle(mid + Mathf.Sin(_t * Mathf.PI * 2f * 0.5f + i * 0.9f) * range);
            }
        }
    }
}
