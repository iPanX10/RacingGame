#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using PhysMat = UnityEngine.PhysicsMaterial;
#else
using PhysMat = UnityEngine.PhysicMaterial;
#endif

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Setup 1 klik: menu  Racing > Setup Everything
    /// - mengatur import tekstur, membuat Material
    /// - membuat prefab mobil (WheelCollider, Rigidbody, script), kerucut, pembatas, pohon, gerbang start
    /// - membangun scene Assets/Scenes/Race.unity lengkap (lintasan, 3 mobil AI, kamera, HUD)
    /// Aman dijalankan berulang kali (akan menimpa hasil sebelumnya).
    /// </summary>
    public static class RacingSetup
    {
        const string ModelDir = "Assets/Models/";
        const string TexDir = "Assets/Textures/";
        const string MatDir = "Assets/Materials/";
        const string PrefabDir = "Assets/Prefabs/";
        const string MeshDir = "Assets/Meshes/";
        const string SceneDir = "Assets/Scenes/";
        const string ScenePath = SceneDir + "Race.unity";

#if UNITY_6000_0_OR_NEWER
        const string PhysExt = ".physicsMaterial";
#else
        const string PhysExt = ".physicMaterial";
#endif

        // ------------------------------------------------------------------
        [InitializeOnLoadMethod]
        static void AutoPrompt()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("RacingGame.Prompted", false)) return;
                SessionState.SetBool("RacingGame.Prompted", true);
                if (File.Exists(ScenePath)) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Car.fbx") == null) return;

                if (EditorUtility.DisplayDialog("Racing Game",
                    "Scene balapan belum dibuat.\nJalankan setup otomatis sekarang?\n(bisa juga lewat menu Racing > Setup Everything)",
                    "Ya, setup", "Nanti"))
                {
                    SetupEverything();
                }
            };
        }

        [MenuItem("Racing/Setup Everything (1 klik)")]
        public static void SetupEverything()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Car.fbx") == null)
            {
                EditorUtility.DisplayDialog("Racing Game",
                    "Assets/Models/Car.fbx belum ditemukan / belum selesai di-import.\n" +
                    "Tunggu import selesai, atau jalankan BlenderScripts/build_assets.py dulu, lalu coba lagi.", "OK");
                return;
            }

            try
            {
                Progress("Menyiapkan folder...", 0.05f);
                foreach (string d in new[] { MatDir, PrefabDir, MeshDir, SceneDir })
                    EnsureFolder(d.TrimEnd('/'));

                bool inputChanged = EnsureLegacyInput();

                Progress("Mengatur tekstur...", 0.15f);
                ConfigureTextures();

                Progress("Membuat material...", 0.3f);
                Dictionary<string, Material> shared = CreateSharedMaterials();

                Progress("Membuat prefab dekorasi...", 0.45f);
                GameObject cone = MakeCone(shared);
                GameObject barrier = MakeBarrier(shared);
                GameObject tree = MakeTree(shared);
                GameObject gate = MakeStartGate(shared);

                Progress("Membuat prefab mobil...", 0.6f);
                GameObject player = MakeCar("PlayerCar", new Color(0.78f, 0.05f, 0.06f), shared, true, "Kamu", 1f, 0f);
                GameObject aiBlue = MakeCar("AICar_Biru", new Color(0.10f, 0.28f, 0.85f), shared, false, "AI Biru", 0.93f, -2f);
                GameObject aiYellow = MakeCar("AICar_Kuning", new Color(0.95f, 0.75f, 0.05f), shared, false, "AI Kuning", 0.89f, 2f);
                GameObject aiGreen = MakeCar("AICar_Hijau", new Color(0.10f, 0.60f, 0.20f), shared, false, "AI Hijau", 0.85f, 0f);

                Progress("Membangun scene...", 0.8f);
                BuildScene(shared, cone, barrier, tree, gate, player, aiBlue, aiYellow, aiGreen);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                string msg = "Selesai! Scene: Assets/Scenes/Race.unity\n\nTekan Play lalu gunakan:\n" +
                             "W/S = gas/rem, A/D = belok, Spasi = rem tangan, R = reset, C = kamera, Esc = pause.";
                if (inputChanged)
                    msg += "\n\nPENTING: Pengaturan Input diubah ke 'Both'. Restart Unity Editor sekali supaya keyboard terbaca.";
                EditorUtility.DisplayDialog("Racing Game", msg, "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[RacingSetup] Gagal: " + e);
                EditorUtility.DisplayDialog("Racing Game", "Setup gagal. Lihat Console untuk detail:\n" + e.Message, "OK");
            }
        }

        static void Progress(string text, float p)
        {
            EditorUtility.DisplayProgressBar("Racing Game Setup", text, p);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>Kalau project hanya memakai Input System baru, ubah ke "Both" (script memakai Input lama).</summary>
        static bool EnsureLegacyInput()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue == 1)
            {
                p.intValue = 2;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Tekstur & material
        // ------------------------------------------------------------------
        static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
        }

        static Shader LitShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }
            return Shader.Find("Standard");
        }

        static Material GetMat(string name, Color color, Texture2D tex = null, float smooth = 0.2f, float metal = 0f, Color? emission = null)
        {
            string path = MatDir + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = LitShader();
            // set properti untuk Standard (Built-in) sekaligus URP; yang tidak ada akan diabaikan
            m.SetColor("_Color", color);
            m.SetColor("_BaseColor", color);
            if (tex != null)
            {
                m.SetTexture("_MainTex", tex);
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_Color", Color.white);
                m.SetColor("_BaseColor", Color.white);
            }
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D Tex(string file) { return AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + file); }

        static Dictionary<string, Material> CreateSharedMaterials()
        {
            Dictionary<string, Material> d = new Dictionary<string, Material>();
            d["Road"] = GetMat("Road", Color.white, Tex("Asphalt.png"), 0.08f);
            d["Grass"] = GetMat("Grass", Color.white, Tex("Grass.png"), 0f);
            d["Curb"] = GetMat("Curb", Color.white, Tex("Curb.png"), 0.15f);
            d["Finish"] = GetMat("FinishLine", Color.white, Tex("FinishLine.png"), 0.1f);

            d["Car_Glass"] = GetMat("Car_Glass", new Color(0.05f, 0.09f, 0.13f), null, 0.95f, 0.1f);
            d["Car_Trim"] = GetMat("Car_Trim", new Color(0.03f, 0.03f, 0.035f), null, 0.3f, 0.2f);
            d["Car_Tire"] = GetMat("Car_Tire", new Color(0.02f, 0.02f, 0.02f), null, 0.1f);
            d["Car_Rim"] = GetMat("Car_Rim", new Color(0.78f, 0.8f, 0.83f), null, 0.85f, 1f);
            d["Car_Stripe"] = GetMat("Car_Stripe", new Color(0.95f, 0.95f, 0.95f), null, 0.5f, 0.2f);
            d["Car_Headlight"] = GetMat("Car_Headlight", new Color(1f, 0.95f, 0.8f), null, 0.9f, 0f, new Color(2.2f, 2.1f, 1.7f));
            d["Car_Taillight"] = GetMat("Car_Taillight", new Color(1f, 0.05f, 0.03f), null, 0.9f, 0f, new Color(2.0f, 0.1f, 0.05f));

            d["Cone_Orange"] = GetMat("Cone_Orange", new Color(1f, 0.35f, 0.02f), null, 0.3f);
            d["Cone_White"] = GetMat("Cone_White", new Color(0.95f, 0.95f, 0.95f), null, 0.3f);
            d["Cone_Base"] = GetMat("Cone_Base", new Color(0.04f, 0.04f, 0.04f), null, 0.2f);
            d["Barrier_Red"] = GetMat("Barrier_Red", new Color(0.85f, 0.05f, 0.05f), null, 0.3f);
            d["Barrier_White"] = GetMat("Barrier_White", new Color(0.95f, 0.95f, 0.95f), null, 0.3f);
            d["Barrier_Leg"] = GetMat("Barrier_Leg", new Color(0.08f, 0.08f, 0.09f), null, 0.2f);
            d["Tree_Bark"] = GetMat("Tree_Bark", new Color(0.32f, 0.20f, 0.10f), null, 0.05f);
            d["Tree_Leaf"] = GetMat("Tree_Leaf", new Color(0.10f, 0.45f, 0.12f), null, 0.05f);
            d["Gate_Metal"] = GetMat("Gate_Metal", new Color(0.18f, 0.18f, 0.20f), null, 0.6f, 0.8f);
            d["Gate_Black"] = GetMat("Gate_Black", new Color(0.02f, 0.02f, 0.02f), null, 0.3f);
            d["Gate_White"] = GetMat("Gate_White", new Color(0.97f, 0.97f, 0.97f), null, 0.3f);
            d["Gate_Light"] = GetMat("Gate_Light", new Color(0.1f, 1f, 0.2f), null, 0.5f, 0f, new Color(0.3f, 3f, 0.6f));
            return d;
        }

        static void RemapMaterials(GameObject go, Dictionary<string, Material> map)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (map.TryGetValue(mats[i].name, out Material replacement)) mats[i] = replacement;
                }
                r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------
        // Prefab dekorasi
        // ------------------------------------------------------------------
        static GameObject InstantiateModel(string file, string name)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + file);
            GameObject inst = Object.Instantiate(fbx);
            inst.name = name;
            return inst;
        }

        static GameObject SavePrefab(GameObject inst, string name)
        {
            string path = PrefabDir + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            return prefab;
        }

        static GameObject MakeCone(Dictionary<string, Material> m)
        {
            GameObject go = InstantiateModel("Cone.fbx", "Cone");
            RemapMaterials(go, m);
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.5f, 0.6f, 0.5f);
            bc.center = new Vector3(0f, 0.3f, 0f);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            return SavePrefab(go, "Cone");
        }

        static GameObject MakeBarrier(Dictionary<string, Material> m)
        {
            GameObject go = InstantiateModel("Barrier.fbx", "Barrier");
            RemapMaterials(go, m);
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(3f, 0.9f, 0.4f);
            bc.center = new Vector3(0f, 0.45f, 0f);
            return SavePrefab(go, "Barrier");
        }

        static GameObject MakeTree(Dictionary<string, Material> m)
        {
            GameObject go = InstantiateModel("Tree.fbx", "Tree");
            RemapMaterials(go, m);
            CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.3f;
            cc.height = 4f;
            cc.center = new Vector3(0f, 2f, 0f);
            return SavePrefab(go, "Tree");
        }

        static GameObject MakeStartGate(Dictionary<string, Material> m)
        {
            GameObject root = new GameObject("StartGate");
            GameObject model = InstantiateModel("StartGate.fbx", "Model");
            model.transform.SetParent(root.transform, false);
            RemapMaterials(model, m);

            // Pastikan sisi papan kotak-kotak menghadap +Z (ke arah mobil yang mendekat)
            Renderer checker = FindRenderer(model, "Gate_CheckerBlack");
            Renderer frame = FindRenderer(model, "Gate_Frame");
            if (checker != null && frame != null)
            {
                float dz = root.transform.InverseTransformPoint(checker.bounds.center).z -
                           root.transform.InverseTransformPoint(frame.bounds.center).z;
                if (dz < 0f) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            foreach (float x in new[] { -6.25f, 6.25f })
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.5f, 6f, 0.5f);
                bc.center = new Vector3(x, 3f, 0f);
            }
            return SavePrefab(root, "StartGate");
        }

        // ------------------------------------------------------------------
        // Mobil
        // ------------------------------------------------------------------
        static Renderer FindRenderer(GameObject root, string objectName)
        {
            Transform t = FindDeep(root.transform, objectName);
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform c in parent)
            {
                Transform r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static GameObject MakeCar(string prefabName, Color paintColor, Dictionary<string, Material> shared,
                                  bool player, string racerName, float skill, float lane)
        {
            GameObject root = new GameObject(prefabName);
            GameObject model = InstantiateModel("Car.fbx", "Model");
            model.transform.SetParent(root.transform, false);

            // Putar model bila bagian depan (lampu depan) ternyata menghadap -Z
            Renderer head = FindRenderer(model, "Headlights");
            Renderer tail = FindRenderer(model, "Taillights");
            if (head != null && tail != null)
            {
                float f = root.transform.InverseTransformPoint(head.bounds.center).z -
                          root.transform.InverseTransformPoint(tail.bounds.center).z;
                if (f < 0f) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // Material: cat mobil unik per prefab, sisanya bersama
            Dictionary<string, Material> map = new Dictionary<string, Material>(shared);
            map["Car_Paint"] = GetMat("CarPaint_" + prefabName, paintColor, null, 0.75f, 0.55f);
            RemapMaterials(model, map);

            // Rigidbody
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1250f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            PhysCompat.SetDrag(rb, 0.02f);
            PhysCompat.SetAngularDrag(rb, 0.8f);

            // Collider bodi (licin supaya tidak "menempel" ke tembok)
            string physPath = MatDir + "CarBody" + PhysExt;
            PhysMat pm = AssetDatabase.LoadAssetAtPath<PhysMat>(physPath);
            if (pm == null)
            {
                pm = new PhysMat("CarBody") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f };
                AssetDatabase.CreateAsset(pm, physPath);
            }

            GameObject body = new GameObject("BodyCollider");
            body.transform.SetParent(root.transform, false);
            BoxCollider bc = body.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.9f, 0.55f, 4.15f);
            bc.center = new Vector3(0f, 0.5f, 0f);
            bc.sharedMaterial = pm;

            GameObject cabin = new GameObject("CabinCollider");
            cabin.transform.SetParent(root.transform, false);
            BoxCollider cc = cabin.AddComponent<BoxCollider>();
            cc.size = new Vector3(1.5f, 0.5f, 1.8f);
            cc.center = new Vector3(0f, 1.0f, 0.1f);
            cc.sharedMaterial = pm;

            // WheelCollider
            GameObject wcRoot = new GameObject("WheelColliders");
            wcRoot.transform.SetParent(root.transform, false);
            string[] tags = { "FL", "FR", "RL", "RR" };
            WheelCollider[] wcs = new WheelCollider[4];
            Transform[] meshes = new Transform[4];
            const float suspension = 0.25f;

            for (int i = 0; i < 4; i++)
            {
                Transform wm = FindDeep(model.transform, "Wheel_" + tags[i]);
                if (wm == null) throw new System.Exception("Objek Wheel_" + tags[i] + " tidak ditemukan di Car.fbx");
                meshes[i] = wm;

                Renderer wr = wm.GetComponent<Renderer>();
                float radius = wr != null ? wr.bounds.extents.y : 0.34f;
                Vector3 lp = root.transform.InverseTransformPoint(wm.position);

                GameObject go = new GameObject("WC_" + tags[i]);
                go.transform.SetParent(wcRoot.transform, false);
                go.transform.localPosition = lp + Vector3.up * (suspension * 0.5f);

                WheelCollider wc = go.AddComponent<WheelCollider>();
                wc.radius = radius;
                wc.mass = 20f;
                wc.wheelDampingRate = 1f;
                wc.suspensionDistance = suspension;
                wc.forceAppPointDistance = 0f;
                wc.suspensionSpring = new JointSpring { spring = 38000f, damper = 4500f, targetPosition = 0.5f };

                WheelFrictionCurve f = wc.forwardFriction;
                f.stiffness = 1.8f;
                wc.forwardFriction = f;
                WheelFrictionCurve s = wc.sidewaysFriction;
                s.stiffness = 2.2f;
                wc.sidewaysFriction = s;
                wcs[i] = wc;
            }

            // Script
            CarController car = root.AddComponent<CarController>();
            car.wheelFL = wcs[0]; car.wheelFR = wcs[1]; car.wheelRL = wcs[2]; car.wheelRR = wcs[3];
            car.meshFL = meshes[0]; car.meshFR = meshes[1]; car.meshRL = meshes[2]; car.meshRR = meshes[3];

            RacerProgress rp = root.AddComponent<RacerProgress>();
            rp.racerName = racerName;
            rp.isPlayer = player;

            root.AddComponent<EngineSound>();

            if (!player)
            {
                car.useAIInput = true;
                AICarDriver ai = root.AddComponent<AICarDriver>();
                ai.skill = skill;
                ai.laneOffset = lane;
            }

            return SavePrefab(root, prefabName);
        }

        // ------------------------------------------------------------------
        // Scene
        // ------------------------------------------------------------------
        static void BuildScene(Dictionary<string, Material> m, GameObject cone, GameObject barrier, GameObject tree, GameObject gate,
                               GameObject player, GameObject aiBlue, GameObject aiYellow, GameObject aiGreen)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Cahaya
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.name = "Sun";
                sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.90f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 1100f;
            QualitySettings.shadowDistance = 160f;

            // Manager
            GameObject manager = new GameObject("RaceManager");
            LapManager lm = manager.AddComponent<LapManager>();
            lm.totalLaps = 3;
            manager.AddComponent<RaceHUD>();

            // Lintasan
            GameObject trackGO = new GameObject("Track");
            TrackBuilder tb = trackGO.AddComponent<TrackBuilder>();
            tb.roadMaterial = m["Road"];
            tb.grassMaterial = m["Grass"];
            tb.curbMaterial = m["Curb"];
            tb.finishLineMaterial = m["Finish"];
            tb.conePrefab = cone;
            tb.barrierPrefab = barrier;
            tb.treePrefab = tree;
            tb.startGatePrefab = gate;
            tb.Build();
            SaveGeneratedMeshes(trackGO);
            MarkStatic(trackGO);

            // Mobil di grid start
            GameObject[] order = { aiBlue, aiYellow, player, aiGreen };
            GameObject playerInstance = null;
            for (int slot = 0; slot < order.Length; slot++)
            {
                tb.GetGridPose(slot, out Vector3 pos, out Quaternion rot);
                GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(order[slot]);
                car.transform.SetPositionAndRotation(pos, rot);
                if (order[slot] == player) playerInstance = car;
            }

            // Kamera
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = 1500f;
                cam.fieldOfView = 62f;
                CameraFollow cf = cam.GetComponent<CameraFollow>();
                if (cf == null) cf = cam.gameObject.AddComponent<CameraFollow>();
                cf.target = playerInstance.transform;
                cf.Snap();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void SaveGeneratedMeshes(GameObject trackGO)
        {
            HashSet<Mesh> done = new HashSet<Mesh>();
            foreach (MeshFilter mf in trackGO.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || done.Contains(mesh)) continue;
                if (AssetDatabase.Contains(mesh)) continue;   // mesh dari FBX, jangan disimpan ulang
                string path = MeshDir + mesh.name + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                done.Add(mesh);
            }
        }

        static void MarkStatic(GameObject trackGO)
        {
            foreach (Transform t in trackGO.GetComponentsInChildren<Transform>())
            {
                string n = t.name;
                if (n.StartsWith("Barrier") || n.StartsWith("Tree") || n.StartsWith("Road") ||
                    n.StartsWith("Curb") || n.StartsWith("Grass") || n.StartsWith("FinishLine") || n.StartsWith("StartGate") || n.StartsWith("Gate_"))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }
        }
    }
}
#endif
