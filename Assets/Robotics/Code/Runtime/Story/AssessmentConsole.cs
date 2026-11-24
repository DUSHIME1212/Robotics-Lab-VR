using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboticsLab.Story
{
    /// <summary>
    /// Final chapter: six scenario questions (one per verified chapter) and a closing reflection question.
    /// Wrong answers get a clue from ECHO and can be retried; mistakes are counted for the training summary.
    /// </summary>
    public class AssessmentConsole : MonoBehaviour
    {
        [Serializable]
        public class Question
        {
            public string topic;
            [TextArea] public string prompt;
            public string[] options;
            public int correct;
            [TextArea] public string clue;
            [TextArea] public string explanation;
        }

        public EchoAssistant echo;
        public Question[] questions = DefaultQuestions();
        public string reflectionPrompt = "If a robot CAN perform a task, does that mean it SHOULD always perform it?";
        public string[] reflectionOptions =
        {
            "Yes. If it is capable, it should do it.",
            "No. It depends on safety, suitability and limitations.",
            "Only if it is faster than a person.",
        };

        public event Action<int> Finished; // mistakes
        public int Mistakes { get; private set; }

        int _index;
        TMP_Text _title, _prompt;
        RectTransform _options;
        bool _reflection;

        void Start() => Build();

        void Build()
        {
            var c = UIFactory.Canvas(transform, "Assessment", new Vector2(820, 640));
            var bg = UIFactory.Background(c, UIFactory.Panel);
            _title = UIFactory.Text(bg, "", 28, UIFactory.Accent, FontStyles.Bold);
            _prompt = UIFactory.Text(bg, "", 28, Color.white);
            _prompt.GetComponent<LayoutElement>().minHeight = 150;
            var go = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(bg, false);
            var v = go.GetComponent<VerticalLayoutGroup>(); v.spacing = 12; v.childControlHeight = v.childControlWidth = true; v.childForceExpandHeight = false;
            _options = (RectTransform)go.transform;
            Show();
        }

        void Show()
        {
            foreach (Transform t in _options) Destroy(t.gameObject);
            if (_index < questions.Length)
            {
                var q = questions[_index];
                _title.text = $"FINAL VERIFICATION · {_index + 1}/{questions.Length} · {q.topic}";
                _prompt.text = q.prompt;
                for (int i = 0; i < q.options.Length; i++)
                {
                    int k = i;
                    var b = UIFactory.Button(_options, q.options[i], () => Answer(k), 24);
                    b.GetComponent<LayoutElement>().minHeight = 70;
                }
            }
            else
            {
                _reflection = true;
                _title.text = "ONE LAST QUESTION";
                _prompt.text = reflectionPrompt;
                for (int i = 0; i < reflectionOptions.Length; i++)
                {
                    int k = i;
                    var b = UIFactory.Button(_options, reflectionOptions[i], () => Reflect(k), 24);
                    b.GetComponent<LayoutElement>().minHeight = 70;
                }
            }
        }

        void Answer(int i)
        {
            var q = questions[_index];
            if (i == q.correct)
            {
                echo?.SayNow("<color=#6f6>Correct.</color> " + q.explanation);
                _index++;
                Show();
            }
            else
            {
                Mistakes++;
                echo?.SayNow("Not quite. " + q.clue);
            }
        }

        void Reflect(int i)
        {
            string reply = i == 1
                ? "Exactly. A robot that can do something is not automatically the right tool, or safe to use in that moment."
                : "Many engineers start there. But capability is only half the question.";
            echo?.SayNow(reply + " Engineering means judging safety, suitability and limitations, not only making a machine move.");
            foreach (Transform t in _options) Destroy(t.gameObject);
            _title.text = "FINAL VERIFICATION COMPLETE";
            _prompt.text = $"<color=#6f6>All six principles verified.</color>\nAttempts with a mistake: {Mistakes}";
            Finished?.Invoke(Mistakes);
        }

        public static Question[] DefaultQuestions() => new[]
        {
            new Question { topic = "MOTION",
                prompt = "The UR17's tool must swing to the other side of the robot at the SAME height. Which single joint should you rotate?",
                options = new[] { "J3 · Elbow", "J1 · Base", "J6 · Wrist 3" }, correct = 1,
                clue = "Which joint turns everything after it around a vertical axis?",
                explanation = "J1 rotates the whole arm about the vertical axis, so the height stays the same." },
            new Question { topic = "PRECISION",
                prompt = "Which target can the Franka FR3 (about 0.9 m reach) actually reach with inverse kinematics?",
                options = new[] { "2 m above its base", "Inside its own base", "0.5 m in front of it at table height" }, correct = 2,
                clue = "IK can only find solutions inside the robot's workspace: think about the reach.",
                explanation = "Points outside the workspace or inside the robot have no IK solution." },
            new Question { topic = "PROGRAMMING",
                prompt = "Which waypoint order makes a safe, repeatable pick-and-place program?",
                options = new[] { "Home -> Approach -> Pick -> Lift -> Place -> Home", "Pick -> Home -> Place", "Place -> Approach -> Pick" }, correct = 0,
                clue = "Start and finish somewhere known, and approach the part before touching it.",
                explanation = "Known start and end poses plus approach/lift moves avoid collisions." },
            new Question { topic = "HANDLING",
                prompt = "A 30 mm machined part must be moved between two fixtures. Which end effector fits?",
                options = new[] { "Welding torch", "Parallel 2-finger gripper", "Paint spray gun" }, correct = 1,
                clue = "Which tool did you use to carry the blue part?",
                explanation = "A parallel gripper holds rigid parts securely between its pads." },
            new Question { topic = "SAFETY",
                prompt = "Which action is a safety VIOLATION in an industrial robot cell?",
                options = new[] { "Programming from outside the fence", "Pressing the E-stop when unsure", "Entering the cell while the program runs" }, correct = 2,
                clue = "What triggered the simulated E-stop in Chapter 5?",
                explanation = "Never enter a fenced cell while the robot can move. Fences and interlocks exist for that reason." },
            new Question { topic = "LOCOMOTION",
                prompt = "In which gait do diagonal leg pairs move together, so two feet always support the body?",
                options = new[] { "Trot", "Sit", "Stand" }, correct = 0,
                clue = "Think of the mode where front-left and hind-right swung together.",
                explanation = "The trot pairs diagonal legs for a stable, coordinated walk." },
        };
    }
}
