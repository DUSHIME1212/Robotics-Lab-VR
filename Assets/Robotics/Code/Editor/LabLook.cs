using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// Visual look of the ATLAS lab: Poly Haven (CC0) PBR materials, the custom RoboticsLab shaders,
    /// HDRI sky + ambient, a light rig, reflection probes and a Quest-friendly post-processing volume.
    /// URP-Performant runs without HDR, so post effects are tuned for LDR (Neutral tonemapping, threshold &lt; 1 bloom).
    /// </summary>
    public static class LabLook
    {
        const string PH = "Assets/Robotics/Art/Textures/PolyHaven/";
        const string MatDir = "Assets/Robotics/Art/Materials/";

        // ------------------------------------------------------------------ materials
        public static Material Pbr(string name, string texId, Color tint, Vector2 tiling, float normalScale = 1f)
        {
            var m = GetOrCreate(name, "Universal Render Pipeline/Lit");
            m.SetFloat("_Surface", 0); m.renderQueue = -1; m.SetOverrideTag("RenderType", "Opaque");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetFloat("_ZWrite", 1); m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 0);
            m.DisableKeyword("_EMISSION");
            m.SetColor("_BaseColor", tint);
            // Prefer a lab-graded albedo variant when present (e.g. brightened concrete), else the original.
            var baseMap = Tex($"{PH}{texId}/{texId}_difflab_1k.jpg") ?? Tex($"{PH}{texId}/{texId}_diff_1k.jpg");
            m.SetTexture("_BaseMap", baseMap);
            m.SetTextureScale("_BaseMap", tiling);
            var nrm = Tex($"{PH}{texId}/{texId}_nor_gl_1k.jpg", normal: true);
            if (nrm) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP"); }
            var ms = Tex($"{PH}{texId}/{texId}_metalsmooth_1k.png", linear: true);
            if (ms)
            {
                m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0); // alpha of metallic map
            }
            var ao = Tex($"{PH}{texId}/{texId}_ao_1k.jpg", linear: true);
            if (ao) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 0.8f); m.EnableKeyword("_OCCLUSIONMAP"); }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Hologram(string name, Color c)
        {
            var m = GetOrCreate(name, "RoboticsLab/Hologram");
            m.SetColor("_Color", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material EnergyField(string name, Color c, float alpha = 0.12f, float stripeDensity = 18f, float stripeAlpha = 0.25f, float rimAlpha = 0.6f)
        {
            var m = GetOrCreate(name, "RoboticsLab/EnergyField");
            m.SetColor("_Color", c); m.SetFloat("_Alpha", alpha); m.SetFloat("_StripeDensity", stripeDensity);
            m.SetFloat("_StripeAlpha", stripeAlpha); m.SetFloat("_RimAlpha", rimAlpha);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material PulseGlow(string name, Color glow, Color baseColor, float rings = 0, float speed = 1.6f, bool additive = false)
        {
            var m = GetOrCreate(name, "RoboticsLab/PulseGlow");
            m.SetColor("_Color", glow); m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_RingCount", rings); m.SetFloat("_PulseSpeed", speed);
            if (additive)
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.One); m.SetFloat("_ZWrite", 0);
                m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Emissive(string name, Color c, float intensity)
        {
            var m = GetOrCreate(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GetOrCreate(string name, string shader)
        {
            Directory.CreateDirectory(MatDir);
            var path = MatDir + name + ".mat";
            var sh = Shader.Find(shader);
            if (!sh) { Debug.LogWarning("[LabLook] shader not found: " + shader); sh = Shader.Find("Universal Render Pipeline/Lit"); }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        static Texture2D Tex(string path, bool normal = false, bool linear = false)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (linear && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
                if (ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ environment
        /// <summary>HDRI sky (Poly Haven aircraft_workshop_01), ambient + reflections from it.</summary>
        public static void Sky()
        {
            var hdrPath = PH + "HDRI/aircraft_workshop_01_2k.hdr";
            if (AssetImporter.GetAtPath(hdrPath) is TextureImporter ti && (ti.textureShape != TextureImporterShape.Texture2D || ti.maxTextureSize != 2048 || ti.mipmapEnabled))
            {
                ti.textureShape = TextureImporterShape.Texture2D; ti.maxTextureSize = 2048; ti.mipmapEnabled = false;
                ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath);
            var sky = GetOrCreate("Sky_AircraftWorkshop", "Skybox/Panoramic");
            sky.SetTexture("_MainTex", hdr);
            sky.SetFloat("_Mapping", 1);          // latitude-longitude
            sky.SetFloat("_ImageType", 0);        // 360
            sky.SetFloat("_Exposure", 0.9f);
            sky.SetFloat("_Rotation", 120f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.9f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.fogStartDistance = 18f; RenderSettings.fogEndDistance = 70f;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Key/fill light rig: warm key from the sun, cool fill, spot accents stay per-station.</summary>
        public static void Lights(Light sun)
        {
            if (sun)
            {
                sun.color = new Color(1f, 0.95f, 0.88f);
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            }
            var fill = new GameObject("Fill Light (cool)", typeof(Light)).GetComponent<Light>();
            fill.type = LightType.Directional; fill.color = new Color(0.62f, 0.75f, 1f); fill.intensity = 0.25f;
            fill.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(25, 150, 0);
        }

        /// <summary>Realtime-on-awake reflection probes per zone (no baking needed, refreshed once at load).</summary>
        public static void ReflectionProbe(Transform parent, string name, Vector3 center, Vector3 size, int resolution = 128)
        {
            var go = new GameObject(name, typeof(ReflectionProbe));
            go.transform.SetParent(parent); go.transform.position = center + Vector3.up * 1.6f;
            var p = go.GetComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            p.size = size; p.boxProjection = true; p.resolution = resolution; p.hdr = false;
            p.intensity = 1f;
            p.cullingMask = ~(1 << 5); // skip UI layer
        }

        /// <summary>Global post-processing volume (LDR-safe for Quest).</summary>
        public static void PostProcessing(Camera cam)
        {
            const string path = "Assets/Robotics/Settings/ATLAS_PostProfile.asset";
            Directory.CreateDirectory("Assets/Robotics/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }

            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                c.active = true;
                return c;
            }
            var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
            var bloom = Get<Bloom>(); bloom.threshold.Override(0.85f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.85f, 0.95f, 1f)); bloom.highQualityFiltering.Override(false);
            var color = Get<ColorAdjustments>(); color.postExposure.Override(0.15f); color.contrast.Override(12f); color.saturation.Override(6f);
            var wb = Get<WhiteBalance>(); wb.temperature.Override(-4f);
            var vignette = Get<Vignette>(); vignette.intensity.Override(0.16f); vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var vol = new GameObject("Global Post Processing", typeof(Volume)).GetComponent<Volume>();
            vol.isGlobal = true; vol.priority = 1; vol.sharedProfile = profile;

            if (cam)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None; // MSAA from the URP asset handles AA in VR
            }
        }
    }
}
