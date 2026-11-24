using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace RoboticsLab.Story
{
    /// <summary>
    /// Animation Rigging: humanoid robots in the gallery turn their head (yaw joint) toward the learner.
    /// The builder sets up RigBuilder + Rig + MultiAimConstraint; this keeps the aim target on the user's head
    /// and fades the constraint in only when the learner is close.
    /// </summary>
    public class HeadTracking : MonoBehaviour
    {
        public Transform aimTarget;
        public Rig rig;
        public float range = 4f;
        Transform _head;

        void Update()
        {
            if (_head == null && Camera.main) _head = Camera.main.transform;
            if (!_head || !aimTarget || !rig) return;
            aimTarget.position = Vector3.Lerp(aimTarget.position, _head.position, Time.deltaTime * 4f);
            float near = Mathf.InverseLerp(range, range * 0.5f, Vector3.Distance(_head.position, transform.position));
            rig.weight = Mathf.MoveTowards(rig.weight, near, Time.deltaTime * 1.5f);
        }
    }
}
