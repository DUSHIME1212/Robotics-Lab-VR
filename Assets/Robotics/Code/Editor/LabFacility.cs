using UnityEngine;

namespace RoboticsLab.EditorTools
{
    /// <summary>
    /// The ATLAS building shell: one connected facility around the lobby, verification hall, robot gallery and
    /// research lab. Exterior ground, perimeter walls with a plinth + roofline trim, a roof with an open atrium
    /// over the circular hall, and an annex enclosing the research lab model.
    ///
    /// Layout (metres, world space; +Z = north):
    ///   main block  x -7..7,    z -19.6..21.2   (lobby z -19..-7, hall r 6.5 at origin, gallery z 7.3..20.3)
    ///   lab annex   x  7..17.6, z  9.0..18.6    (research lab model centred at 11.6, 13.8)
    /// </summary>
    public static class LabFacility
    {
        const float H = 4.6f;      // wall height
        const float T = 0.3f;      // wall thickness

        public static Transform Build(Transform world, Material wall, Material ground, Material trim, Material roof, float hallRadius)
        {
            var root = new GameObject("ATLAS Facility Shell").transform;
            root.SetParent(world);

            // Exterior ground slab.
            Box(root, "Exterior Ground", new Vector3(5, -0.06f, 0), new Vector3(90, 0.1f, 90), ground, collider: true);

            // ---- main block perimeter
            WallX(root, "Wall West", -7f, -19.6f, 21.2f, wall);
            WallZ(root, "Wall South", -19.6f, -7f, 7f, wall);
            WallZ(root, "Wall North", 21.2f, -7f, 7f, wall);
            WallX(root, "Wall East (hall)", 7f, -19.6f, 9.0f, wall);
            WallX(root, "Wall East (gallery end)", 7f, 18.6f, 21.2f, wall);
            // ---- lab annex
            WallZ(root, "Annex Wall South", 9.0f, 7f, 17.6f, wall);
            WallX(root, "Annex Wall East", 17.6f, 9.0f, 18.6f, wall);
            WallZ(root, "Annex Wall North", 18.6f, 7f, 17.6f, wall);

            // Trim: dark plinth at the floor and a lit fascia under the roof line (reads as architecture, not boxes).
            foreach (Transform w in root)
            {
                if (!w.name.StartsWith("Wall") && !w.name.StartsWith("Annex")) continue;
                var s = w.localScale; var p = w.position;
                Box(root, w.name + " Plinth", new Vector3(p.x, 0.15f, p.z), new Vector3(s.x + 0.04f, 0.3f, s.z + 0.04f), trim);
                Box(root, w.name + " Fascia", new Vector3(p.x, H - 0.15f, p.z), new Vector3(s.x + 0.06f, 0.3f, s.z + 0.06f), trim);
            }

            // ---- roof: 2 m tiles, open atrium over the hall so the verification ring stays sky-lit.
            var roofRoot = new GameObject("Roof").transform; roofRoot.SetParent(root);
            float atrium = hallRadius + 0.6f;
            void RoofArea(float x0, float x1, float z0, float z1)
            {
                for (float x = x0; x < x1 - 0.01f; x += 2f)
                for (float z = z0; z < z1 - 0.01f; z += 2f)
                {
                    float w = Mathf.Min(2f, x1 - x), d = Mathf.Min(2f, z1 - z);
                    var c = new Vector3(x + w / 2, H + 0.1f, z + d / 2);
                    if (new Vector2(c.x, c.z).magnitude < atrium) continue;
                    Box(roofRoot, "Roof Tile", c, new Vector3(w + 0.01f, 0.2f, d + 0.01f), roof);
                }
            }
            RoofArea(-7f, 7f, -19.6f, 21.2f);
            RoofArea(7f, 17.6f, 9.0f, 18.6f);
            // Glazed ring around the atrium edge.
            int ringSegments = 40;
            for (int i = 0; i < ringSegments; i++)
            {
                float a = i * Mathf.PI * 2 / ringSegments;
                var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * atrium + Vector3.up * (H + 0.25f);
                var seg = Box(roofRoot, "Atrium Rim", p, new Vector3(2 * Mathf.PI * atrium / ringSegments + 0.05f, 0.3f, 0.25f), trim);
                seg.transform.rotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            }
            return root;
        }

        static void WallX(Transform p, string n, float x, float z0, float z1, Material m) =>
            Box(p, n, new Vector3(x, H / 2, (z0 + z1) / 2), new Vector3(T, H, Mathf.Abs(z1 - z0)), m, collider: true);

        static void WallZ(Transform p, string n, float z, float x0, float x1, Material m) =>
            Box(p, n, new Vector3((x0 + x1) / 2, H / 2, z), new Vector3(Mathf.Abs(x1 - x0), H, T), m, collider: true);

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent, true);
            g.transform.position = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true;
            return g;
        }
    }
}
