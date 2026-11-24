using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// Reads Assets/Robotics/Data/robot_specs.json (published manufacturer specs) and usd_physical_data.json
    /// (mass/joints extracted from the SimReady USDs) and formats short spec lines for boards and plaques.
    /// </summary>
    public static class RobotSpecs
    {
        [Serializable] public class Spec
        {
            public string id, name, type, notes, source, maxSpeed, maxTcpSpeed, runtime, gripForceN, ipRating;
            public float dof, payloadKg, reachMm, repeatabilityMm, weightKg, strokeMm, heightCm;
        }
        [Serializable] class File { public Spec[] robots; }

        static Dictionary<string, Spec> _specs;
        static Dictionary<string, float> _usdMass;

        static void Load()
        {
            if (_specs != null) return;
            _specs = new Dictionary<string, Spec>();
            _usdMass = new Dictionary<string, float>();
            const string specPath = "Assets/Robotics/Data/robot_specs.json";
            if (System.IO.File.Exists(specPath))
            {
                // JsonUtility cannot read null numbers: map them to 0 (= unknown).
                var json = Regex.Replace(System.IO.File.ReadAllText(specPath), @":\s*null", ": 0");
                foreach (var s in JsonUtility.FromJson<File>(json).robots) _specs[s.id] = s;
            }
            const string usdPath = "Assets/Robotics/Data/usd_physical_data.json";
            if (System.IO.File.Exists(usdPath))
                foreach (Match m in Regex.Matches(System.IO.File.ReadAllText(usdPath), "\"([^\"]+)\":\\s*\\{\\s*\"usdMassKg\":\\s*([0-9.]+)"))
                    _usdMass[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static void Reload() { _specs = null; _usdMass = null; }

        public static Spec Get(string id) { Load(); return _specs.TryGetValue(id, out var s) ? s : null; }

        /// <summary>One-line spec summary, e.g. "Payload 10 kg · Reach 1249 mm · ±0.04 mm · 40 kg · 6 DOF".</summary>
        public static string Line(string id, int jointCountFallback = 0, bool withType = false)
        {
            Load();
            var parts = new List<string>();
            var s = Get(id);
            if (s != null)
            {
                if (withType && !string.IsNullOrEmpty(s.type)) parts.Add(s.type);
                if (s.payloadKg > 0) parts.Add($"Payload {s.payloadKg:0.#} kg");
                if (s.reachMm > 0) parts.Add($"Reach {s.reachMm:0} mm");
                if (s.strokeMm > 0) parts.Add($"Stroke {s.strokeMm:0} mm");
                if (!string.IsNullOrEmpty(s.gripForceN)) parts.Add($"Grip {s.gripForceN} N");
                if (s.repeatabilityMm > 0) parts.Add($"Repeatability ±{s.repeatabilityMm:0.###} mm");
                if (s.heightCm > 0) parts.Add($"Height {s.heightCm:0} cm");
                if (!string.IsNullOrEmpty(s.maxTcpSpeed)) parts.Add($"TCP {s.maxTcpSpeed}");
                if (!string.IsNullOrEmpty(s.maxSpeed)) parts.Add($"Speed {s.maxSpeed}");
                if (!string.IsNullOrEmpty(s.ipRating)) parts.Add(s.ipRating);
                if (s.dof > 0) parts.Add($"{s.dof:0} DOF");
            }
            float mass = s != null && s.weightKg > 0 ? s.weightKg : (_usdMass.TryGetValue(id, out var um) ? um : 0);
            if (mass > 0) parts.Add(s != null && s.weightKg > 0 ? $"{mass:0.#} kg" : $"{mass:0.#} kg (sim model)");
            if ((s == null || s.dof <= 0) && jointCountFallback > 0) parts.Add($"{jointCountFallback} joints (sim model)");
            return string.Join("  ·  ", parts);
        }

        public static string Name(string id, string fallback) => Get(id)?.name ?? fallback;
        public static string Type(string id) => Get(id)?.type ?? "";
    }
}
