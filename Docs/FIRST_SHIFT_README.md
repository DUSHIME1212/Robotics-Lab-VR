# FIRST SHIFT · VR Robotics Learning Experience

Scene: `Assets/Robotics/Scenes/RoboticsLab.unity` (first in Build Settings)
Rebuild the whole scene at any time: **Robotics Lab → Build Lab Scene**

## How to play

### With a headset (Quest via Link / Air Link, or SteamVR)
1. Start Quest Link (or SteamVR) and **put the headset on before pressing Play**.
2. Press Play. If the Game view is black, the headset is idle or not worn, so Unity is rendering to it and not to the Game view.

### Without a headset (mouse + keyboard)
After about 4 seconds without a worn headset the scene switches to **desktop mode** automatically, using XRI's XR Interaction Simulator.
Press **F9** to switch immediately. To always test this way in the Editor, tick `Force Desktop In Editor` on **XR Desktop Fallback**.

| Action | Simulator control |
|---|---|
| Look around | hold right mouse button + move |
| Move | W A S D, Q / E for down/up |
| Switch hand / HMD | Tab |
| Trigger (select / UI click) | left mouse button |
| Grip (grab handle / gripper) | G |
| Teleport | point at a blue pad or the floor, then trigger |

## Story flow
1. **Arrival (lobby)**: ECHO welcomes you. Point at **START SHIFT** and pull the trigger. The lab doors open.
2. **Six chapters** unlock one at a time. Teleport to the glowing pad in front of each station.

| Ch | Robot | Concept | Success |
|---|---|---|---|
| 01 | UR17 | joints, DOF, forward kinematics | set J1/J2/J3, then put the tool in the green sphere using sliders |
| 02 | Franka FR3 | inverse kinematics, redundancy | IK on, drag the orange handle into the green sphere |
| 03 | FANUC CRX-10iA | waypoints, programming | record 3 waypoints, Run, Home |
| 04 | Robotiq 2F-85 | end effectors, pick and place | carry the blue part into the green zone |
| 05 | Comau N-220 | safety, cells, joint limits | program from outside the fence (entering trips a simulated E-stop) |
| 06 | Lite3 | quadruped gait | Trot, set ~1.0 Hz, Sit |

   ECHO gives a hint after 45 s on a step, or when you press **? Hint**. **Replay** repeats the last line.
3. **Final verification** at the central console: six scenario questions plus the reflection question.
4. **Demonstration**: all robots perform, the sign turns **DEMONSTRATION READY**, and the training summary appears.

Progress is saved (PlayerPrefs). Use **Reset progress** on the central console to start over. **Seated mode** raises the rig by 45 cm.

The **Robot Gallery** (through the opening opposite the lobby) shows 24 more SimReady robots on plinths: humanoids, wheeled and legged quadrupeds, cobots, heavy arms and grippers (grippers are enlarged for display). They idle-animate when you walk near them. Your laboratory model stands beside the gallery as the walk-in research lab.
The two Agibot G2 rigs are rigged but hidden (`Excluded` in the builder): their instanced parts import scattered and need a fix in `rig_simready.py`.

## Meta UI Kit (Meta Interaction SDK "UI Set")
All panels use Meta's UI Set: ECHO captions, teach pendants, lesson boards, hub console, final verification, START SHIFT and the clipboard.
- Package: `com.meta.xr.sdk.interaction` 209.0.0 from Meta's registry `https://npm.developer.oculus.com` (scoped registry in `Packages/manifest.json`). It needs only uGUI and TextMeshPro, not the Meta Core SDK, so it sits alongside the existing XRI / OpenXR rig.
- `Assets/Robotics/Resources/MetaUIKitLibrary.asset` points at the Quest Dark theme and at the Primary / Secondary / Destructive / Borderless button and MediumSlider prefabs. The builder creates it automatically.
- `UIFactory` builds every panel from it: the theme backplate (`RoundedBoxGradientUIDark` + `RoundedBoxUIProperties`), theme fonts and text colours, and kit buttons and sliders. `ScaledKitElement` scales the kit prefabs (authored at about 48 units tall) into our layouts. The scale sits on a parent object because the kit buttons animate their own root on hover and press.
- Interaction stays on XRI rays (`TrackedDeviceGraphicRaycaster`). The kit buttons are standard uGUI `Button`s, so no Meta rig is needed. Switching to Meta's own rig (`OVRCameraRig` + ISDK ray/poke) would also need the Meta Core SDK.
- To switch theme, point `MetaUIKitLibrary.theme` at `UIThemeQuest_Light` (or a brand theme) and rebuild.

