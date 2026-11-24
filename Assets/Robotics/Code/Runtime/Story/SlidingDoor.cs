using DG.Tweening;
using UnityEngine;

namespace RoboticsLab.Story
{
    /// <summary>Two door leaves that slide apart when the shift starts (DOTween: eased slide + a small settle bounce).</summary>
    public class SlidingDoor : MonoBehaviour
    {
        public Transform left, right;
        public float travel = 0.9f;
        public float duration = 1.8f;
        public AudioSource sound;
        Vector3 _l0, _r0;
        bool _open;

        void Awake() { if (left) _l0 = left.localPosition; if (right) _r0 = right.localPosition; }

        public void Open(bool instant = false)
        {
            if (_open) return;
            _open = true;
            if (instant)
            {
                if (left) left.localPosition = _l0 + Vector3.left * travel;
                if (right) right.localPosition = _r0 + Vector3.right * travel;
                return;
            }
            if (sound) sound.Play();
            // Short "unlock" nudge, then a smooth industrial slide.
            var seq = DOTween.Sequence();
            if (left) seq.Join(left.DOLocalMove(_l0 + Vector3.right * 0.02f, 0.15f).SetEase(Ease.OutQuad));
            if (right) seq.Join(right.DOLocalMove(_r0 + Vector3.left * 0.02f, 0.15f).SetEase(Ease.OutQuad));
            seq.AppendInterval(0.1f);
            if (left) seq.Append(left.DOLocalMove(_l0 + Vector3.left * travel, duration).SetEase(Ease.InOutCubic));
            if (right) seq.Join(right.DOLocalMove(_r0 + Vector3.right * travel, duration).SetEase(Ease.InOutCubic));
        }

        void OnDestroy() { if (left) left.DOKill(); if (right) right.DOKill(); }
    }
}
