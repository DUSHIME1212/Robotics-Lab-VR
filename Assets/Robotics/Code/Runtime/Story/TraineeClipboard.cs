using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboticsLab.Story
{
    /// <summary>
    /// The learner's handheld clipboard: a diegetic checklist printed on the paper. Shows the current chapter,
    /// the active step and all six verifications, updating as the story progresses. Grab it to read it up close;
    /// if it is dropped out of the world it returns to its stand.
    /// </summary>
    public class TraineeClipboard : MonoBehaviour
    {
        public LabStoryManager story;
        public RectTransform paperCanvas;
        public float refreshInterval = 0.4f;

        TMP_Text _header, _body, _footer;
        Vector3 _homePos;
        Quaternion _homeRot;
        Rigidbody _rb;
        float _t;
        string _last;

        void Start()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _rb = GetComponent<Rigidbody>();
            BuildPaper();
            Refresh();
        }

        void BuildPaper()
        {
            if (!paperCanvas) return;
            var col = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            col.transform.SetParent(paperCanvas, false);
            var r = (RectTransform)col.transform;
            UIFactory.Stretch(r);
            var v = col.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 18, 12); v.spacing = 6;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false;

            // Ink colours from the Meta Quest *light* theme read like printed paper.
            var ink = new Color(0.1f, 0.11f, 0.13f);
            var muted = new Color(0.35f, 0.37f, 0.4f);
            _header = UIFactory.Text(col.transform, "", 15, ink, FontStyles.Bold);
            _body = UIFactory.Text(col.transform, "", 11.5f, ink);
            _body.GetComponent<LayoutElement>().flexibleHeight = 1;
            _footer = UIFactory.Text(col.transform, "", 9, muted);
        }

        void Update()
        {
            if (transform.position.y < -2f) ReturnHome();
            _t += Time.deltaTime;
            if (_t < refreshInterval) return;
            _t = 0;
            Refresh();
        }

        public void ReturnHome()
        {
            if (_rb) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            transform.SetPositionAndRotation(_homePos, _homeRot);
        }

        void Refresh()
        {
            if (!_header || !story) return;
            var sb = new StringBuilder();
            int idx = story.CurrentChapterIndex;
            string header = "ATLAS · TRAINEE CHECKLIST";
            switch (story.phase)
            {
                case LabStoryManager.Phase.Arrival:
                    sb.Append("<b>Day 1 · Arrival</b>\n\nPoint at <b>START SHIFT</b> and pull the trigger.\n\nKeep this clipboard: it tracks every verification you complete.");
                    break;
                case LabStoryManager.Phase.Chapters when idx < story.chapters.Length:
                {
                    var c = story.chapters[idx];
                    sb.Append($"<b>Chapter {c.chapter:00} · {c.lessonTitle}</b>\n");
                    if (c.steps != null && c.CurrentStep < c.steps.Length)
                        sb.Append($"Step {c.CurrentStep + 1}/{c.steps.Length}\n<indent=4%>{Strip(c.steps[c.CurrentStep].instruction)}</indent>\n");
                    sb.Append('\n');
                    break;
                }
                case LabStoryManager.Phase.Finale:
                    sb.Append("<b>Final verification</b>\nGo to the central console and answer the six scenarios.\n\n");
                    break;
                default:
                    header = "ATLAS · SHIFT COMPLETE";
                    sb.Append("<b>Demonstration ready.</b>\nYour first shift is complete.\n\n");
                    break;
            }
            if (story.phase != LabStoryManager.Phase.Arrival)
            {
                foreach (var c in story.chapters)
                    sb.Append(c.Complete ? "[x] " : "[  ] ").Append(c.verificationName).Append('\n');
            }
            string text = sb.ToString();
            if (text == _last) return;
            _last = text;
            _header.text = header;
            _body.text = text;
            _footer.text = "Press ? Hint on ECHO's captions if you get stuck.";
        }

        static string Strip(string s) => s.Length > 170 ? s.Substring(0, 167) + "..." : s;
    }
}