**Trainee clipboard**: the clipboard model you supplied stands next to START SHIFT in the lobby. Grab it with the grip button. Its paper shows the current chapter, the active step and all six verifications, updating live. If dropped out of the world it returns to its stand. Textures were downsized from 4K to 1K for Quest (`Assets/Robotics/Art/Props/Clipboard/`).

## Look: lighting, materials and shaders
- **Sky and ambient light:** Poly Haven `aircraft_workshop_01` HDRI (2K) as a panoramic skybox. It also drives ambient light and default reflections, plus light distance fog.
- **Light rig:** a warm key light with soft shadows, a cool fill light, a spotlight per station, and emissive wall strips.
- **Reflection probes:** box-projected probes for the hall, lobby, gallery and research lab. They refresh once on load, so no baking is needed.
- **Post-processing** (`Assets/Robotics/Settings/ATLAS_PostProfile.asset`): Neutral tonemapping, light bloom, colour adjustments, a slightly cool white balance, and a subtle vignette. It's tuned for URP-Performant, which runs without HDR for Quest; the project's shared URP asset is unchanged.
- **PBR materials** (Poly Haven CC0, 1K): `hangar_concrete_floor` for floors (a brightened `difflab` albedo variant, original kept), `grey_plaster` for walls, `metal_plate_02` for pedestals. Roughness and metal maps are packed into URP's metallic/smoothness format.
- **Custom shaders** (`Assets/Robotics/Art/Shaders/`, URP HLSL, single-pass instanced for Quest):
  - `RoboticsLab/Hologram`: ECHO's orb and ring, with a fresnel rim, scanlines and flicker.
  - `RoboticsLab/EnergyField`: reach targets, the drop zone and the Chapter 5 light curtain. `SafetyFenceMonitor` drives its `_Alarm` to flash red on a breach.
  - `RoboticsLab/PulseGlow`: teleport pads (radial rings), the IK handle, hub lamps (steady green when verified) and the cell status beacon.
- The lighting code lives in `Assets/Robotics/Code/Editor/LabLook.cs`.

## Robot data
- `Assets/Robotics/Data/robot_specs.json`: manufacturer specs researched online (payload, reach, repeatability, weight, DOF, stroke and grip force, IP rating, speed), each with its source URL. Values that couldn't be verified are `null`.
- `Assets/Robotics/Data/usd_physical_data.json`: extracted from every SimReady USD by `Tools/Blender/extract_usd_data.py`: total mass, joint count and types, limits, and max joint velocities. The USD masses match the datasheets closely (UR17 40.4 vs 40.7 kg; Comau N-220 1182 vs 1193 kg).
- Each lesson board ends with a **SPEC SHEET** line, and every gallery plaque shows the robot's name, type and key specs. Where no published spec exists, the plaque shows the simulation model's mass and joint count.

## Asset pipeline (Blender, headless)
All scripts are in `Tools/Blender/` and run with Blender 5.2 (`-b --factory-startup`).

| Script | Purpose |
|---|---|
| `rig_simready.py <usd> <outDir> <Name>` | Reads the UsdPhysics joints, builds one bone per joint (bone Y = joint axis), rigid-binds links, exports FBX and `<Name>.joints.json` |
| `prepare_environment.py <in.fbx> <out.fbx> <maxTris> [removePrefixes] [notex]` | Fixes missing textures, removes objects, merges by material, decimates for Quest |
| `remove_region.py` | Deletes small faces in a box (used to strip a third-party logo from the lobby model) |
| `inspect_usd.py`, `preview_fbx.py`, `verify_rig.py` | Inspection and preview renders |
| `gallery_list.tsv` | The 26 gallery robots: name, source USD, caption |

