using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Membuat lintasan balap secara prosedural dari titik-titik kontrol (spline Catmull-Rom):
    /// jalan, curb merah-putih, rumput, garis finis, checkpoint, gerbang start,
    /// serta dekorasi (pembatas di tikungan luar, kerucut di tikungan dalam, pohon).
    /// Ubah 'controlPoints' di Inspector lalu klik kanan komponen -> "Build Track" untuk lintasan baru.
    /// </summary>
    public class TrackBuilder : MonoBehaviour
    {
        public static TrackBuilder Instance { get; private set; }

        [Header("Bentuk lintasan (x, z dalam meter, loop tertutup)")]
        public Vector2[] controlPoints = DefaultPoints();
        public float roadWidth = 16f;
        public float curbWidth = 1.2f;
        public float sampleSpacing = 3f;
        public int checkpointCount = 16;
        public float groundSize = 1800f;

        [Header("Material")]
        public Material roadMaterial;
        public Material grassMaterial;
        public Material curbMaterial;
        public Material finishLineMaterial;

        [Header("Prefab dekorasi (opsional)")]
        public GameObject conePrefab;
        public GameObject barrierPrefab;
        public GameObject treePrefab;
        public GameObject startGatePrefab;
        public int treeCount = 220;
        public int seed = 7;

        [SerializeField] Vector3[] centerline;
        [SerializeField] Vector3[] tangents;
        [SerializeField] float actualSpacing = 3f;
        [SerializeField] float totalLength;

        public int Count => centerline == null ? 0 : centerline.Length;
        public float SampleSpacing => actualSpacing;
        public float TotalLength => totalLength;

        public static Vector2[] DefaultPoints()
        {
            return new[]
            {
                new Vector2(0, 0),     new Vector2(0, 110),    new Vector2(40, 200),  new Vector2(130, 250),
                new Vector2(230, 240), new Vector2(290, 170),  new Vector2(260, 90),  new Vector2(170, 60),
                new Vector2(150, -10), new Vector2(210, -80),  new Vector2(280, -150),new Vector2(260, -240),
                new Vector2(170, -290),new Vector2(60, -270),  new Vector2(-30, -220),new Vector2(-110, -250),
                new Vector2(-200, -210),new Vector2(-230, -120),new Vector2(-180, -30),new Vector2(-110, -50),
                new Vector2(-50, -90), new Vector2(-5, -60)
            };
        }

        void OnEnable() { Instance = this; }

        void Awake()
        {
            Instance = this;
            if (Count == 0) Build();   // jaga-jaga jika belum pernah di-build di editor
        }

        // ------------------------------------------------------------------
        // Akses data garis tengah (dipakai AI, checkpoint, grid start)
        // ------------------------------------------------------------------
        int Wrap(int i) { int n = Count; return ((i % n) + n) % n; }
        public Vector3 PointAt(int i) { return centerline[Wrap(i)]; }
        public Vector3 TangentAt(int i) { return tangents[Wrap(i)]; }
        public Vector3 RightAt(int i) { return Vector3.Cross(Vector3.up, tangents[Wrap(i)]); }

        public int NearestIndex(Vector3 p, int hint = -1, int window = 30)
        {
            int n = Count;
            float best = float.MaxValue;
            int bi = 0;
            if (hint < 0)
            {
                for (int i = 0; i < n; i++)
                {
                    float d = Sqr2D(p, centerline[i]);
                    if (d < best) { best = d; bi = i; }
                }
            }
            else
            {
                for (int k = -window / 3; k <= window; k++)
                {
                    int i = Wrap(hint + k);
                    float d = Sqr2D(p, centerline[i]);
                    if (d < best) { best = d; bi = i; }
                }
            }
            return bi;
        }

        static float Sqr2D(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>Posisi grid start. slot 0 = pole position (paling depan).</summary>
        public void GetGridPose(int slot, out Vector3 pos, out Quaternion rot)
        {
            int row = slot / 2;
            float side = (slot % 2 == 0) ? -3.2f : 3.2f;
            float back = 7f + row * 9f + ((slot % 2 == 0) ? 0f : 3f);
            int idx = -Mathf.RoundToInt(back / actualSpacing);
            pos = PointAt(idx) + RightAt(idx) * side + Vector3.up * 0.5f;
            rot = Quaternion.LookRotation(TangentAt(idx), Vector3.up);
        }

        // ------------------------------------------------------------------
        [ContextMenu("Build Track")]
        public void Build()
        {
            Instance = this;
            Clear();
            SampleSpline();

            Transform root = new GameObject("TrackRoot").transform;
            root.SetParent(transform, false);

            BuildRoad(root);
            BuildCurbs(root);
            BuildFinishLine(root);
            BuildGround(root);
            BuildCheckpoints(root);
            BuildProps(root);
        }

        void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }

        // ------------------------------------------------------------------
        // Spline
        // ------------------------------------------------------------------
        void SampleSpline()
        {
            int n = controlPoints.Length;
            List<Vector3> dense = new List<Vector3>(n * 60);
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = CP(i - 1), p1 = CP(i), p2 = CP(i + 1), p3 = CP(i + 2);
                for (int k = 0; k < 60; k++) dense.Add(CatmullRom(p0, p1, p2, p3, k / 60f));
            }

            int dn = dense.Count;
            float[] cum = new float[dn + 1];
            for (int i = 0; i < dn; i++)
                cum[i + 1] = cum[i] + Vector3.Distance(dense[i], dense[(i + 1) % dn]);
            totalLength = cum[dn];

            int m = Mathf.Max(8, Mathf.RoundToInt(totalLength / sampleSpacing));
            actualSpacing = totalLength / m;
            centerline = new Vector3[m];
            int j = 0;
            for (int i = 0; i < m; i++)
            {
                float d = i * actualSpacing;
                while (j < dn - 1 && cum[j + 1] <= d) j++;
                float seg = cum[j + 1] - cum[j];
                float f = seg > 1e-5f ? (d - cum[j]) / seg : 0f;
                centerline[i] = Vector3.Lerp(dense[j], dense[(j + 1) % dn], f);
            }

            tangents = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                Vector3 t = centerline[(i + 1) % m] - centerline[(i - 1 + m) % m];
                t.y = 0f;
                tangents[i] = t.normalized;
            }
        }

        Vector3 CP(int i)
        {
            int n = controlPoints.Length;
            Vector2 v = controlPoints[((i % n) + n) % n];
            return new Vector3(v.x, 0f, v.y);
        }

        // Catmull-Rom "centripetal" (tidak membentuk loop / cusp), rumus Barry-Goldman
        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            const float alpha = 0.5f;
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Mathf.Max(Vector3.Distance(p0, p1), 1e-4f), alpha);
            float t2 = t1 + Mathf.Pow(Mathf.Max(Vector3.Distance(p1, p2), 1e-4f), alpha);
            float t3 = t2 + Mathf.Pow(Mathf.Max(Vector3.Distance(p2, p3), 1e-4f), alpha);
            float t = Mathf.Lerp(t1, t2, u);

            Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        // ------------------------------------------------------------------
        // Mesh
        // ------------------------------------------------------------------
        /// <summary>Strip mengikuti garis tengah antara offset lateral a (kiri) dan b (kanan).</summary>
        Mesh BuildStrip(string name, float a, float b, float y, float tileMeters)
        {
            int m = Count;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(totalLength / tileMeters));
            float tileLen = totalLength / tiles;

            Vector3[] verts = new Vector3[(m + 1) * 2];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[m * 6];

            for (int i = 0; i <= m; i++)
            {
                int k = i % m;
                Vector3 c = centerline[k];
                Vector3 r = Vector3.Cross(Vector3.up, tangents[k]);
                float v = (i * actualSpacing) / tileLen;
                verts[2 * i] = c + r * a + Vector3.up * y;
                verts[2 * i + 1] = c + r * b + Vector3.up * y;
                uvs[2 * i] = new Vector2(0f, v);
                uvs[2 * i + 1] = new Vector2(1f, v);
            }

            for (int i = 0; i < m; i++)
            {
                int A = 2 * i, B = 2 * i + 1, C = 2 * i + 2, D = 2 * i + 3;
                int t = i * 6;
                tris[t] = A; tris[t + 1] = C; tris[t + 2] = B;
                tris[t + 3] = B; tris[t + 4] = C; tris[t + 5] = D;
            }

            Mesh mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        GameObject MakeMeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        void BuildRoad(Transform root)
        {
            float hw = roadWidth * 0.5f;
            Mesh mesh = BuildStrip("RoadMesh", -hw, hw, 0f, 8f);
            GameObject road = MakeMeshObject(root, "Road", mesh, roadMaterial);
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void BuildCurbs(Transform root)
        {
            float hw = roadWidth * 0.5f;
            Mesh left = BuildStrip("CurbLeftMesh", -hw - curbWidth, -hw, 0.012f, 6f);
            Mesh right = BuildStrip("CurbRightMesh", hw, hw + curbWidth, 0.012f, 6f);
            MakeMeshObject(root, "Curb_Left", left, curbMaterial);
            MakeMeshObject(root, "Curb_Right", right, curbMaterial);
        }

        void BuildFinishLine(Transform root)
        {
            Vector3 c = centerline[0] + Vector3.up * 0.02f;
            Vector3 t = tangents[0];
            Vector3 r = Vector3.Cross(Vector3.up, t);
            float hw = roadWidth * 0.5f;
            float hl = 2f;

            Mesh mesh = new Mesh { name = "FinishLineMesh" };
            mesh.vertices = new[]
            {
                c - r * hw - t * hl, c + r * hw - t * hl,
                c - r * hw + t * hl, c + r * hw + t * hl
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0.25f), new Vector2(1, 0.25f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            MakeMeshObject(root, "FinishLine", mesh, finishLineMaterial);
        }

        void BuildGround(Transform root)
        {
            float s = groundSize * 0.5f;
            const float y = -0.03f;
            Mesh mesh = new Mesh { name = "GroundMesh" };
            mesh.vertices = new[]
            {
                new Vector3(-s, y, -s), new Vector3(-s, y, s),
                new Vector3(s, y, s),   new Vector3(s, y, -s)
            };
            float tile = groundSize / 20f;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, tile), new Vector2(tile, tile), new Vector2(tile, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();

            GameObject g = MakeMeshObject(root, "Grass", mesh, grassMaterial);
            BoxCollider bc = g.AddComponent<BoxCollider>();
            bc.size = new Vector3(groundSize, 1f, groundSize);
            bc.center = new Vector3(0f, y - 0.5f, 0f);
        }

        void BuildCheckpoints(Transform root)
        {
            Transform group = new GameObject("Checkpoints").transform;
            group.SetParent(root, false);
            int m = Count;
            for (int k = 0; k < checkpointCount; k++)
            {
                int idx = Mathf.RoundToInt(k * m / (float)checkpointCount) % m;
                GameObject go = new GameObject("Checkpoint_" + k);
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(centerline[idx], Quaternion.LookRotation(tangents[idx], Vector3.up));
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(roadWidth + 12f, 8f, 4f);
                bc.center = new Vector3(0f, 3.5f, 0f);
                go.AddComponent<Checkpoint>().index = k;
            }
        }

        // ------------------------------------------------------------------
        // Dekorasi
        // ------------------------------------------------------------------
        GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.transform.SetPositionAndRotation(pos, rot);
                return inst;
            }
#endif
            return Instantiate(prefab, pos, rot, parent);
        }

        void BuildProps(Transform root)
        {
            Transform props = new GameObject("Props").transform;
            props.SetParent(root, false);
            int m = Count;
            float edge = roadWidth * 0.5f + curbWidth;

            if (startGatePrefab != null)
            {
                GameObject gate = Spawn(startGatePrefab, centerline[0], Quaternion.LookRotation(-tangents[0], Vector3.up), props);
                gate.name = "StartGate";
                gate.transform.localScale = new Vector3((roadWidth + 2f * curbWidth + 1f) / 12.5f, 1f, 1f);
            }

            for (int i = 0; i < m; i++)
            {
                float turn = Vector3.SignedAngle(tangents[i], tangents[(i + 1) % m], Vector3.up); // derajat per sampel
                float absTurn = Mathf.Abs(turn);
                float outer = turn > 0f ? -1f : 1f;   // belok kanan -> sisi luar = kiri

                if (barrierPrefab != null && absTurn > 1.0f)
                {
                    Vector3 pos = centerline[i] + RightAt(i) * outer * (edge + 1.8f);
                    Quaternion rot = Quaternion.LookRotation(tangents[i], Vector3.up) * Quaternion.Euler(0f, -90f, 0f);
                    Spawn(barrierPrefab, pos, rot, props).name = "Barrier";
                }

                if (conePrefab != null && absTurn > 2.2f && i % 2 == 0)
                {
                    Vector3 pos = centerline[i] + RightAt(i) * (-outer) * (edge + 0.7f);
                    Spawn(conePrefab, pos, Quaternion.Euler(0f, (i * 53) % 360, 0f), props).name = "Cone";
                }
            }

            if (treePrefab != null) ScatterTrees(props, edge);
        }

        void ScatterTrees(Transform props, float edge)
        {
            System.Random rng = new System.Random(seed);
            Vector3 min = centerline[0], max = centerline[0];
            foreach (Vector3 c in centerline) { min = Vector3.Min(min, c); max = Vector3.Max(max, c); }
            float pad = 160f;
            float clear = edge + 12f;
            float clearSqr = clear * clear;
            List<Vector3> placed = new List<Vector3>();

            int attempts = 0;
            while (placed.Count < treeCount && attempts < treeCount * 12)
            {
                attempts++;
                float x = Mathf.Lerp(min.x - pad, max.x + pad, (float)rng.NextDouble());
                float z = Mathf.Lerp(min.z - pad, max.z + pad, (float)rng.NextDouble());
                Vector3 p = new Vector3(x, 0f, z);

                bool tooClose = false;
                for (int i = 0; i < centerline.Length && !tooClose; i++)
                    if (Sqr2D(p, centerline[i]) < clearSqr) tooClose = true;
                if (tooClose) continue;

                for (int i = 0; i < placed.Count && !tooClose; i++)
                    if (Sqr2D(p, placed[i]) < 36f) tooClose = true;
                if (tooClose) continue;

                placed.Add(p);
                float s = Mathf.Lerp(0.8f, 1.5f, (float)rng.NextDouble());
                GameObject t = Spawn(treePrefab, p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), props);
                t.name = "Tree";
                t.transform.localScale = Vector3.one * s;
            }
        }
    }
}
