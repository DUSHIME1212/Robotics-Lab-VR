using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// Chooses how Play mode runs XR in the Editor (Robotics Lab ▸ Play Mode):
    ///  • XR Interaction Simulator (default) — OpenXR is NOT started in the Editor, so whatever OpenXR runtime
    ///    Windows has active (Meta XR Simulator, Quest Link, SteamVR) is ignored; XRI's simulator gives
    ///    mouse/keyboard control in any scene.
    ///  • Headset / Meta XR Simulator — OpenXR starts on Play and renders to the active runtime.
    /// Android (Quest) builds are never affected: only the Standalone/Editor XR settings are switched.
    /// </summary>
    [InitializeOnLoad]
    public static class XRPlayModeSwitch
    {
        const string ModeKey = "RoboticsLab.PlayModeXR";      // 0 = XRI simulator, 1 = OpenXR
        const string StartSceneKey = "RoboticsLab.StartInLab";
        const string LabScene = "Assets/Robotics/Scenes/RoboticsLab.unity";
        const string SimPrefab = "Assets/Samples/XR Interaction Toolkit/3.5.1/XR Interaction Simulator/XR Interaction Simulator.prefab";
        const string SimSettings = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";

        const string MenuSim = "Robotics Lab/Play Mode/XR Interaction Simulator (no headset)";
        const string MenuXR = "Robotics Lab/Play Mode/Headset or Meta XR Simulator (OpenXR)";
        const string MenuStart = "Robotics Lab/Play Mode/Always Start In Robotics Lab";

        public static bool UseOpenXR => EditorPrefs.GetInt(ModeKey, 0) == 1;
        static bool StartInLab => EditorPrefs.GetBool(StartSceneKey, true);

        static XRPlayModeSwitch()
        {
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Apply(); };
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuSim, false, 1)] static void SetSim() { EditorPrefs.SetInt(ModeKey, 0); Apply(); Debug.Log("[RoboticsLab] Play mode: XR Interaction Simulator (OpenXR off in Editor)."); }
        [MenuItem(MenuSim, true)] static bool SetSimCheck() { Menu.SetChecked(MenuSim, !UseOpenXR); return true; }
        [MenuItem(MenuXR, false, 2)] static void SetXR() { EditorPrefs.SetInt(ModeKey, 1); Apply(); Debug.Log("[RoboticsLab] Play mode: OpenXR (headset / Meta XR Simulator)."); }
        [MenuItem(MenuXR, true)] static bool SetXRCheck() { Menu.SetChecked(MenuXR, UseOpenXR); return true; }
        [MenuItem(MenuStart, false, 20)] static void ToggleStart() { EditorPrefs.SetBool(StartSceneKey, !StartInLab); Apply(); }
        [MenuItem(MenuStart, true)] static bool ToggleStartCheck() { Menu.SetChecked(MenuStart, StartInLab); return true; }

        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) { /* about to enter play: fine */ }
            // 1) OpenXR on/off for Editor play (Standalone XR settings are what the Editor uses).
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings && settings.InitManagerOnStart != UseOpenXR)
            {
                settings.InitManagerOnStart = UseOpenXR;
                EditorUtility.SetDirty(settings);
            }
            // 2) XRI simulator auto-spawn in every scene when not using OpenXR.
            var sim = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SimSettings);
            if (sim)
            {
                var so = new SerializedObject(sim);
                so.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = !UseOpenXR;
                so.FindProperty("m_AutomaticallyInstantiateInEditorOnly").boolValue = true;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimPrefab);
                if (prefab) so.FindProperty("m_SimulatorPrefab").objectReferenceValue = prefab;
                if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(sim);
            }
            // 3) Press Play from any open scene and land in the lab.
            EditorSceneManager.playModeStartScene = StartInLab ? AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScene) : null;
        }
    }
}
