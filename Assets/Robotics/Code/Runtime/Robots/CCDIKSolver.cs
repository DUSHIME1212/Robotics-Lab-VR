using UnityEngine;

namespace RoboticsLab
{
    /// <summary>
    /// Cyclic Coordinate Descent inverse kinematics on top of RobotArticulation.
    /// Learners grab the target handle and the arm solves joint angles to reach it, respecting joint limits.
    /// </summary>
    [RequireComponent(typeof(RobotArticulation))]
    public class CCDIKSolver : MonoBehaviour
    {
        public Transform target;
        public bool solve = true;
        [Range(1, 30)] public int iterations = 12;
        public float tolerance = 0.005f;

        RobotArticulation _robot;
        public float LastError { get; private set; }

        void Awake() => _robot = GetComponent<RobotArticulation>();

        void LateUpdate()
        {
            if (!solve || target == null || _robot.endEffector == null || _robot.IsMoving) return;
            for (int it = 0; it < iterations; it++)
            {
                for (int i = _robot.Count - 1; i >= 0; i--)
                {
                    var j = _robot.joints[i];
                    Vector3 pivot = j.bone.position;
                    Vector3 axis = j.WorldAxis;
                    Vector3 toEnd = Vector3.ProjectOnPlane(_robot.endEffector.position - pivot, axis);
                    Vector3 toTarget = Vector3.ProjectOnPlane(target.position - pivot, axis);
                    if (toEnd.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-6f) continue;
                    float delta = Vector3.SignedAngle(toEnd, toTarget, axis);
                    _robot.joints[i].SetAngle(j.Angle + delta * 0.8f);
                }
                LastError = Vector3.Distance(_robot.endEffector.position, target.position);
                if (LastError < tolerance) break;
            }
            _robot.SetAngle(0, _robot.GetAngle(0)); // raise Changed for UI
        }
    }
}
