using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// Baked global illumination for the ATLAS lab (Quest-friendly: lightmaps + light probes, no runtime GI cost).
    /// • Static architecture (floors, walls, pedestals, lobby, research lab) is lightmapped with bounce light + AO.
    /// • Robots, UI and props stay dynamic and are lit by a light-probe grid sampled from the bake.
    /// • Soft rectangular area lights above stations + warm ceiling panels give studio-style key light.
    /// Menu: Robotics Lab ▸ Lighting ▸ Bake (runs async on the GPU lightmapper).
    /// </summary>
    public static class LabBakedLighting
    {
        static readonly string[] DynamicRoots = { "Chapters", "ECHO", "Hub Console", "Trainee Clipboard (grab me)", "START SHIFT", "XR Origin (XR Rig)", "Robot Gallery (Research Wing)" };

        /// <summary>Called by the scene builder after the world exists.</summary>
        public static void Setup(Transform world, Vector3 lobby, Vector3 gallery, Vector3 lab, float hallRadius)
        {
            MarkStatic(world);
            AreaLights(world, hallRadius, lobby, gallery);
            // Research lab sits under the facility roof now: give it ceiling panels too.
            for (int i = 0; i < 3; i++)
                Area(world, $"Research Lab Panel {i + 1}", lab + new Vector3(-3.2f + i * 3.2f, 2.85f, 0), new Vector2(2.2f, 0.6f), new Color(0.97f, 0.98f, 1f), 3.5f, 6f);
            MixedLights();
            LightProbes(world, hallRadius, lobby, gallery, lab);
            Settings();
        }

        // ------------------------------------------------------------------ static flags
        static void MarkStatic(Transform world)
        {
            foreach (var r in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                if (go.name.Contains("Light Strip") || go.name.Contains("Teleport") || go.name.Contains("Door")) continue; // emissive / moving
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                // The lobby model is heavily decimated (poor lightmap UVs): it still contributes bounce light,
                // but is lit from the probe grid, which gives smooth shading on faceted meshes.
                bool decimated = r.transform.IsChildOf(world) && r.GetComponentInParent<Transform>().root == world.root && HasAncestor(r.transform, "ATLAS_Lobby");
                r.receiveGI = decimated ? ReceiveGI.LightProbes : ReceiveGI.Lightmaps;
                // Big flat surfaces get more texels, clutter fewer (keeps lightmap count low for Quest).
                var size = r.bounds.size;
                float area = Mathf.Max(size.x * size.z, size.x * size.y, size.y * size.z);
                var so = new SerializedObject(r);
                var scale = so.FindProperty("m_ScaleInLightmap");
                if (scale != null) { scale.floatValue = area > 40 ? 0.6f : area < 0.5f ? 0.5f : 1f; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            // Pedestals / stands / fences outside the world root are also static.
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                var n = r.gameObject.name;
                if (n == "Pedestal" || n == "Plinth" || n == "Safety Fence" || n == "Console Base" || n == "Clipboard Stand")
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                    r.receiveGI = ReceiveGI.Lightmaps;
                }
            }
        }

        static bool HasAncestor(Transform t, string name)
        {
            for (; t; t = t.parent) if (t.name.StartsWith(name)) return true;
            return false;
        }

        // ------------------------------------------------------------------ lights
        static void AreaLights(Transform world, float hallRadius, Vector3 lobby, Vector3 gallery)
        {
            var root = new GameObject("Baked Area Lights").transform;
            root.SetParent(world);
            // Soft key panel above every station (ring at 3.2 m radius, 6 stations).
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Lerp(-150f, 150f, i / 5f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 2.6f + Vector3.up * 3.3f;
                Area(root, $"Station Panel {i + 1}", pos, new Vector2(1.6f, 1.0f), new Color(1f, 0.95f, 0.88f), 4.5f, 7f);
            }
            // Central hub skylight.
            Area(root, "Hub Skylight", new Vector3(0, 3.6f, 0), new Vector2(2.4f, 2.4f), new Color(0.85f, 0.92f, 1f), 3.5f, 8f);
            // Lobby ceiling panels.
            for (int i = 0; i < 3; i++)
                Area(root, $"Lobby Panel {i + 1}", lobby + new Vector3(-2.5f + i * 2.5f, 3.4f, 1.5f), new Vector2(1.8f, 0.6f), new Color(1f, 0.92f, 0.82f), 4f, 6f);
            // Gallery rows.
            for (int i = 0; i < 4; i++)
                Area(root, $"Gallery Panel {i + 1}", gallery + new Vector3(0, 3.6f, -4.5f + i * 3f), new Vector2(6f, 0.5f), new Color(0.95f, 0.97f, 1f), 3.5f, 7f);
        }

        static void Area(Transform parent, string name, Vector3 pos, Vector2 size, Color c, float intensity, float range)
        {
            var l = new GameObject(name, typeof(Light)).GetComponent<Light>();
            l.transform.SetParent(parent); l.transform.position = pos;
            l.transform.rotation = Quaternion.Euler(90, 0, 0); // facing down
            l.type = LightType.Rectangle;
            l.areaSize = size;
            l.lightmapBakeType = LightmapBakeType.Baked;
            l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.Soft;
        }

        static void MixedLights()
        {
            foreach (var l in Object.FindObjectsByType<Light>())
            {
                if (l.type == LightType.Rectangle || l.type == LightType.Disc) continue;
                if (l.type == LightType.Directional)
                {
                    // Key light: baked indirect + realtime direct shadows on the moving robots.
                    l.lightmapBakeType = LightmapBakeType.Mixed;
                }
                else if (l.name.StartsWith("Station Spotlight"))
                {
                    l.lightmapBakeType = LightmapBakeType.Realtime; // toggled by the story (chapter unlocks)
                    l.intensity = Mathf.Min(l.intensity, 4f);
                }
                else if (l.name.StartsWith("Hall Light") || l.name.StartsWith("Showroom Light"))
                {
                    l.lightmapBakeType = LightmapBakeType.Baked; // fill: bake it, free at runtime
                    l.shadows = LightShadows.Soft;
                }
                else
                {
                    l.lightmapBakeType = LightmapBakeType.Realtime; // lamp/beacon/orb glow lights stay live
                }
            }
        }

        // ------------------------------------------------------------------ light probes
        static void LightProbes(Transform world, float hallRadius, Vector3 lobby, Vector3 gallery, Vector3 lab)
        {
            var go = new GameObject("Light Probes");
            go.transform.SetParent(world);
            var group = go.AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            void Grid(Vector3 center, Vector2 extent, float step, bool circle = false, float r = 0)
            {
                for (float x = -extent.x; x <= extent.x + 0.01f; x += step)
                for (float z = -extent.y; z <= extent.y + 0.01f; z += step)
                {
                    if (circle && new Vector2(x, z).magnitude > r) continue;
                    foreach (var y in new[] { 0.4f, 1.4f, 2.6f })
                        pts.Add(center + new Vector3(x, y, z));
                }
            }
            Grid(Vector3.zero, new Vector2(hallRadius - 0.5f, hallRadius - 0.5f), 1.5f, true, hallRadius - 0.4f);
            Grid(lobby, new Vector2(5f, 5f), 2f);
            Grid(gallery, new Vector2(5f, 6f), 2f);
            Grid(lab, new Vector2(4.8f, 3.5f), 2f);
            group.probePositions = pts.ConvertAll(p => go.transform.InverseTransformPoint(p)).ToArray();
        }

        // ------------------------------------------------------------------ lightmapper settings
        static void Settings()
        {
            // A freshly built scene has no Lighting Settings asset (the getter throws), so always assign ours.
            const string path = "Assets/Robotics/Settings/ATLAS_LightingSettings.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (!ls) { ls = new LightingSettings { name = "ATLAS_LightingSettings" }; AssetDatabase.CreateAsset(ls, path); }
            Lightmapping.lightingSettings = ls;
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.Shadowmask;          // baked shadows from static, realtime on dynamic
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.lightmapResolution = 12f;                              // texels / unit
            ls.lightmapPadding = 4;
            ls.lightmapMaxSize = 2048;
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            ls.ao = true; ls.aoMaxDistance = 0.6f; ls.aoExponentDirect = 0.6f; ls.aoExponentIndirect = 1.2f;
            ls.directionalityMode = LightmapsMode.NonDirectional;     // cheaper on Quest
            ls.maxBounces = 3;
            ls.directSampleCount = 32; ls.indirectSampleCount = 256; ls.environmentSampleCount = 256;
            ls.filteringMode = LightingSettings.FilterMode.Auto;
            ls.albedoBoost = 1.15f;
            ls.indirectScale = 1.1f;
            EditorUtility.SetDirty(ls);
        }

        // ------------------------------------------------------------------ bake
        [MenuItem("Robotics Lab/Lighting/Bake (GPU, async)")]
        public static void Bake()
        {
            EnsureLightmapUVs();
            Lightmapping.bakeCompleted -= OnBaked;
            Lightmapping.bakeCompleted += OnBaked;
            if (!Lightmapping.BakeAsync()) Debug.LogWarning("[RoboticsLab] Could not start bake.");
            else Debug.Log("[RoboticsLab] Lighting bake started (GPU progressive).");
        }

        [MenuItem("Robotics Lab/Lighting/Clear Baked Data")]
        public static void Clear() { Lightmapping.Clear(); Lightmapping.ClearDiskCache(); }

        static void OnBaked()
        {
            Lightmapping.bakeCompleted -= OnBaked;
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[RoboticsLab] Lighting bake complete: {LightmapSettings.lightmaps.Length} lightmaps.");
        }

        /// <summary>Environment FBX files need a second UV set for lightmaps.</summary>
        static void EnsureLightmapUVs()
        {
            foreach (var path in new[] { "Assets/Robotics/Art/Environment/ATLAS_Lobby.fbx", "Assets/Robotics/Art/Environment/ATLAS_Lab.fbx" })
                if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.generateSecondaryUV)
                {
                    mi.generateSecondaryUV = true;
                    mi.secondaryUVHardAngle = 88; mi.secondaryUVPackMargin = 8;
                    mi.SaveAndReimport();
                }
        }
    }
}
