using System.IO;
using System.Linq;
using RoboticsLab.Story;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// Builds Assets/Robotics/Scenes/RoboticsLab.unity, the "FIRST SHIFT" experience:
    /// lobby (arrival) -> sliding doors -> circular verification hall with six chapter stations and the hub
    /// -> research wing with a gallery of every other rigged SimReady robot.
    /// </summary>
    public static class RoboticsLabBuilder
    {
        const string Models = "Assets/Robotics/Art/Models/";
        const string Env = "Assets/Robotics/Art/Environment/";
        const string ScenePath = "Assets/Robotics/Scenes/RoboticsLab.unity";
        const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/XR Interaction Simulator/XR Interaction Simulator.prefab";
        // Rigs that import incorrectly (instanced parts scatter); kept on disk, hidden from the gallery until fixed.
        static readonly string[] Excluded = { "Agibot_G2_Omnipicker", "Agibot_G2_Swiftpicker" };
        static readonly string[] Core = { "UR17", "FR3", "CRX10iA", "Robotiq2F85", "ComauN220", "Lite3" };

        const float HallRadius = 6.5f;
        static readonly Vector3 LobbyCenter = new(0, 0, -13f);
        static readonly Vector3 WingCenter = new(11.6f, 0, 13.8f);   // research lab (your laboratory model)
        static readonly Vector3 GalleryCenter = new(0, 0, 13.8f);  // open showroom floor for the 26 gallery robots

        static Material _robotMat, _pedestal, _safety, _target, _handle, _cube, _floor, _wall, _glass, _echoMat, _lamp, _pad, _strip, _curtain;

        [MenuItem("Robotics Lab/Build Lab Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
            Materials();
            EnsureMetaUIKit();

            // ---------------------------------------------------------------- player + XR plumbing
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
            GameObject rig = rigPrefab ? (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab) : null;
            if (rig) { rig.transform.position = LobbyCenter + new Vector3(0, 0, 2.5f); rig.transform.rotation = Quaternion.identity; }
            else Debug.LogWarning("XR Origin prefab not found; add an XR rig manually.");
            if (!Object.FindAnyObjectByType<XRInteractionManager>()) new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            // Desktop fallback: avoids the black Game view when no headset is worn.
            var simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath);
            var fallback = new GameObject("XR Desktop Fallback").AddComponent<XRDesktopFallback>();
            if (simPrefab)
            {
                var sim = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
                sim.SetActive(false);
                fallback.simulator = sim;
            }
            // Lighting: HDRI sky + ambient/reflections (Poly Haven CC0), key/fill rig, post-processing.
            var sun = Object.FindAnyObjectByType<Light>();
            LabLook.Sky();
            LabLook.Lights(sun);
            LabLook.PostProcessing(rig ? rig.GetComponentInChildren<Camera>(true) : null);

            // ---------------------------------------------------------------- architecture
            var world = new GameObject("World").transform;
            var hallFloor = Disc(world, "Hall Floor", Vector3.zero, HallRadius + 0.2f, _floor);
            Teleportable(hallFloor);
            var hallLights = HallWalls(world);
            var lobby = Environment(world, "ATLAS_Lobby", LobbyCenter, 0f);
            // Clear the café furniture and brochure stands so the arrival floor is free for walking/teleporting.
            if (lobby)
                foreach (var t in lobby.GetComponentsInChildren<Transform>(true))
                    if (new[] { "env_01_-_Default.001", "env_Material", "env_phamplet_1", "env_phamplet_2", "env_Pastillas", "env_stand", "env_Vidrio", "env__none" }.Contains(t.name))
                        t.gameObject.SetActive(false);
            var lobbyFloor = Box(world, "Lobby Walk Floor", LobbyCenter + new Vector3(0, -0.01f, 0), new Vector3(11.5f, 0.02f, 11.5f), _floor);
            Teleportable(lobbyFloor);
            Box(world, "Corridor", new Vector3(0, -0.01f, -HallRadius - 0.3f), new Vector3(2.4f, 0.02f, 1.4f), _floor);
            var wing = Environment(world, "ATLAS_Lab", WingCenter, 0f);
            var wingFloor = Box(world, "Wing Walk Floor", WingCenter + new Vector3(0, -0.01f, 0), new Vector3(10.6f, 0.02f, 8f), _floor);
            var showroom = Box(world, "Showroom Floor", GalleryCenter + new Vector3(0, -0.02f, 0), new Vector3(11f, 0.02f, 13f), _floor);
            Teleportable(showroom);
            for (int i = 0; i < 4; i++)
            {
                var sl = new GameObject($"Showroom Light {i + 1}", typeof(Light)).GetComponent<Light>();
                sl.transform.SetParent(world); sl.transform.position = GalleryCenter + new Vector3(i % 2 == 0 ? -2.5f : 2.5f, 3.6f, i < 2 ? -3.5f : 3.5f);
                sl.type = LightType.Point; sl.range = 8; sl.intensity = 1.2f;
            }
            WorldText(world, "Showroom Sign", GalleryCenter + new Vector3(0, 2.9f, -6.2f), Quaternion.identity, new Vector2(3000, 300), 150,
                "ROBOT GALLERY  <size=60%>24 SimReady robots · walk close to see them move</size>").color = new Color(0.7f, 0.9f, 1f);
            Teleportable(wingFloor);

            // Facility sign + doors between lobby and hall.
            var doorRoot = new GameObject("Lab Door").transform;
            doorRoot.SetParent(world); doorRoot.position = new Vector3(0, 0, -HallRadius);
            var leftLeaf = Box(doorRoot, "Door L", doorRoot.position + new Vector3(-0.55f, 1.2f, 0), new Vector3(1.1f, 2.4f, 0.06f), _glass).transform;
            var rightLeaf = Box(doorRoot, "Door R", doorRoot.position + new Vector3(0.55f, 1.2f, 0), new Vector3(1.1f, 2.4f, 0.06f), _glass).transform;
            var door = doorRoot.gameObject.AddComponent<SlidingDoor>();
            door.left = leftLeaf; door.right = rightLeaf; door.travel = 1.05f;
            var sign = WorldText(world, "Facility Sign", new Vector3(0, 2.85f, -HallRadius - 0.1f), Quaternion.identity, new Vector2(2600, 220), 110,
                "ATLAS ROBOTICS LAB · <color=#fb4>SYSTEM VERIFICATION REQUIRED</color>");
            var lobbySignPos = LobbyCenter + new Vector3(-2.5f, 3.62f, -2.0f);
            WorldText(world, "Lobby Sign", lobbySignPos, FaceFrom(lobbySignPos, LobbyCenter + new Vector3(0, 1.6f, 2.5f), true), new Vector2(3400, 560), 210,
                "<b>ATLAS</b>\n<size=45%>ROBOTICS TRAINING FACILITY · 2035</size>").color = new Color(0.7f, 0.9f, 1f);

            // ---------------------------------------------------------------- ECHO
            var echoGo = new GameObject("ECHO");
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.name = "ECHO Orb"; orb.transform.SetParent(echoGo.transform); orb.transform.localScale = Vector3.one * 0.18f;
            orb.transform.position = LobbyCenter + new Vector3(0, 1.8f, 0.5f);
            orb.GetComponent<Renderer>().sharedMaterial = _echoMat;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(orb.transform, false); ring.transform.localScale = new Vector3(1.7f, 0.02f, 1.7f);
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            ring.GetComponent<Renderer>().sharedMaterial = _echoMat;
            var orbLight = orb.AddComponent<Light>(); orbLight.type = LightType.Point; orbLight.color = new Color(0.4f, 0.85f, 1f); orbLight.range = 2.5f; orbLight.intensity = 1.5f;
            var echo = echoGo.AddComponent<EchoAssistant>();
            echo.orb = orb.transform;
            echo.audioSource = echoGo.AddComponent<AudioSource>();
            var echoLobby = Marker(world, "ECHO Lobby Point", LobbyCenter + new Vector3(0.6f, 1.8f, 0.6f));
            var echoHub = Marker(world, "ECHO Hub Point", new Vector3(0, 2.3f, 0));

            // ---------------------------------------------------------------- hub console
            var hubGo = new GameObject("Hub Console");
            var hubBase = Cylinder(hubGo.transform, "Console Base", new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.45f, 1.0f), _pedestal);
            var hub = hubGo.AddComponent<HubConsole>();
            hub.facilitySign = sign;
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * Mathf.PI * 2f;
                var lamp = Cylinder(hubGo.transform, $"Lamp {i + 1}", new Vector3(Mathf.Sin(a) * 0.4f, 0.92f, Mathf.Cos(a) * 0.4f), new Vector3(0.12f, 0.02f, 0.12f), _lamp);
                hub.lamps[i] = lamp.GetComponent<Renderer>();
                var l = new GameObject("Lamp Light", typeof(Light)).GetComponent<Light>();
                l.transform.SetParent(lamp.transform, false); l.transform.localPosition = Vector3.up * 3; l.type = LightType.Point; l.range = 0.6f; l.intensity = 0.8f; l.color = UIFactory.Amber;
                hub.lampLights[i] = l;
            }
            // Hub UI faces a learner standing ~2 m south of the console (the side you enter from).
            var hubEye = new Vector3(0, 1.6f, -2.4f);
            var hubPanel = Marker(hubGo.transform, "Hub Panel Anchor", new Vector3(-0.75f, 1.3f, -0.75f));
            hubPanel.rotation = FaceFrom(hubPanel.position, hubEye);
            hub.panelAnchor = hubPanel;

            var assessGo = new GameObject("Final Verification Console");
            assessGo.transform.SetParent(hubGo.transform); assessGo.transform.position = new Vector3(0.45f, 1.55f, -0.8f);
            assessGo.transform.rotation = FaceFrom(assessGo.transform.position, hubEye);
            var assessment = assessGo.AddComponent<AssessmentConsole>(); assessment.echo = echo;

            var summaryPos = new Vector3(0.45f, 1.55f, -0.85f);
            var summaryCanvas = UIFactory_Canvas(hubGo.transform, "Training Summary", summaryPos, FaceFrom(summaryPos, hubEye), new Vector2(700, 560));
            var summaryText = BoardText(summaryCanvas, 26);
            summaryCanvas.gameObject.SetActive(false);

            // ---------------------------------------------------------------- start button (first interaction)
            var startGo = new GameObject("START SHIFT");
            startGo.transform.position = LobbyCenter + new Vector3(0, 0, 4.2f);
            Cylinder(startGo.transform, "Plinth", startGo.transform.position + new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.5f, 0.5f), _pedestal);
            var startCanvas = UIFactory_Canvas(startGo.transform, "Start Canvas", startGo.transform.position + new Vector3(0, 1.3f, 0), Quaternion.identity, new Vector2(600, 260), true);
            var startBtn = MakeButton(startCanvas, "START SHIFT", new Color(0.1f, 0.6f, 0.9f), 15);

            // ---------------------------------------------------------------- the six chapters
            var stations = new GameObject("Chapters").transform;
            var lessons = new LessonStation[6];
            var demos = new RobotDemo[6];

            // Chapter 01 · UR17
            {
                var s = Slot(stations, 0, "Chapter 01 · UR17");
                var go = Robot(s, "UR17", new Vector3(0, 0.8f, 0.9f), out var art);
                Pedestal(s, new Vector3(0, 0.4f, 0.9f), new Vector3(0.4f, 0.8f, 0.4f));
                var poseTarget = Target(s, Vector3.zero);
                poseTarget.position = PoseEndEffector(art, new[] { 45f, -50f, 80f, 0f, 0f, 0f });
                var pendant = Pendant(s, art, null, null, null, "UR17 · Jog Joints");
                lessons[0] = Station(s, 1, "The Language of Motion", "Motion",
                    "A serial arm is a chain of links joined by joints. Each joint adds one degree of freedom (DOF). The UR17 has 6. Joint angles determine where the tool is: that is FORWARD KINEMATICS.",
                    "Ready for your first challenge? Let's start with the simplest question in robotics: how does a robot know where its hand is? Your arm has joints. This one does too. The difference is that this robot needs numbers to describe every movement.",
                    "Every position of that tool came from six numbers. Now you speak the language of motion.",
                    art, pendant, null, null, null, new[]
                    {
                        Step("Rotate the base: set J1 to +90°. Watch the whole arm swing: J1 moves everything after it.", LessonStep.Check.JointAngle, 0, 90, 6, "Point at the J1 slider, hold the trigger and drag right until the readout shows about 90°."),
                        Step("Raise the shoulder: set J2 to -60°.", LessonStep.Check.JointAngle, 1, -60, 8, "J2 is the second slider. Drag it left of centre."),
                        Step("Bend the elbow: set J3 to +90° and watch the tool position readout change.", LessonStep.Check.JointAngle, 2, 90, 8, "J3 is the elbow. The X/Y/Z readout is forward kinematics, computed from your joint angles."),
                        StepTarget("Requested tool pose: move the tool into the green sphere using only the joint sliders.", poseTarget, 0.07f, "Try J1 near 45°, then adjust J2 and J3 together. The base sets the direction, shoulder and elbow set the reach."),
                    });
                demos[0] = Demo(go, art);
            }
            // Chapter 02 · FR3
            {
                var s = Slot(stations, 1, "Chapter 02 · Franka FR3");
                var go = Robot(s, "FR3", new Vector3(0, 0.8f, 0.9f), out var art);
                Pedestal(s, new Vector3(0, 0.4f, 0.9f), new Vector3(0.35f, 0.8f, 0.35f));
                var ik = go.AddComponent<CCDIKSolver>();
                var handle = Handle(s, art.endEffector ? art.endEffector.position : s.position + Vector3.up * 1.5f);
                ik.target = handle; ik.solve = false;
                var target = Target(s, new Vector3(0.35f, 1.15f, 0.65f));
                var pendant = Pendant(s, art, ik, null, null, "FR3 · Inverse Kinematics");
                lessons[1] = Station(s, 2, "One Target, Many Solutions", "Precision",
                    "Inverse kinematics (IK) works backwards: choose where the tool must go, and the solver finds the joint angles. With 7 joints the FR3 is REDUNDANT: many elbow poses reach the same point.",
                    "Before, you chose the joint angles and observed the result. Now you choose the result, and the robot must work out the angles.",
                    "You gave the robot a destination, not instructions, and it found its own way there.",
                    art, pendant, null, null, null, new[]
                    {
                        Step("Press 'IK: drag target' on the pendant to switch the solver on.", LessonStep.Check.Timer, 0, 3, 0, "The IK button is on the panel to the right of the robot."),
                        StepTarget("Grab the orange handle and guide the tool into the green sphere.", target, 0.06f, "Point at the orange ball, squeeze the grip button and move your hand. The arm follows."),
                        Step("Drag the handle far behind the robot and watch the IK error grow: some targets have NO solution.", LessonStep.Check.Timer, 0, 10, 0, "Reach is limited. The workspace is roughly a sphere about 0.9 m around the shoulder."),
                    });
                demos[1] = Demo(go, art);
            }
            // Chapter 03 · CRX-10iA
            {
                var s = Slot(stations, 2, "Chapter 03 · FANUC CRX-10iA");
                var go = Robot(s, "CRX10iA", new Vector3(0, 0.8f, 0.9f), out var art);
                Pedestal(s, new Vector3(0, 0.4f, 0.9f), new Vector3(0.4f, 0.8f, 0.4f));
                var pendant = Pendant(s, art, null, null, null, "CRX-10iA · Teach Pendant");
                lessons[2] = Station(s, 3, "Teach the Machine", "Programming",
                    "Industrial robots are programmed by TEACHING: jog to a pose, record it as a waypoint, repeat, then run. MoveJ moves every joint at once to the next recorded pose.",
                    "A robot doesn't understand what you want. It follows what you program. So let's give it instructions it can repeat.",
                    "That sequence will now run exactly the same way a thousand times. That's what programming a robot means.",
                    art, pendant, null, null, null, new[]
                    {
                        Step("Jog the arm to a 'pick' pose and press Record (P1).", LessonStep.Check.Timer, 0, 3, 0, "Move a few sliders, then press Record."),
                        Step("Record an 'approach' pose and a 'place' pose (P2, P3), then press Run.", LessonStep.Check.ProgramRun, 0, 3, 0, "You need three recorded waypoints before Run. The readout shows how many you have."),
                        Step("Press Home. Programs should start and end in a known, safe pose.", LessonStep.Check.JointAngle, 1, 0, 2, "Home sets every joint back to 0°."),
                    });
                demos[2] = Demo(go, art);
            }
            // Chapter 04 · Robotiq 2F-85
            GripperController grip;
            {
                var s = Slot(stations, 3, "Chapter 04 · Robotiq 2F-85");
                Pedestal(s, new Vector3(0, 0.4f, 0.9f), new Vector3(1.2f, 0.8f, 0.6f));
                var holder = new GameObject("Gripper (grab me)");
                holder.transform.SetParent(s, false);
                holder.transform.localPosition = new Vector3(-0.35f, 1.15f, 0.9f);
                holder.transform.localRotation = Quaternion.Euler(180, 0, 0);
                var rb = holder.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                var box = holder.AddComponent<BoxCollider>(); box.size = new Vector3(0.09f, 0.12f, 0.09f); box.center = new Vector3(0, 0.05f, 0);
                var grab = holder.AddComponent<XRGrabInteractable>(); grab.throwOnDetach = false;
                var go = Robot(holder.transform, "Robotiq2F85", Vector3.zero, out var art);
                grip = go.AddComponent<GripperController>();
                var center = new GameObject("GraspCenter").transform; center.SetParent(holder.transform, false); center.localPosition = new Vector3(0, 0.15f, 0);
                grip.graspCenter = center;
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = "Blue Component"; part.transform.SetParent(s, false);
                part.transform.localPosition = new Vector3(-0.35f, 0.83f, 0.9f); part.transform.localScale = Vector3.one * 0.045f;
                part.GetComponent<Renderer>().sharedMaterial = _cube;
                var partRb = part.AddComponent<Rigidbody>(); partRb.mass = 0.2f;
                var bin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bin.name = "Destination Zone"; bin.transform.SetParent(s, false);
                bin.transform.localPosition = new Vector3(0.35f, 0.86f, 0.9f); bin.transform.localScale = new Vector3(0.2f, 0.12f, 0.2f);
                bin.GetComponent<Renderer>().sharedMaterial = _target;
                bin.GetComponent<Collider>().isTrigger = true;
                var pendant = Pendant(s, art, null, grip, null, "2F-85 · Gripper");
                lessons[3] = Station(s, 4, "The Art of Holding", "Handling",
                    "The end effector is the robot's 'hand'. The 2F-85 is an adaptive two-finger gripper: one motor drives a linkage that keeps the pads parallel over an 85 mm stroke. (The linkage is approximated in this simulation.)",
                    "An arm can reach a thousand places. But without the right tool, it can't do much when it gets there.",
                    "Reach plus the right tool: that's how robots actually change the world around them.",
                    null, pendant, grip, null, null, new[]
                    {
                        Step("Press Close, then Open, and watch the parallel linkage.", LessonStep.Check.Timer, 0, 4, 0, "The Open and Close buttons are on the gripper panel."),
                        StepZone("Grab the gripper, pick up the blue component and release it in the green zone.", bin.GetComponent<Collider>(), partRb,
                            "Hold the gripper above the blue cube with the fingers around it, press Close, carry it over the green box, then press Open."),
                    });
                demos[3] = go.AddComponent<RobotDemo>(); demos[3].gripper = grip;
            }
            // Chapter 05 · Comau N-220
            {
                var s = Slot(stations, 4, "Chapter 05 · Comau N-220");
                var go = Robot(s, "ComauN220", new Vector3(0, 0, -1.3f), out var art);
                go.transform.localScale = Vector3.one * 0.5f;
                var curtains = new System.Collections.Generic.List<Renderer>();
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f * Mathf.Deg2Rad;
                    var f = Box(s, "Safety Fence", Vector3.zero, new Vector3(2f, 1f, 0.03f), _safety);
                    f.transform.localPosition = new Vector3(Mathf.Sin(a) * 1.0f, 0.5f, -1.3f + Mathf.Cos(a) * 1.0f);
                    f.transform.localRotation = Quaternion.Euler(0, i * 90f, 0);
                    // Visible light curtain above the fence (EnergyField shader, turns red on a violation).
                    var c = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    c.name = "Light Curtain Field"; Object.DestroyImmediate(c.GetComponent<Collider>());
                    c.transform.SetParent(s, false);
                    c.transform.localPosition = new Vector3(Mathf.Sin(a) * 1.0f, 1.8f, -1.3f + Mathf.Cos(a) * 1.0f);
                    c.transform.localRotation = Quaternion.Euler(0, i * 90f, 0);
                    c.transform.localScale = new Vector3(2f, 1.6f, 1f);
                    var cr = c.GetComponent<Renderer>(); cr.sharedMaterial = _curtain;
                    cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    curtains.Add(cr);
                }
                var monitor = new GameObject("Light Curtain").AddComponent<SafetyFenceMonitor>();
                monitor.curtains = curtains.ToArray();
                monitor.transform.SetParent(s, false); monitor.transform.localPosition = new Vector3(0, 1.2f, -1.3f);
                monitor.cell = new Bounds(Vector3.zero, new Vector3(2f, 2.6f, 2f));
                monitor.robot = art;
                var beacon = Cylinder(s, "Status Beacon", Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), _lamp);
                beacon.transform.localPosition = new Vector3(1.05f, 1.15f, -0.3f);
                monitor.beaconRenderer = beacon.GetComponent<Renderer>();
                monitor.beacon = beacon.AddComponent<Light>(); monitor.beacon.range = 1.5f;
                var pendant = Pendant(s, art, null, null, null, "N-220 · Cell Controller");
                lessons[4] = Station(s, 5, "The Boundary", "Safety",
                    "Heavy industrial robots work inside FENCED CELLS with interlocks, light curtains and emergency stops. Program from OUTSIDE. Crossing the light curtain triggers a simulated E-stop. (Robot shown at 50% scale.)",
                    "You've learned how to make machines move. Now comes one of the most important things an engineer can learn: when a machine must not move.",
                    "You programmed a 220 kg robot without ever stepping inside its cell.",
                    art, pendant, null, null, monitor, new[]
                    {
                        Step("From OUTSIDE the yellow fence, teach two waypoints and press Run.", LessonStep.Check.ProgramRun, 0, 2, 0, "Stay on the teleport pad. Everything you need is on the cell controller panel."),
                        Step("Joint limits stop self-collision. Note J3's uneven limits, then rotate J1 to -90°.", LessonStep.Check.JointAngle, 0, -90, 8, "J1 is the first slider. Drag it left to -90°."),
                    });
                demos[4] = Demo(go, art);
            }
            // Chapter 06 · Lite3
            {
                var s = Slot(stations, 5, "Chapter 06 · Deep Robotics Lite3");
                var go = Robot(s, "Lite3", new Vector3(0, 0.35f, 1.2f), out var art);
                var gait = go.AddComponent<QuadrupedGait>(); gait.moveBody = false;
                var pendant = Pendant(s, art, null, null, gait, "Lite3 · Gait Controller");
                lessons[5] = Station(s, 6, "Learning to Walk", "Locomotion",
                    "Each Lite3 leg has 3 joints: 12 DOF in all. In a TROT, diagonal leg pairs swing together so two feet always support the body. Frequency and stride set the rhythm. (This walks in place: it shows gait coordination, not balance or terrain.)",
                    "We've spent all day teaching arms how to move. But what if the robot itself needs to move through the world?",
                    "Twelve joints, one rhythm. Coordination is what turns joints into walking.",
                    art, pendant, null, gait, null, new[]
                    {
                        Step("Press Trot and watch front-left + hind-right swing together.", LessonStep.Check.GaitMode, 0, 1, 0, "Trot is the middle button on the gait panel."),
                        Step("While trotting, set the gait frequency to about 1.0 Hz.", LessonStep.Check.GaitTuned, 0, 1.0f, 0.15f, "Drag the frequency slider left. Slower steps are easier to watch."),
                        Step("Press Sit to end the demonstration.", LessonStep.Check.GaitMode, 0, 2, 0, "Sit is the right-hand gait button."),
                    });
                demos[5] = go.AddComponent<RobotDemo>(); demos[5].robot = art; demos[5].gait = gait;
            }

            // ---------------------------------------------------------------- story manager
            // Manufacturer spec sheets (researched online; see Assets/Robotics/Data/robot_specs.json).
            RobotSpecs.Reload();
            var chapterRobotIds = new[] { "UR17", "FR3", "CRX10iA", "Robotiq2F85", "ComauN220", "Lite3" };
            for (int i = 0; i < lessons.Length; i++)
                lessons[i].specs = RobotSpecs.Name(chapterRobotIds[i], chapterRobotIds[i]) + ":  " + RobotSpecs.Line(chapterRobotIds[i]);

            var story = new GameObject("Lab Story Manager").AddComponent<LabStoryManager>();
            story.echo = echo; story.hub = hub; story.chapters = lessons; story.demos = demos;
            story.assessment = assessment; story.labDoor = door; story.startButton = startGo;
            story.echoLobbyPoint = echoLobby; story.echoHubPoint = echoHub; story.summaryBoard = summaryText;
            story.hallLights = hallLights;
            Clipboard(world, story);
            story.finaleTimeline = LabExtras.FinaleTimeline(hubGo.transform, _echoMat);   // Timeline
            LabExtras.PendantCables(stations, _pedestal);                                    // cables (Obi Rope stand-in)
            UnityEventTools.AddPersistentListener(startBtn.onClick, story.StartShift);

            // ---------------------------------------------------------------- research wing gallery
            Gallery(world);
            var headRigs = LabExtras.HeadTracking(GameObject.Find("Robot Gallery (Research Wing)").transform); // Animation Rigging
            Debug.Log($"[RoboticsLab] Head-tracking rigs: {headRigs}");

            // Box-projected reflection probes per zone (refresh once on load; no baking required).
            var probes = new GameObject("Reflection Probes").transform;
            LabLook.ReflectionProbe(probes, "Probe Hall", Vector3.zero, new Vector3(13.5f, 4.5f, 13.5f));
            LabLook.ReflectionProbe(probes, "Probe Lobby", LobbyCenter, new Vector3(12f, 4.5f, 12f));
            LabLook.ReflectionProbe(probes, "Probe Gallery", GalleryCenter, new Vector3(11f, 4.5f, 13f));
            LabLook.ReflectionProbe(probes, "Probe Research Lab", WingCenter, new Vector3(10.8f, 3.2f, 8.2f));

            // One connected building around lobby, hall, gallery and research lab.
            LabFacility.Build(world, _wall,
                LabLook.Pbr("ExteriorGround", "hangar_concrete_floor", new Color(0.7f, 0.72f, 0.75f), new Vector2(30, 30), 0.4f),
                Mat("FacadeTrim", new Color(0.12f, 0.13f, 0.15f)),
                Mat("Roof", new Color(0.55f, 0.57f, 0.6f)), HallRadius);

            // Baked GI: static flags, area lights, light probes, lightmapper settings (bake via Robotics Lab ▸ Lighting).
            LabBakedLighting.Setup(world, LobbyCenter, GalleryCenter, WingCenter, HallRadius);

            Directory.CreateDirectory("Assets/Robotics/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(x => x.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[RoboticsLab] Built " + ScenePath);
        }

        // ================================================================== layout helpers
        static Transform Slot(Transform parent, int i, string name)
        {
            float a = Mathf.Lerp(-150f, 150f, i / 5f) * Mathf.Deg2Rad;
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            t.position = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 3.2f;
            t.rotation = Quaternion.LookRotation(-t.position.normalized);
            // Teleport pad where the learner stands to work at this station.
            var pad = Cylinder(t, "Teleport Pad", Vector3.zero, new Vector3(0.7f, 0.01f, 0.7f), _pad);
            pad.transform.localPosition = new Vector3(0.1f, 0.01f, 1.95f);
            var anchor = pad.AddComponent<TeleportationAnchor>();
            var at = new GameObject("Anchor").transform; at.SetParent(pad.transform, false);
            at.rotation = Quaternion.LookRotation(-t.forward);
            anchor.teleportAnchorTransform = at;
            SetTeleportLayer(anchor);
            // Spotlight that turns on when the chapter unlocks.
            var spot = new GameObject("Station Spotlight", typeof(Light)).GetComponent<Light>();
            spot.transform.SetParent(t, false); spot.transform.localPosition = new Vector3(0, 3.6f, 1.2f);
            spot.transform.localRotation = Quaternion.Euler(80, 0, 0);
            spot.type = LightType.Spot; spot.spotAngle = 60; spot.range = 6; spot.intensity = 6; spot.color = new Color(1f, 0.96f, 0.9f);
            return t;
        }

        static Light[] HallWalls(Transform world)
        {
            var walls = new GameObject("Hall Walls").transform; walls.SetParent(world);
            int n = 36;
            for (int i = 0; i < n; i++)
            {
                float deg = i * 360f / n;
                if (Mathf.Abs(Mathf.DeltaAngle(deg, 180)) < 12f) continue; // door to lobby
                if (Mathf.Abs(Mathf.DeltaAngle(deg, 0)) < 14f) continue;   // opening to research wing
                float a = deg * Mathf.Deg2Rad;
                var p = Box(walls, "Wall Panel", new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * HallRadius + Vector3.up * 1.6f,
                    new Vector3(2f * Mathf.PI * HallRadius / n + 0.02f, 3.2f, 0.15f), _wall);
                p.transform.rotation = Quaternion.Euler(0, deg, 0);
                var strip = Box(walls, "Light Strip", p.transform.position + Vector3.up * 1.2f - p.transform.forward * 0.09f,
                    new Vector3(1f, 0.04f, 0.02f), _strip);
                strip.transform.rotation = p.transform.rotation;
                Object.DestroyImmediate(strip.GetComponent<Collider>());
            }
            var lights = new Light[4];
            for (int i = 0; i < 4; i++)
            {
                var l = new GameObject($"Hall Light {i + 1}", typeof(Light)).GetComponent<Light>();
                l.transform.SetParent(walls); l.transform.position = new Vector3(i % 2 == 0 ? -2.5f : 2.5f, 3.4f, i < 2 ? -2.5f : 2.5f);
                l.type = LightType.Point; l.range = 7; l.intensity = 1; l.color = new Color(0.85f, 0.92f, 1f);
                lights[i] = l;
            }
            return lights;
        }

        static GameObject Environment(Transform parent, string name, Vector3 pos, float yaw)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Env}{name}.fbx");
            if (!model) { Debug.LogWarning($"Missing environment {name}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.isStatic = true;
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        static void Gallery(Transform world)
        {
            var root = new GameObject("Robot Gallery (Research Wing)").transform; root.SetParent(world);
            var dirs = Directory.GetDirectories(Models).Select(Path.GetFileName).Where(d => !Core.Contains(d) && !Excluded.Contains(d)).OrderBy(d => d).ToList();
            var info = LoadGalleryInfo();
            int cols = 5; float dx = 1.9f, dz = 2.0f;
            for (int i = 0; i < dirs.Count; i++)
            {
                string n = dirs[i];
                if (!File.Exists($"{Models}{n}/{n}.fbx")) continue;
                int r = i / cols, c = i % cols;
                var slot = new GameObject(n).transform; slot.SetParent(root);
                slot.position = GalleryCenter + new Vector3((c - (cols - 1) / 2f) * dx, 0, -5f + r * dz);
                slot.rotation = Quaternion.Euler(0, 180, 0);
                var plinth = Cylinder(slot, "Plinth", slot.position + Vector3.up * 0.05f, new Vector3(0.9f, 0.05f, 0.9f), _pedestal);
                var go = Robot(slot, n, new Vector3(0, 0.1f, 0), out var art);
                go.transform.localRotation = Quaternion.identity; // face visitors arriving from the hall
                FitHeight(go, 1.7f);
                SitOnPlinth(go, slot.position.y + 0.1f, 0.5f);
                var d = go.AddComponent<RobotDemo>(); d.robot = art; d.amplitude = 0.15f; d.speed = 0.25f;
                d.Play(false);
                info.TryGetValue(n, out var text);
                var plaquePos = slot.position + slot.forward * 0.6f + Vector3.up * 0.35f;
                WorldText(slot, "Plaque", plaquePos, FaceFrom(plaquePos, slot.position + slot.forward * 1.6f + Vector3.up * 1.6f),
                    new Vector2(1100, 420), 56, $"<b>{RobotSpecs.Name(n, n)}</b>\n<size=70%>{(RobotSpecs.Type(n) != "" ? RobotSpecs.Type(n) : text ?? "")}\n{RobotSpecs.Line(n, art.Count)}</size>");
                // Gallery robots idle gently so learners can see the joints move.
                EditorAutoPlay(d);
            }
        }

        static System.Collections.Generic.Dictionary<string, string> LoadGalleryInfo()
        {
            var d = new System.Collections.Generic.Dictionary<string, string>();
            var path = "Assets/Robotics/Art/Models/gallery.tsv";
            if (!File.Exists(path)) return d;
            foreach (var line in File.ReadAllLines(path))
            {
                var p = line.Split('\t');
                if (p.Length >= 2) d[p[0]] = p[1];
            }
            return d;
        }

        static void EditorAutoPlay(RobotDemo d) { d.gameObject.AddComponent<GalleryIdle>().demo = d; }

        /// <summary>Enlarge tiny items (grippers) for display and rest the model's lowest point on the plinth
        /// (floating-base robots such as humanoids have their origin at the pelvis).</summary>
        static void SitOnPlinth(GameObject go, float topY, float minSize)
        {
            Bounds B()
            {
                var rs = go.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                return b;
            }
            var b0 = B();
            float m = Mathf.Max(b0.size.x, b0.size.y, b0.size.z);
            if (m > 0 && m < minSize) go.transform.localScale *= minSize / m;
            var b1 = B();
            go.transform.position += Vector3.up * (topY - b1.min.y);
        }

        static void FitHeight(GameObject go, float maxH)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float h = Mathf.Max(b.size.y, b.size.x * 0.8f, b.size.z * 0.8f);
            if (h > maxH) go.transform.localScale *= maxH / h;
        }

        static Vector3 PoseEndEffector(RobotArticulation art, float[] pose)
        {
            for (int i = 0; i < pose.Length && i < art.Count; i++) art.joints[i].SetAngle(pose[i]);
            var p = art.endEffector.position;
            for (int i = 0; i < art.Count; i++) art.joints[i].SetAngle(0);
            return p;
        }

        // ================================================================== object helpers
        static GameObject Robot(Transform parent, string name, Vector3 localPos, out RobotArticulation art)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}{name}/{name}.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterials.All(m => m == null || m.name.StartsWith("Default"))) r.sharedMaterial = _robotMat;
            art = go.AddComponent<RobotArticulation>();
            art.jointDefinition = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Models}{name}/{name}.joints.json");
            art.Build();
            return go;
        }

        static RobotDemo Demo(GameObject go, RobotArticulation art) { var d = go.AddComponent<RobotDemo>(); d.robot = art; return d; }

        static void Pedestal(Transform parent, Vector3 pos, Vector3 size)
        {
            var p = Box(parent, "Pedestal", Vector3.zero, size, _pedestal);
            p.transform.localPosition = pos;
        }

        static Transform Handle(Transform parent, Vector3 worldPos)
        {
            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.name = "IK Handle (grab me)"; h.transform.SetParent(parent, true);
            h.transform.position = worldPos; h.transform.localScale = Vector3.one * 0.07f;
            h.GetComponent<Renderer>().sharedMaterial = _handle;
            var rb = h.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var g = h.AddComponent<XRGrabInteractable>(); g.throwOnDetach = false;
            return h.transform;
        }

        static Transform Target(Transform parent, Vector3 localPos)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            t.name = "Reach Target"; t.transform.SetParent(parent, false);
            t.transform.localPosition = localPos; t.transform.localScale = Vector3.one * 0.1f;
            t.GetComponent<Renderer>().sharedMaterial = _target;
            Object.DestroyImmediate(t.GetComponent<Collider>());
            return t.transform;
        }

        /// <summary>Rotation that makes a world-space canvas/TMP readable from <paramref name="eye"/> (UI is read looking along its +Z).</summary>
        static Quaternion FaceFrom(Vector3 pos, Vector3 eye, bool upright = false)
        {
            var d = pos - eye;
            if (upright) d.y = 0;
            return Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        static Vector3 StationEye(Transform station) =>
            station.TransformPoint(new Vector3(0.1f, 0, 1.95f)) + Vector3.up * 1.6f;

        static TeachPendant Pendant(Transform parent, RobotArticulation art, CCDIKSolver ik, GripperController grip, QuadrupedGait gait, string title)
        {
            var go = new GameObject("Teach Pendant");
            go.transform.SetParent(parent, false);
            // Learner stands on the pad looking at the robot (local -Z); their right hand side is local -X.
            go.transform.localPosition = new Vector3(-0.7f, 1.2f, 1.35f);
            go.transform.rotation = FaceFrom(go.transform.position, StationEye(parent));
            var p = go.AddComponent<TeachPendant>();
            p.robot = grip ? grip.GetComponent<RobotArticulation>() : art; p.ik = ik; p.gripper = grip; p.gait = gait; p.title = title;
            if (grip || gait) p.size = new Vector2(520, 380);
            return p;
        }

        static LessonStation Station(Transform parent, int chapter, string title, string verification, string concept, string intro, string complete,
            RobotArticulation art, TeachPendant pendant, GripperController grip, QuadrupedGait gait, SafetyFenceMonitor safety, LessonStep[] steps)
        {
            var canvas = UIFactory_Canvas(parent, "Lesson Board", Vector3.zero, Quaternion.identity, new Vector2(720, 700));
            canvas.localPosition = new Vector3(0.95f, 1.55f, 0.55f); // learner's left
            canvas.rotation = FaceFrom(canvas.position, StationEye(parent), true);
            var tmp = BoardText(canvas, 24);
            var ls = parent.gameObject.AddComponent<LessonStation>();
            ls.chapter = chapter; ls.lessonTitle = title; ls.verificationName = verification;
            ls.concept = concept; ls.echoIntro = intro; ls.echoComplete = complete;
            ls.robot = art; ls.pendant = pendant; ls.gripper = grip; ls.gait = gait; ls.safety = safety;
            ls.steps = steps; ls.board = tmp;
            ls.spotlight = parent.GetComponentsInChildren<Light>().FirstOrDefault(l => l.name == "Station Spotlight");
            return ls;
        }

        static RectTransform UIFactory_Canvas(Transform parent, string name, Vector3 worldPos, Quaternion rot, Vector2 size, bool interactive = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            if (interactive) go.AddComponent<TrackedDeviceGraphicRaycaster>();
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size; rt.localScale = Vector3.one * 0.001f;
            rt.position = worldPos; rt.rotation = rot;
            var c = go.GetComponent<Canvas>();
            c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
            var bg = new GameObject("Backplate", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bg.transform.SetParent(rt, false);
            UIFactory.StyleBackplate(bg.GetComponent<UnityEngine.UI.Image>(), UIFactory.Panel); // Meta UI Set theme backplate
            Fill((RectTransform)bg.transform, 0);
            return rt;
        }

        static TMP_Text BoardText(RectTransform canvas, float size)
        {
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvas, false);
            Fill((RectTransform)textGo.transform, 26);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            UIFactory.ApplyFont(tmp, FontStyles.Normal);
            tmp.fontSize = size; tmp.color = UIFactory.TextPrimary; tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>A large Meta UI Set primary button filling a canvas.</summary>
        static UnityEngine.UI.Button MakeButton(RectTransform canvas, string label, Color c, float fontSize)
        {
            var plate = UIFactory.Background(canvas, UIFactory.Panel, 28, 0);
            plate.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childForceExpandHeight = true;
            var b = UIFactory.Button(plate, label, null, ButtonStyle.Primary, fontSize);
            var le = b.GetComponentInParent<UnityEngine.UI.LayoutElement>();
            if (le) { le.flexibleHeight = 1; le.minHeight = 120; }
            return b;
        }

        // ================================================================== Meta UI Kit
        const string UISetRoot = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/";

        /// <summary>Creates/updates Resources/MetaUIKitLibrary.asset pointing at the Meta Interaction SDK UI Set.</summary>
        static void EnsureMetaUIKit()
        {
            const string path = "Assets/Robotics/Resources/MetaUIKitLibrary.asset";
            Directory.CreateDirectory("Assets/Robotics/Resources");
            var lib = AssetDatabase.LoadAssetAtPath<MetaUIKitLibrary>(path);
            if (!lib) { lib = ScriptableObject.CreateInstance<MetaUIKitLibrary>(); AssetDatabase.CreateAsset(lib, path); }
            T Load<T>(string rel) where T : Object
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(UISetRoot + rel);
                if (!a) Debug.LogWarning("[RoboticsLab] Meta UI Set asset missing: " + rel);
                return a;
            }
            lib.theme = Load<Oculus.Interaction.UITheme>("Themes/UIThemeQuest_Dark.asset");
            lib.primaryButton = Load<GameObject>("Prefabs/Button/UnityUIButtonBased/PrimaryButton_IconAndLabel_UnityUIButton.prefab");
            lib.secondaryButton = Load<GameObject>("Prefabs/Button/UnityUIButtonBased/SecondaryButton_IconAndLabel_UnityUIButton.prefab");
            lib.destructiveButton = Load<GameObject>("Prefabs/Button/UnityUIButtonBased/DestructiveButton_IconAndLabel_UnityUIButton.prefab");
            lib.borderlessButton = Load<GameObject>("Prefabs/Button/UnityUIButtonBased/BorderlessButton_IconAndLabel_UnityUIButton.prefab");
            lib.slider = Load<GameObject>("Prefabs/Slider/MediumSlider.prefab");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            MetaUIKitLibrary.ClearCache();
        }

        // ================================================================== trainee clipboard (user-supplied prop)
        static void Clipboard(Transform world, LabStoryManager story)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Robotics/Art/Props/Clipboard/Clipboard.fbx");
            if (!model) { Debug.LogWarning("Clipboard model missing"); return; }

            // Stand beside the START plinth.
            var standPos = LobbyCenter + new Vector3(0.85f, 0, 3.9f);
            var stand = Box(world, "Clipboard Stand", standPos + Vector3.up * 0.45f, new Vector3(0.4f, 0.9f, 0.35f), _pedestal);

            var root = new GameObject("Trainee Clipboard (grab me)");
            root.transform.position = standPos + new Vector3(0, 0.98f, 0); // clears the stand once tilted
            root.transform.rotation = Quaternion.identity; // final tilt applied at the end
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            mesh.transform.localPosition = Vector3.zero; mesh.transform.localRotation = Quaternion.identity;
            var mat = ClipboardMaterial();
            foreach (var r in mesh.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

            // Normalise orientation: paper face -> +Y, clip -> +Z (the model may arrive standing upright).
            Bounds WorldBounds(System.Func<Renderer, bool> pick)
            {
                var rs = mesh.GetComponentsInChildren<Renderer>().Where(pick).ToArray();
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); return bb;
            }
            bool IsPaper(Renderer r) => r.name.ToLower().Contains("paper");
            bool IsBoard(Renderer r) => !IsPaper(r) && !r.name.ToLower().Contains("pen");
            {
                var board = WorldBounds(IsBoard); var paperB = WorldBounds(IsPaper);
                var ext = board.extents;
                int thin = ext.x < ext.y ? (ext.x < ext.z ? 0 : 2) : (ext.y < ext.z ? 1 : 2);
                var face = Vector3.zero; face[thin] = Mathf.Sign((paperB.center - board.center)[thin]); // paper sits on the front face
                mesh.transform.rotation = Quaternion.FromToRotation(face, Vector3.up) * mesh.transform.rotation;
                board = WorldBounds(IsBoard); paperB = WorldBounds(IsPaper);
                int along = board.extents.x > board.extents.z ? 0 : 2;          // long axis of the board
                float clipSide = (paperB.center - board.center)[along];        // paper is offset toward the clip end
                var clipDir = along == 0 ? new Vector3(Mathf.Sign(clipSide), 0, 0) : new Vector3(0, 0, Mathf.Sign(clipSide));
                mesh.transform.rotation = Quaternion.FromToRotation(clipDir, Vector3.forward) * mesh.transform.rotation;
                var all = WorldBounds(_ => true);
                mesh.transform.position += root.transform.position - new Vector3(all.center.x, all.min.y, all.center.z);
            }

            // Physics + XRI grab. Attach so the paper faces the learner when held.
            var rb = root.AddComponent<Rigidbody>(); rb.mass = 0.4f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in mesh.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size + new Vector3(0, 0.004f, 0);
            var attach = new GameObject("Hold Point").transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = new Vector3(0, 0, -0.12f);                                   // near the bottom edge
            attach.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward) * Quaternion.Euler(-25, 0, 0);
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach; grab.throwOnDetach = false; grab.useDynamicAttach = false;

            // Paper canvas: find the "paper" mesh and lay a canvas on its surface.
            var paper = mesh.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.ToLower().Contains("paper"));
            var pb = paper ? paper.bounds : bounds;
            var canvas = UIFactory.Canvas(root.transform, "Paper Canvas", new Vector2(200, 240), 0.001f, false);
            // Paper surface height from the actual mesh vertices (bounds are too coarse), +2 mm to avoid z-fighting.
            float surface = float.MinValue;
            if (paper)
                foreach (var v in paper.GetComponent<MeshFilter>().sharedMesh.vertices)
                    surface = Mathf.Max(surface, root.transform.InverseTransformPoint(paper.transform.TransformPoint(v)).y);
            var top = root.transform.InverseTransformPoint(pb.center);
            top.y = (surface > float.MinValue ? surface : top.y) + 0.002f;
            canvas.localPosition = top;
            // Loose pen would lie across the checklist text.
            foreach (var r in mesh.GetComponentsInChildren<Renderer>()) if (r.name.ToLower().Contains("pen")) r.gameObject.SetActive(false);
            canvas.localRotation = Quaternion.Euler(90, 0, 0);
            var paperSize = root.transform.InverseTransformVector(pb.size);
            canvas.sizeDelta = new Vector2(Mathf.Abs(paperSize.x) * 1000f * 0.9f, Mathf.Abs(paperSize.z) * 1000f * 0.78f);

            // Tilt the board up toward the learner arriving from -Z (clip end raised).
            root.transform.rotation = Quaternion.Euler(-25, 0, 0);

            var cb = root.AddComponent<TraineeClipboard>();
            cb.story = story; cb.paperCanvas = canvas;
        }

        static Material ClipboardMaterial()
        {
            const string dir = "Assets/Robotics/Art/Props/Clipboard/";
            var m = AssetDatabase.LoadAssetAtPath<Material>(dir + "Clipboard.mat");
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, dir + "Clipboard.mat"); }
            Texture2D Tex(string n, bool normal = false)
            {
                var p = dir + "clipboard_" + n + ".png";
                if (normal && AssetImporter.GetAtPath(p) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            m.SetTexture("_BaseMap", Tex("Albedo"));
            m.SetTexture("_BumpMap", Tex("Normal", true)); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", Tex("AO"));
            m.SetFloat("_Smoothness", 0.35f);
            m.color = Color.white;
            EditorUtility.SetDirty(m);
            return m;
        }

        static TMP_Text WorldText(Transform parent, string name, Vector3 pos, Quaternion rot, Vector2 size, float fontSize, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            go.transform.SetParent(parent, true);
            var rt = (RectTransform)go.transform; rt.sizeDelta = size; rt.localScale = Vector3.one * 0.001f;
            rt.position = pos; rt.rotation = rot;
            var t = go.GetComponent<TextMeshPro>(); t.text = text; t.fontSize = fontSize; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        static void Fill(RectTransform r, float pad) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.one * pad; r.offsetMax = -Vector2.one * pad; }

        static GameObject Box(Transform parent, string name, Vector3 worldPos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent, true);
            g.transform.position = worldPos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 worldPos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name; g.transform.SetParent(parent, true);
            g.transform.position = worldPos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Disc(Transform parent, string name, Vector3 pos, float radius, Material m)
        {
            var d = Cylinder(parent, name, pos + Vector3.down * 0.05f, new Vector3(radius * 2, 0.05f, radius * 2), m);
            return d;
        }

        static Transform Marker(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, true); t.position = pos; return t;
        }

        static void Teleportable(GameObject floor)
        {
            if (!floor) return;
            var area = floor.AddComponent<TeleportationArea>();
            SetTeleportLayer(area);
        }

        static void SetTeleportLayer(BaseTeleportationInteractable t)
        {
            int mask = InteractionLayerMask.GetMask("Teleport");
            if (mask != 0) t.interactionLayers = mask;
        }

        static LessonStep Step(string text, LessonStep.Check c, int joint, float value, float tol, string hint) =>
            new() { instruction = text, check = c, jointIndex = joint, value = value, tolerance = tol == 0 ? 5 : tol, hint = hint };
        static LessonStep StepTarget(string text, Transform target, float tol, string hint) =>
            new() { instruction = text, check = LessonStep.Check.ReachTarget, target = target, tolerance = tol, hint = hint };
        static LessonStep StepZone(string text, Collider zone, Rigidbody item, string hint) =>
            new() { instruction = text, check = LessonStep.Check.ObjectInZone, zone = zone, item = item, hint = hint };

        // ================================================================== materials
        static void Materials()
        {
            _robotMat = Mat("RobotWhite", new Color(0.86f, 0.87f, 0.88f));
            _safety = Mat("SafetyYellow", new Color(1f, 0.8f, 0.05f));
            _cube = Mat("PartBlue", new Color(0.15f, 0.45f, 1f));
            _glass = Mat("DoorGlass", new Color(0.6f, 0.8f, 0.95f, 0.35f), true);
            // Poly Haven CC0 PBR surfaces.
            _floor = LabLook.Pbr("LabFloor", "hangar_concrete_floor", Color.white, new Vector2(6, 6), 0.5f);
            _wall = LabLook.Pbr("HallWall", "grey_plaster", new Color(0.88f, 0.9f, 0.93f), new Vector2(1, 2), 0.6f);
            _pedestal = LabLook.Pbr("Pedestal", "metal_plate_02", new Color(0.85f, 0.87f, 0.9f), new Vector2(1, 1));
            // Custom RoboticsLab shaders.
            _echoMat = LabLook.Hologram("EchoHologram", new Color(0.35f, 0.85f, 1f));
            _target = LabLook.EnergyField("TargetGreen", new Color(0.25f, 1f, 0.45f), 0.14f, 14f);
            _curtain = LabLook.EnergyField("LightCurtain", new Color(1f, 0.82f, 0.3f), 0.025f, 60f, 0.07f, 0.15f); // subtle shimmer; flares red on alarm
            _handle = LabLook.PulseGlow("IKHandle", new Color(1f, 0.45f, 0.08f), new Color(0.25f, 0.08f, 0f), 0, 1.2f);
            _lamp = LabLook.PulseGlow("IndicatorLamp", UIFactory.Amber, new Color(0.15f, 0.09f, 0.02f), 0, 0.7f);
            _pad = LabLook.PulseGlow("TeleportPad", new Color(0.25f, 0.7f, 1f), new Color(0, 0, 0), 3, 0.8f, true);
            _strip = LabLook.Emissive("LightStrip", new Color(1f, 0.93f, 0.8f), 2.2f);
        }

        static Material Mat(string name, Color c, bool transparent = false, Color? emission = null)
        {
            string path = $"Assets/Robotics/Art/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create)
            {
                Directory.CreateDirectory("Assets/Robotics/Art/Materials");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            m.color = c;
            if (transparent)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_ZWrite", 0); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10);
            }
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            if (create) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }
    }
}
