using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace RoboticsLab.Story
{
    /// <summary>
    /// Fixes the "black Game view" problem: when OpenXR starts against a running runtime (Quest Link / SteamVR)
    /// but nobody is wearing the headset, Unity renders into the idle headset and the Game view stays black.
    /// After a short grace period with no user presence (or no XR at all), this shuts XR down and enables the
    /// XR Interaction Simulator so the experience is fully playable with mouse + keyboard.
    /// Press F9 at any time to force desktop mode.
    /// </summary>
    public class XRDesktopFallback : MonoBehaviour
    {
        public GameObject simulator;          // XR Interaction Simulator (inactive in the scene)
        public float graceSeconds = 4f;
        public bool forceDesktopInEditor;     // tick to always test with mouse + keyboard
        public bool DesktopMode { get; private set; }

        IEnumerator Start()
        {
            if (forceDesktopInEditor && Application.isEditor) { SwitchToDesktop("forced in inspector"); yield break; }
            yield return null; // let XR Management finish starting (or not)
            if (!XRRunning()) { SwitchToDesktop("XR not started (Editor simulator mode)"); yield break; }
            float t = 0;
            while (t < graceSeconds)
            {
                if (HeadsetWorn()) yield break; // real VR session: do nothing
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            SwitchToDesktop(XRRunning() ? "headset not worn / not streaming" : "no XR runtime");
        }

        void Update()
        {
            if (!DesktopMode && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
                SwitchToDesktop("F9");
        }

        // The simulated headset starts near the floor; lift the rig's camera offset so eyes sit at ~1.6 m.
        IEnumerator FixEyeHeight()
        {
            yield return null;
            yield return null;
            var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            var cam = Camera.main;
            if (!origin || !cam || !origin.CameraFloorOffsetObject) yield break;
            var off = origin.CameraFloorOffsetObject.transform;
            off.localPosition = new Vector3(0, Mathf.Max(0, 1.6f - cam.transform.localPosition.y), 0);
        }

        static bool XRRunning()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            return displays.Exists(d => d.running);
        }

        static bool HeadsetWorn()
        {
            if (!XRRunning()) return false;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return false;
            if (head.TryGetFeatureValue(CommonUsages.userPresence, out bool present)) return present;
            return head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        }

        void SwitchToDesktop(string reason)
        {
            if (DesktopMode) return;
            DesktopMode = true;
            var mgr = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
            if (mgr && mgr.isInitializationComplete)
            {
                mgr.StopSubsystems();
                mgr.DeinitializeLoader();
            }
            // XRI may already have auto-spawned its simulator (project setting); never run two.
            var existing = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>();
            if (simulator && (existing == null || existing.gameObject == simulator)) simulator.SetActive(true);
            var cam = Camera.main;
            if (cam) cam.stereoTargetEye = StereoTargetEyeMask.None;
            StartCoroutine(FixEyeHeight());
            Debug.Log($"[ATLAS] Desktop mode ({reason}). Use the XR Interaction Simulator: hold right mouse to look, WASD to move, Tab to switch hands, T/G for triggers.");
        }
    }
}
