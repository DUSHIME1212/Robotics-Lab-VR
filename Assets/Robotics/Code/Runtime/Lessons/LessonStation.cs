using DG.Tweening;
using System;
using TMPro;
using UnityEngine;

namespace RoboticsLab
{
    /// <summary>A lesson step: instruction text, an automatic completion check and an ECHO hint.</summary>
    [Serializable]
    public class LessonStep
    {
        public enum Check { None, JointAngle, ReachTarget, ProgramRun, ObjectInZone, GaitMode, Timer, GaitTuned }
        [TextArea] public string instruction;
        [TextArea] public string hint;
        public Check check;
        public int jointIndex;
        public float value;
        public float tolerance = 5f;
        public Transform target;
        public Collider zone;
        public Rigidbody item;
    }

    /// <summary>
    /// One chapter of the story: concept, steps checked automatically against the robot,
    /// ECHO hints when the learner is stuck, and a Completed event the LabStoryManager listens to.
    /// </summary>
    public class LessonStation : MonoBehaviour
    {
        public int chapter;
        public string lessonTitle;
        public string verificationName = "Motion";
        [TextArea(3, 8)] public string concept;
        [TextArea] public string echoIntro;
        [TextArea] public string echoComplete;
        public RobotArticulation robot;
        public TeachPendant pendant;
        public GripperController gripper;
        public QuadrupedGait gait;
        public SafetyFenceMonitor safety;
        public LessonStep[] steps;
        public TMP_Text board;
        public Light spotlight;
        public float hintAfterSeconds = 45f;
        [Tooltip("Manufacturer spec sheet line (from Assets/Robotics/Data/robot_specs.json), shown under the steps.")]
        [TextArea] public string specs;

        public event Action<LessonStation> Completed;
        public event Action<LessonStation, string> HintRequested;
        public event Action<LessonStation, int> StepCompleted;

        int _step;
        float _timer, _stuck;
        bool _unlocked = true, _hinted;
        public bool Complete => _step >= (steps?.Length ?? 0);
        public int CurrentStep => _step;

        void Start() => Render();

        public void SetUnlocked(bool on)
        {
            _unlocked = on;
            if (pendant) pendant.gameObject.SetActive(on || Complete);
            if (spotlight) spotlight.enabled = on || Complete;
            Render();
        }

        public void ForceComplete()
        {
            if (Complete) return;
            _step = steps.Length;
            Render();
            Completed?.Invoke(this);
        }

        void Update()
        {
            if (!_unlocked || Complete || steps == null) return;
            _stuck += Time.deltaTime;
            if (!_hinted && _stuck > hintAfterSeconds && !string.IsNullOrEmpty(steps[_step].hint))
            {
                _hinted = true;
                HintRequested?.Invoke(this, steps[_step].hint);
            }
            if (safety && safety.Violated)
            {
                // Safety chapter: entering the cell resets the current step.
                safety.Acknowledge();
                if (pendant) pendant.Status("E-STOP: you entered the cell. Step reset.");
                _timer = 0;
                return;
            }
            if (Passed(steps[_step]))
            {
                StepCompleted?.Invoke(this, _step);
                _step++;
                _timer = _stuck = 0;
                _hinted = false;
                Render();
                if (board) { board.transform.parent.DOKill(true); board.transform.parent.DOPunchScale(Vector3.one * 0.06f * board.transform.parent.localScale.x, 0.5f, 5, 0.6f); } // DOTween step feedback
                if (Complete) Completed?.Invoke(this);
            }
        }

        public void RequestHint()
        {
            if (!Complete && steps != null) HintRequested?.Invoke(this, steps[_step].hint);
        }

        bool Passed(LessonStep s)
        {
            switch (s.check)
            {
                case LessonStep.Check.JointAngle:
                    return robot && Mathf.Abs(robot.GetAngle(s.jointIndex) - s.value) <= s.tolerance;
                case LessonStep.Check.ReachTarget:
                    return robot && robot.endEffector && s.target && Vector3.Distance(robot.endEffector.position, s.target.position) <= s.tolerance;
                case LessonStep.Check.ProgramRun:
                    return pendant && pendant.ProgramCompleted && pendant.ProgramLength >= (int)s.value;
                case LessonStep.Check.ObjectInZone:
                    return s.zone && s.item && s.zone.bounds.Contains(s.item.position) && (gripper == null || !gripper.IsHolding);
                case LessonStep.Check.GaitMode:
                    return gait && (int)gait.mode == (int)s.value;
                case LessonStep.Check.GaitTuned:
                    return gait && gait.mode == QuadrupedGait.Mode.Trot && Mathf.Abs(gait.frequency - s.value) <= s.tolerance;
                case LessonStep.Check.Timer:
                    return (_timer += Time.deltaTime) >= s.value;
                default:
                    return false;
            }
        }

        public void Skip() { if (!Complete) { _step++; Render(); if (Complete) Completed?.Invoke(this); } }
        public void Restart() { _step = 0; Render(); }

        void Render()
        {
            if (!board) return;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=80%><color=#7fa>CHAPTER {chapter:00}</color></size>\n<size=140%><b>{lessonTitle}</b></size>\n");
            if (!_unlocked && !Complete)
            {
                sb.Append("\n<color=#fb4>LOCKED · Complete the previous verification first.</color>");
                board.text = sb.ToString();
                return;
            }
            sb.Append($"<color=#9fd8ff>{concept}</color>\n\n");
            for (int i = 0; i < steps.Length; i++)
            {
                string mark = i < _step ? "<color=#6f6>OK</color>" : i == _step ? "<color=#fd4>></color>" : "<color=#888>-</color>";
                string col = i == _step ? "white" : "#aaa";
                sb.Append($"{mark} <color={col}>{steps[i].instruction}</color>\n");
            }
            if (Complete) sb.Append($"\n<color=#6f6><b>{verificationName.ToUpper()} VERIFICATION COMPLETE</b></color>");
            if (!string.IsNullOrEmpty(specs)) sb.Append($"\n\n<size=80%><color=#8fb4c8><b>SPEC SHEET</b>  {specs}</color></size>");
            board.text = sb.ToString();
        }
    }
}
