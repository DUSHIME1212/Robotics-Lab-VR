using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace RoboticsLab.Story
{
    /// <summary>
    /// "FIRST SHIFT" story director. Arrival -> six chapters (unlocked in order) -> final verification ->
    /// demonstration -> training summary. Listens to each LessonStation's Completed event, drives ECHO,
    /// the hub lamps, station lights and doors, and saves progress for classroom use.
    /// </summary>
    public class LabStoryManager : MonoBehaviour
    {
        public enum Phase { Arrival, Chapters, Finale, Demonstration, Complete }

        public EchoAssistant echo;
        public HubConsole hub;
        public LessonStation[] chapters = new LessonStation[6];
        public RobotDemo[] demos;
        public AssessmentConsole assessment;
        public SlidingDoor labDoor;
        public GameObject startButton;
        public Transform echoLobbyPoint, echoHubPoint;
        public TMP_Text summaryBoard;
        public Light[] hallLights;
        public float hallLightOn = 2.5f;
        public bool skipArrival;
        [Tooltip("Timeline played at DEMONSTRATION READY (facility hologram rises above the hub).")]
        public UnityEngine.Playables.PlayableDirector finaleTimeline;

        const string SaveKey = "ATLAS.FirstShift.progress";
        public Phase phase { get; private set; } = Phase.Arrival;
        int _chapter;
        public int CurrentChapterIndex => _chapter;
        float _startTime;
        XROrigin _origin;

        void Start()
        {
            _origin = FindAnyObjectByType<XROrigin>();
            _startTime = Time.time;
            foreach (var c in chapters)
            {
                c.Completed += OnChapterCompleted;
                c.HintRequested += (s, hint) => echo.SayNow(string.IsNullOrEmpty(hint) ? "Look at the step marked > on the board. You're closer than you think." : hint);
                c.SetUnlocked(false);
            }
            echo.HintPressed += () => { if (phase == Phase.Chapters && _chapter < chapters.Length) chapters[_chapter].RequestHint(); };
            if (chapters[4] && chapters[4].safety)
                chapters[4].safety.Tripped += () => echo.SayNow("<color=#f66>Stop.</color> You crossed the light curtain and the cell halted. Step back outside the yellow fence; you can program everything from out there.");
            hub.ResetPressed += ResetProgress;
            hub.SeatedModeChanged += SetSeated;
            if (assessment) { assessment.gameObject.SetActive(false); assessment.Finished += m => StartCoroutine(Demonstration(m)); }
            SetHallLights(0.4f);

            int saved = PlayerPrefs.GetInt(SaveKey, 0);
            if (saved > 0) StartCoroutine(Resume(saved));
            else if (skipArrival) BeginChapters();
            else StartCoroutine(Arrival());
        }

        // ------------------------------------------------------------------ Scene 00
        IEnumerator Arrival()
        {
            phase = Phase.Arrival;
            if (echoLobbyPoint) echo.FlyTo(echoLobbyPoint.position);
            yield return new WaitForSeconds(1.5f);
            echo.Say("Welcome to ATLAS. You're our newest robotics trainee. I was supposed to introduce you to a fully operational laboratory.", "arrival_1");
            echo.Say("Unfortunately, our final demonstration was suspended. Six systems need verification, and I can't complete it alone.", "arrival_2");
            echo.Say("First, pick up your trainee clipboard from the stand beside you. Squeeze the grip button to hold it. It tracks every verification you complete.", "arrival_clipboard");
            echo.Say("Now let's try your controllers. Point at the glowing <b>START SHIFT</b> button and pull the trigger to select it.", "arrival_3");
            if (startButton) startButton.SetActive(true);
        }

        /// <summary>Called by the START SHIFT button in the lobby.</summary>
        public void StartShift()
        {
            if (phase != Phase.Arrival) return;
            if (startButton) startButton.SetActive(false);
            if (labDoor) labDoor.Open();
            echo.SayNow("Don't worry. You don't need to be an expert. That's why you're here. First, I'll teach you how machines move. Then you'll teach them what to do.", "arrival_4");
            echo.Say("To move, point at the glowing floor pad next to a station and use teleport. In seated mode you never have to stand up.", "arrival_5");
            BeginChapters();
        }

        void BeginChapters()
        {
            phase = Phase.Chapters;
            SetHallLights(1f);
            UnlockChapter(_chapter);
        }

        void UnlockChapter(int i)
        {
            if (i >= chapters.Length) { StartFinale(); return; }
            var c = chapters[i];
            c.SetUnlocked(true);
            echo.FlyTo(c.transform.position + Vector3.up * 2f + c.transform.forward * 0.4f);
            if (!string.IsNullOrEmpty(c.echoIntro)) echo.Say(c.echoIntro, $"ch{i + 1}_intro");
        }

        void OnChapterCompleted(LessonStation s)
        {
            int i = System.Array.IndexOf(chapters, s);
            hub.SetVerified(i, true);
            PlayerPrefs.SetInt(SaveKey, Mathf.Max(PlayerPrefs.GetInt(SaveKey, 0), i + 1));
            PlayerPrefs.Save();
            if (phase != Phase.Chapters) return;
            echo.Say($"<color=#6f6>{s.verificationName} Verification Complete.</color> " + s.echoComplete, $"ch{i + 1}_done");
            if (i == 4)
            {
                echo.Say("Here is what I couldn't tell you this morning. The demonstration wasn't stopped because a robot was broken. Its safety verification was incomplete.", "twist_1");
                echo.Say("The system did exactly what it was supposed to do. It stopped before anyone was put at risk.", "twist_2");
            }
            _chapter = i + 1;
            UnlockChapter(_chapter);
        }

        // ------------------------------------------------------------------ Finale
        void StartFinale()
        {
            phase = Phase.Finale;
            echo.FlyTo(echoHubPoint ? echoHubPoint.position : hub.transform.position + Vector3.up * 2f);
            echo.Say("Six machines. Six completely different ways of interacting with the physical world. And now you've learned the principles behind all of them.", "finale_1");
            echo.Say("The system won't restart because someone pressed a button. It restarts when someone demonstrates understanding. Come to the central console.", "finale_2");
            if (assessment) assessment.gameObject.SetActive(true);
        }

        IEnumerator Demonstration(int mistakes)
        {
            PlayerPrefs.SetInt(SaveKey, 7);
            PlayerPrefs.Save();
            yield return new WaitForSeconds(4f);
            phase = Phase.Demonstration;
            hub.SetDemonstrationReady(true);
            SetHallLights(hallLightOn);
            foreach (var d in demos) if (d) d.Play(true);
            if (finaleTimeline) finaleTimeline.Play();
            echo.Say("<color=#6f6>DEMONSTRATION READY.</color>", "demo_ready");
            echo.Say("When you entered this laboratory, you saw six machines. Now you see joints, calculations, instructions, tools, safety systems and coordinated motion.", "final_1");
            echo.Say("That's the difference between watching robotics and understanding it. Your first shift is complete.", "final_2");
            phase = Phase.Complete;
            WriteSummary(mistakes);
        }

        void WriteSummary(int mistakes)
        {
            if (!summaryBoard) return;
            var mins = (Time.time - _startTime) / 60f;
            var sb = new System.Text.StringBuilder("<size=130%><b>ATLAS · Training Summary</b></size>\n<color=#6f6>Story complete</color>\n\n");
            foreach (var c in chapters) sb.Append($"<color=#6f6>OK</color> {c.verificationName}\n");
            sb.Append($"\nFinal verification mistakes: {mistakes}\nSession time: {mins:0} min");
            sb.Append("\n\n<size=80%><color=#aaa>This module builds awareness of robotics principles. It does not qualify anyone to operate real industrial robots.</color></size>");
            summaryBoard.text = sb.ToString();
            summaryBoard.transform.parent.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ progress & accessibility
        IEnumerator Resume(int saved)
        {
            yield return null; // let stations initialise
            if (labDoor) labDoor.Open(true);
            if (startButton) startButton.SetActive(false);
            for (int i = 0; i < Mathf.Min(saved, chapters.Length); i++) { chapters[i].ForceComplete(); hub.SetVerified(i, true); }
            _chapter = Mathf.Min(saved, chapters.Length);
            phase = Phase.Chapters;
            SetHallLights(1f);
            echo.Say($"Welcome back, trainee. {_chapter} of 6 systems are already verified. Let's continue. (Use Reset progress on the central console to start over.)", "resume");
            if (saved >= 7) { StartFinale(); assessment.gameObject.SetActive(false); StartCoroutine(Demonstration(0)); }
            else UnlockChapter(_chapter);
        }

        void ResetProgress()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        void SetSeated(bool seated)
        {
            if (!_origin) return;
            // Seated learners' eyes are ~45 cm lower; raise the rig so robots and panels stay at a comfortable height.
            var p = _origin.transform.position;
            p.y = seated ? 0.45f : 0f;
            _origin.transform.position = p;
            echo.SayNow(seated ? "Seated mode on. Everything is raised to your eye level; use teleport to move between stations." : "Standing mode on.");
        }

        void SetHallLights(float k)
        {
            foreach (var l in hallLights) if (l) l.intensity = k;
        }
    }
}