Source USD packages are in `SourceAssets/SimReady/` (downloaded from the vendors' Hugging Face datasets listed on SimReady Central). Working `.blend` files are in `SourceAssets/Blend/`.

Not imported: the multi-GB SimReady scene props (surgical robots, trolleys, charging station, workstation, floor cleaner, diagnostic cart, 0.6–2 GB each). They are too heavy for Quest without dedicated optimisation.

## Known limitations / next steps
- **Not yet tested on a headset**: verify ray UI, sliders, grabbing and teleport on device.
- ECHO is captions-only. Add voice clips via `EchoAssistant.voices` (line ids: `arrival_1`, `ch1_intro`, `ch1_done`, `twist_1`, `final_1` …).
- The gripper linkage is approximated and the Lite3 walks in place (no balance or terrain).
- Engineering Mode (troubleshooting) and Assessment Mode (no hints) from the design doc are not built yet.
- This module teaches awareness. It does not qualify anyone to operate real industrial robots.

## Play mode without a headset (Editor)
Menu **Robotics Lab ▸ Play Mode**:
- **XR Interaction Simulator (no headset)** (default): OpenXR is not started in the Editor, so whatever runtime Windows has active (e.g. the Meta XR Simulator, which sets itself as the OpenXR runtime when enabled) is ignored, and XRI's simulator gives mouse/keyboard control in any scene.
- **Headset or Meta XR Simulator (OpenXR)**: starts OpenXR on Play and renders to the active runtime.
- **Always Start In Robotics Lab**: pressing Play from any open scene loads `RoboticsLab.unity`.

Only the Standalone/Editor XR setting is switched; Android (Quest) builds always start OpenXR.

## Folder structure
```
Assets/Robotics/
  Art/
    Models/        rigged SimReady robots (FBX + .joints.json), gallery.tsv
    Environment/   ATLAS_Lobby, ATLAS_Lab
    Props/         Clipboard
    Materials/     project materials (PBR + shader materials)
    Shaders/       Hologram, EnergyField, PulseGlow
    Textures/PolyHaven/  CC0 HDRI + PBR textures
  Code/
    Runtime/Robots   RobotArticulation, CCDIKSolver, GripperController, QuadrupedGait, SafetyFenceMonitor
    Runtime/Lessons  LessonStation, TeachPendant
    Runtime/Story    LabStoryManager, EchoAssistant, HubConsole, AssessmentConsole, TraineeClipboard, ...
    Runtime/UI       UIFactory, ScaledKitElement, MetaUIKitLibrary (Meta UI Set)
    Runtime/XR       XRDesktopFallback
    Editor           RoboticsLabBuilder, LabLook, RobotSpecs, XRPlayModeSwitch
  Data/       robot_specs.json, usd_physical_data.json
  Resources/  MetaUIKitLibrary.asset
  Scenes/     RoboticsLab.unity
  Settings/   ATLAS_PostProfile.asset
SourceAssets/  SimReady USD sources + .blend working files (outside Assets, not imported)
Tools/Blender/ headless rig / environment / data-extraction scripts
```

## Plugins
Installed (free):
| Package | Use |
|---|---|
| Unity AI Navigation `com.unity.ai.navigation` 2.0.16 | NPC / ECHO pathfinding |
| Yarn Spinner `dev.yarnspinner.unity` 3.3.2 (OpenUPM) | Branching dialogue for ECHO and missions |
| Unity Behavior `com.unity.behavior` 1.0.17 | Native behavior graphs for NPCs |
| Timeline `com.unity.timeline` 6.6.0 | Cinematic arrival / demonstration sequences |
| Animation Rigging `com.unity.animation.rigging` 6.6.0 | Procedural IK, repair animations |
| Meta XR Audio SDK `com.meta.xr.sdk.audio` 209.0.0 | Spatial audio and acoustics on Quest |

Not installed:
- **Paid Asset Store products** (need your Unity account and a purchase): Obi Fluid / Rope / Softbody, Magica Cloth 2, RayFire, Dialogue System for Unity, Behavior Designer, Quest Machine, SALSA LipSync, Master Audio.
- **Separate installers or accounts**: FMOD, Wwise, Steam Audio (.unitypackage), ElevenLabs (needs an API key).
- **Visual Effect Graph**: needs compute shaders and is too heavy for standalone Quest. Use the Particle System instead, as the design doc recommends.

## Baked lighting (global illumination)
`Assets/Robotics/Code/Editor/LabBakedLighting.cs` sets up baked lighting during every scene build. Bake it with **Robotics Lab ▸ Lighting ▸ Bake (GPU, async)**. A rebuild creates a new scene, so bake again after rebuilding.
- **Static architecture** (floors, walls, pedestals, fences, research lab) is lightmapped with 3 light bounces and ambient occlusion: 3 × 2K lightmaps, non-directional, with a shadowmask so moving robots still get realtime shadows from the key light.
- **The lobby** is a heavily decimated model: it still bounces light, but is lit from light probes for smooth shading.
- **14 soft rectangular area lights** (baked): a key panel over each station, a hub skylight, lobby ceiling panels and gallery rows. The hall and showroom fill lights are baked too, so they cost nothing at runtime.
- **441 light probes** across the hall, lobby, gallery and lab light the dynamic robots, UI props and the clipboard from the bake.
- **Realtime lights left:** the key light (shadows on moving parts), the station spotlights (toggled by the story), and the small glow lights.
- **Settings asset:** `Assets/Robotics/Settings/ATLAS_LightingSettings.lighting`.
