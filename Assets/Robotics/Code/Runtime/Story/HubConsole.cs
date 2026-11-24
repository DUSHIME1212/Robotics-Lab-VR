using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboticsLab.Story
{
    /// <summary>
    /// Central verification console: six indicator lamps (amber = pending, green = verified),
    /// facility status, the training summary and accessibility options (seated mode, reset progress).
    /// </summary>
    public class HubConsole : MonoBehaviour
    {
        public Renderer[] lamps = new Renderer[6];
        public Light[] lampLights = new Light[6];
        public string[] names = { "Motion", "Precision", "Programming", "Handling", "Safety", "Locomotion" };
        public TMP_Text facilitySign;
        public Transform panelAnchor;

        public event Action ResetPressed;
        public event Action<bool> SeatedModeChanged;

        TMP_Text _status;
        Button _seatedButton;
        bool _seated;
        readonly bool[] _done = new bool[6];
        bool _ready;

        void Start()
        {
            var c = UIFactory.Canvas(panelAnchor ? panelAnchor : transform, "Hub Panel", new Vector2(640, 520));
            var bg = UIFactory.Background(c, UIFactory.Panel);
            UIFactory.Text(bg, "ATLAS · SYSTEM VERIFICATION", 30, UIFactory.Accent, FontStyles.Bold);
            _status = UIFactory.Text(bg, "", 26, Color.white);
            var row = UIFactory.Row(bg, 50);
            _seatedButton = UIFactory.Button(row, "Seated mode: OFF", ToggleSeated, 20, new Color(0.2f, 0.3f, 0.4f));
            UIFactory.Button(row, "Reset progress", () => ResetPressed?.Invoke(), 20, new Color(0.45f, 0.2f, 0.2f));
            Refresh();
        }

        public void SetVerified(int index, bool on)
        {
            _done[index] = on;
            var col = on ? UIFactory.Green : UIFactory.Amber;
            if (lamps[index])
            {
                // PulseGlow shader: _Color = glow, _BaseColor = dark body. Verified lamps glow steadily green.
                var mpb = new MaterialPropertyBlock();
                lamps[index].GetPropertyBlock(mpb);
                mpb.SetColor("_Color", col);
                mpb.SetColor("_BaseColor", col * 0.15f);
                mpb.SetFloat("_PulseMin", on ? 1f : 0.45f);
                lamps[index].SetPropertyBlock(mpb);
                if (on && Application.isPlaying)
                {
                    // DOTween: verification "pop" on the lamp.
                    var t = lamps[index].transform;
                    t.DOKill(true);
                    t.DOPunchScale(new Vector3(0.6f, 2f, 0.6f) * t.localScale.x, 0.6f, 6, 0.5f);
                }
            }
            if (lampLights[index]) lampLights[index].color = col;
            Refresh();
        }

        public void SetDemonstrationReady(bool ready)
        {
            _ready = ready;
            Refresh();
        }

        void ToggleSeated()
        {
            _seated = !_seated;
            UIFactory.SetLabel(_seatedButton, _seated ? "Seated mode: ON" : "Seated mode: OFF");
            SeatedModeChanged?.Invoke(_seated);
        }

        void Refresh()
        {
            int n = 0;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                if (_done[i]) n++;
                sb.Append(_done[i] ? "<color=#6f6>[x] </color>" : "<color=#fb4>[ ] </color>").Append(names[i]).Append(_done[i] ? "  <color=#6f6>verified</color>\n" : "\n");
            }
            sb.Insert(0, $"Verified {n}/6\n\n");
            if (_status) _status.text = sb.ToString();
            if (facilitySign)
            {
                facilitySign.text = _ready ? "ATLAS ROBOTICS LAB · <color=#6f6>DEMONSTRATION READY</color>"
                                           : "ATLAS ROBOTICS LAB · <color=#fb4>SYSTEM VERIFICATION REQUIRED</color>";
            }
        }
    }
}
