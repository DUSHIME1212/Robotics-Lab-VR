using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RoboticsLab
{
    /// <summary>
    /// World-space teach pendant built at runtime for any RobotArticulation:
    /// one slider per joint (jog), live joint + Cartesian readout (forward kinematics),
    /// Home / Record waypoint / Run program / Clear, optional IK toggle and gripper/gait buttons.
    /// Works with XRI ray interactors through TrackedDeviceGraphicRaycaster.
    /// </summary>
    public class TeachPendant : MonoBehaviour
    {
        public RobotArticulation robot;
        public CCDIKSolver ik;
        public GripperController gripper;
        public QuadrupedGait gait;
        public string title = "Teach Pendant";
        public Vector2 size = new(520, 640);

        readonly List<Slider> _sliders = new();
        readonly List<TMP_Text> _labels = new();
        readonly List<float[]> _program = new();
        TMP_Text _readout, _status;
        int _playIndex = -1;
        bool _suppress;

        public int ProgramLength => _program.Count;
        public bool ProgramCompleted { get; private set; }

        void Start()
        {
            BuildUI();
            if (robot) robot.Changed += Refresh;
            Refresh();
        }

        void OnDestroy() { if (robot) robot.Changed -= Refresh; }

        // ------------------------------------------------------------------ UI construction
        void BuildUI()
        {
            var canvasGo = new GameObject("PendantCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.001f;

            var bg = Panel(rt, "Background", new Color(0.08f, 0.1f, 0.13f, 0.95f));
            Stretch(bg);
            var layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 6;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            Text(bg, title, 30, FontStyles.Bold, new Color(0.4f, 0.85f, 1f));
            _status = Text(bg, "", 18, FontStyles.Italic, new Color(1f, 0.85f, 0.4f));

            bool jointSliders = gait == null && gripper == null;
            if (robot && jointSliders)
            {
                for (int i = 0; i < robot.Count; i++)
                {
                    int idx = i;
                    var j = robot.joints[i];
                    _labels.Add(Text(bg, j.name, 18, FontStyles.Normal, Color.white));
                    var s = SliderUI(bg, j.lower, j.upper);
                    s.onValueChanged.AddListener(v => { if (!_suppress) { if (ik) ik.solve = false; robot.SetAngle(idx, v); } });
                    _sliders.Add(s);
                }
            }

            _readout = Text(bg, "", 18, FontStyles.Normal, new Color(0.7f, 1f, 0.7f));

            var row1 = Row(bg);
            if (robot && jointSliders)
            {
                Button(row1, "Home", () => { if (ik) ik.solve = false; robot.Home(); });
                Button(row1, "Record", Record);
                Button(row1, "Run", Run);
                Button(row1, "Clear", () => { _program.Clear(); ProgramCompleted = false; Status("Program cleared"); });
            }
            if (ik)
            {
                var row2 = Row(bg);
                Button(row2, "IK: drag target", () => { ik.solve = !ik.solve; Status(ik.solve ? "IK ON: grab the orange handle" : "IK OFF: jog joints with sliders"); });
            }
            if (gripper)
            {
                var row = Row(bg);
                Button(row, "Open", gripper.Open);
                Button(row, "Close", gripper.Close);
            }
            if (gait)
            {
                var row = Row(bg);
                Button(row, "Stand", () => gait.SetMode(0));
                Button(row, "Trot", () => gait.SetMode(1));
                Button(row, "Sit", () => gait.SetMode(2));
                Text(bg, "Gait frequency (Hz)", 18, FontStyles.Normal, Color.white);
                var f = SliderUI(bg, 0.2f, 3f); f.value = gait.frequency; f.onValueChanged.AddListener(v => gait.frequency = v);
                Text(bg, "Stride (deg)", 18, FontStyles.Normal, Color.white);
                var st = SliderUI(bg, 0f, 30f); st.value = gait.strideDeg; st.onValueChanged.AddListener(v => gait.strideDeg = v);
            }
        }

        // ------------------------------------------------------------------ behaviour
        void Record()
        {
            _program.Add(robot.CurrentPose());
            Status($"Waypoint P{_program.Count} recorded");
        }

        void Run()
        {
            if (_program.Count == 0) { Status("Record at least one waypoint first"); return; }
            if (ik) ik.solve = false;
            _playIndex = 0;
            robot.MoveTo(_program[0]);
            Status($"Running: MoveJ P1 / {_program.Count}");
        }

        void Update()
        {
            if (_playIndex < 0 || robot.IsMoving) return;
            _playIndex++;
            if (_playIndex >= _program.Count) { _playIndex = -1; ProgramCompleted = true; Status("Program finished"); return; }
            robot.MoveTo(_program[_playIndex]);
            Status($"Running: MoveJ P{_playIndex + 1} / {_program.Count}");
        }

        void Refresh()
        {
            if (!robot) return;
            _suppress = true;
            for (int i = 0; i < _sliders.Count; i++)
            {
                _sliders[i].SetValueWithoutNotify(robot.GetAngle(i));
                _labels[i].text = $"J{i + 1}  {robot.joints[i].name}   <b>{robot.GetAngle(i):0.0}°</b>   [{robot.joints[i].lower:0}..{robot.joints[i].upper:0}]";
            }
            _suppress = false;
            if (_readout && robot.endEffector)
            {
                var p = robot.transform.InverseTransformPoint(robot.endEffector.position);
                var sb = new StringBuilder();
                sb.Append($"Tool position (base frame): X {p.x * 1000:0} mm  Y {p.y * 1000:0} mm  Z {p.z * 1000:0} mm");
                if (ik && ik.solve) sb.Append($"\nIK error: {ik.LastError * 1000:0.0} mm");
                if (_program.Count > 0) sb.Append($"\nProgram: {_program.Count} waypoint(s)");
                _readout.text = sb.ToString();
            }
        }

        public void Status(string s) { if (_status) _status.text = s; }

        // ------------------------------------------------------------------ UI helpers (Meta UI Set via UIFactory)
        static RectTransform Panel(Transform parent, string n, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Story.UIFactory.StyleBackplate(go.GetComponent<Image>(), c);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r) => Story.UIFactory.Stretch(r);

        static TMP_Text Text(Transform parent, string s, float sizePx, FontStyles style, Color c)
        {
            // Plain white body text follows the Meta theme's primary text colour.
            if (c == Color.white) c = Story.UIFactory.TextPrimary;
            return Story.UIFactory.Text(parent, s, sizePx, c, style);
        }

        static RectTransform Row(Transform parent) => Story.UIFactory.Row(parent, 56);

        // Record/Run are the primary actions; Home/Open/Stand etc. are secondary; Clear is destructive.
        static void Button(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var style = label switch
            {
                "Run" or "Record" or "IK: drag target" or "Close" or "Trot" => Story.ButtonStyle.Primary,
                "Clear" => Story.ButtonStyle.Destructive,
                _ => Story.ButtonStyle.Secondary,
            };
            Story.UIFactory.Button(parent, label, onClick, style, 20);
        }

        static Slider SliderUI(Transform parent, float min, float max) => Story.UIFactory.Slider(parent, min, max, 0);
    }
}
