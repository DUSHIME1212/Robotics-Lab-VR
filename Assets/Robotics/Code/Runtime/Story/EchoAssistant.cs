using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboticsLab.Story
{
    /// <summary>
    /// ECHO, the holographic training assistant. A floating orb that flies to wherever the story is happening,
    /// plus a head-following caption panel with Replay / Next / Hint buttons (readable, replayable instructions).
    /// Lines are queued; optional voice clips can be assigned by line id.
    /// </summary>
    public class EchoAssistant : MonoBehaviour
    {
        [Serializable] public class Voice { public string id; public AudioClip clip; }

        public Transform orb;
        public AudioSource audioSource;
        public List<Voice> voices = new();
        public float charsPerSecond = 45f;
        public float followDistance = 1.3f;
        public float captionHeightOffset = 0.32f;
        public float captionSideOffset = 0f; // keep captions left of centre so they don't hide what you point at

        public event Action HintPressed;
        public bool Speaking => _queue.Count > 0 || _current != null;

        readonly Queue<(string id, string text)> _queue = new();
        (string id, string text)? _current;
        string _lastLine = "";
        RectTransform _caption;
        TMP_Text _text;
        Transform _head;
        Vector3 _orbTarget;
        bool _skip;
        Coroutine _runner;

        void Start()
        {
            BuildCaption();
            _orbTarget = orb ? orb.position : transform.position;
            _runner = StartCoroutine(Run());
        }

        /// <summary>Queue a line. Text may contain TMP rich text.</summary>
        public void Say(string text, string id = null) => _queue.Enqueue((id, text));

        public void SayNow(string text, string id = null)
        {
            _queue.Clear();
            _skip = true;
            Say(text, id);
        }

        public void FlyTo(Vector3 worldPos) => _orbTarget = worldPos;

        public void Replay() { if (!string.IsNullOrEmpty(_lastLine)) Say(_lastLine); }

        IEnumerator Run()
        {
            while (true)
            {
                if (_queue.Count == 0) { yield return null; continue; }
                _current = _queue.Dequeue();
                var (id, line) = _current.Value;
                _lastLine = line;
                _skip = false;
                if (!_caption.gameObject.activeSelf || _caption.localScale.x < 0.0009f)
                {
                    _caption.gameObject.SetActive(true);
                    _caption.DOKill();
                    _caption.localScale = Vector3.one * 0.0007f;
                    _caption.DOScale(0.001f, 0.35f).SetEase(Ease.OutBack); // DOTween caption pop-in
                }
                var clip = voices.Find(v => v.id == id)?.clip;
                if (clip && audioSource) audioSource.PlayOneShot(clip);

                _text.text = "<color=#6cf><b>ECHO</b></color>  " + line;
                _text.maxVisibleCharacters = 0;
                _text.ForceMeshUpdate();
                int total = _text.textInfo.characterCount;
                float t = 0;
                while (_text.maxVisibleCharacters < total && !_skip)
                {
                    t += Time.deltaTime * charsPerSecond;
                    _text.maxVisibleCharacters = Mathf.Min(total, (int)t);
                    yield return null;
                }
                _text.maxVisibleCharacters = total;
                float hold = clip ? clip.length : Mathf.Clamp(line.Length / 16f, 2.5f, 9f);
                float h = 0;
                _skip = false;
                while (h < hold && !_skip) { h += Time.deltaTime; yield return null; }
                _current = null;
                if (_queue.Count == 0) { yield return new WaitForSeconds(0.5f); }
            }
        }

        void LateUpdate()
        {
            if (_head == null && Camera.main) _head = Camera.main.transform;
            if (orb)
            {
                var bob = Vector3.up * Mathf.Sin(Time.time * 2f) * 0.03f;
                orb.position = Vector3.Lerp(orb.position, _orbTarget + bob, Time.deltaTime * 2.5f);
                if (_head) orb.rotation = Quaternion.LookRotation(orb.position - _head.position);
            }
            if (_head && _caption)
            {
                // Lazy-follow: caption sits below eye level in front of the learner, re-centres when they turn away.
                var fwd = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var desired = _head.position + fwd * followDistance + Vector3.up * captionHeightOffset + right * captionSideOffset;
                var toCaption = _caption.position - _head.position;
                if (Vector3.Angle(fwd, Vector3.ProjectOnPlane(toCaption, Vector3.up)) > 35f || toCaption.magnitude > 2.5f || toCaption.magnitude < 0.5f
                    || Mathf.Abs(_caption.position.y - desired.y) > 0.3f) // e.g. seated mode / eye-height change
                    _followTarget = desired;
                _caption.position = Vector3.Lerp(_caption.position, _followTarget, Time.deltaTime * 3f);
                _caption.rotation = Quaternion.LookRotation(_caption.position - _head.position);
            }
        }
        Vector3 _followTarget;

        void BuildCaption()
        {
            _caption = UIFactory.Canvas(transform, "ECHO Captions", new Vector2(900, 260));
            var bg = UIFactory.Background(_caption, new Color(0.03f, 0.08f, 0.12f, 0.9f), 20, 8);
            _text = UIFactory.Text(bg, "", 30, Color.white);
            _text.GetComponent<LayoutElement>().minHeight = 150;
            var row = UIFactory.Row(bg, 50);
            UIFactory.Button(row, "Replay", Replay, 20, new Color(0.2f, 0.3f, 0.4f));
            UIFactory.Button(row, "Next", () => _skip = true, 20, new Color(0.2f, 0.3f, 0.4f));
            UIFactory.Button(row, "? Hint", () => HintPressed?.Invoke(), 20, new Color(0.45f, 0.35f, 0.1f));
            _followTarget = _caption.position;
        }
    }
}
