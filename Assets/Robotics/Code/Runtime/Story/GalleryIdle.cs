using UnityEngine;

namespace RoboticsLab.Story
{
    /// <summary>Starts a gallery robot's gentle idle motion when the learner is nearby (saves CPU elsewhere).</summary>
    public class GalleryIdle : MonoBehaviour
    {
        public RobotDemo demo;
        public float radius = 5f;
        Transform _head;
        bool _on;

        void Update()
        {
            if (_head == null && Camera.main) _head = Camera.main.transform;
            if (!_head || !demo) return;
            bool near = (_head.position - transform.position).sqrMagnitude < radius * radius;
            if (near != _on) { _on = near; demo.Play(near); }
        }
    }
}
