using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Bowling;
using WeeSpurts.Core;
using WeeSpurts.UI;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// ONE CLICK builds the entire playable greybox bowling scene:
    /// lane, rails, pins, ball, camera, light, managers — fully wired.
    ///
    /// Menu: WeeSpurts → Build Greybox Bowling Scene
    ///
    /// WHY code instead of hand-placing? (a) Beginners don't have to wire
    /// anything, (b) the scene is reproducible — change LaneConfig numbers
    /// and rebuild, (c) no hand-edited scene YAML, ever (see CLAUDE.md).
    /// Safe to run repeatedly; it creates a brand-new scene each time.
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenePath = ProjectRoot + "/Scenes/BowlingAlley.unity";

        [MenuItem("WeeSpurts/Build Greybox Bowling Scene")]
        public static void Build()
        {
            // ----- 1. Config assets (created once, then reused) -----
            BallConfig ballConfig = LoadOrCreateAsset<BallConfig>(ProjectRoot + "/ScriptableObjects/BallConfig.asset");
            LaneConfig laneConfig = LoadOrCreateAsset<LaneConfig>(ProjectRoot + "/ScriptableObjects/LaneConfig.asset");

            // ----- 2. Fresh empty scene -----
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float width = laneConfig.Width;
            float length = laneConfig.Length;

            // ----- 3. Materials (URP if present, fallback to Standard) -----
            Material laneMat = LoadOrCreateMaterial("LaneMat", new Color(0.85f, 0.65f, 0.35f));
            Material railMat = LoadOrCreateMaterial("RailMat", new Color(0.25f, 0.25f, 0.3f));
            Material ballMat = LoadOrCreateMaterial("BallMat", new Color(0.15f, 0.35f, 0.9f));
            Material pinMat  = LoadOrCreateMaterial("PinMat", Color.white);

            // ----- 4. Environment -----
            GameObject env = new GameObject("Environment");

            MakeBox(env, "Floor", new Vector3(0, -0.051f, length * 0.5f - 2f),
                    new Vector3(width + 3f, 0.1f, length + 8f), railMat);
            MakeBox(env, "Lane", new Vector3(0, -0.05f, length * 0.5f),
                    new Vector3(width, 0.102f, length + 1.5f), laneMat);
            MakeBox(env, "RailLeft", new Vector3(-(width * 0.5f + 0.55f), 0.12f, length * 0.5f),
                    new Vector3(0.25f, 0.35f, length + 4f), railMat);
            MakeBox(env, "RailRight", new Vector3(width * 0.5f + 0.55f, 0.12f, length * 0.5f),
                    new Vector3(0.25f, 0.35f, length + 4f), railMat);
            MakeBox(env, "Backstop", new Vector3(0, 0.5f, length + 2.5f),
                    new Vector3(width + 2.4f, 1.2f, 0.25f), railMat);

            // ----- 5. Light -----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ----- 6. Pins -----
            GameObject deckGo = new GameObject("PinDeck");
            deckGo.transform.position = new Vector3(0f, 0f, length);
            PinDeck deck = deckGo.AddComponent<PinDeck>();

            GameObject pinGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pinGo.name = "PinTemplate";
            pinGo.transform.SetParent(deckGo.transform);
            // Cylinder mesh is 2 units tall at scale 1, so scale-y = height/2.
            pinGo.transform.localScale = new Vector3(0.12f, laneConfig.PinHeight * 0.5f, 0.12f);
            pinGo.transform.localPosition = new Vector3(0f, laneConfig.PinHeight * 0.5f, 0f);
            pinGo.GetComponent<Renderer>().sharedMaterial = pinMat;
            // The default capsule collider has a ROUND bottom — pins would
            // wobble over on their own. A box collider stands firm and still
            // tips hilariously.
            Object.DestroyImmediate(pinGo.GetComponent<CapsuleCollider>());
            pinGo.AddComponent<BoxCollider>();
            pinGo.AddComponent<Rigidbody>();
            Pin pinTemplate = pinGo.AddComponent<Pin>();
            pinGo.SetActive(false);

            deck.Initialize(laneConfig, pinTemplate);
            EditorUtility.SetDirty(deck);

            // ----- 7. Ball -----
            GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "BowlingBall";
            float d = ballConfig.Radius * 2f;
            ballGo.transform.localScale = new Vector3(d, d, d);
            ballGo.GetComponent<Renderer>().sharedMaterial = ballMat;
            ballGo.AddComponent<Rigidbody>();
            BowlingBall ball = ballGo.AddComponent<BowlingBall>();

            // Bounciness lives on a physics material so the feel is tunable.
#if UNITY_6000_0_OR_NEWER
            var bounce = new PhysicsMaterial("BallBounce");
#else
            var bounce = new PhysicMaterial("BallBounce");
#endif
            bounce.bounciness = ballConfig.Bounciness;
            // ".asset" is the safe generic extension across Unity versions.
            AssetDatabase.CreateAsset(bounce, ProjectRoot + "/ScriptableObjects/BallBounce.asset");
            ballGo.GetComponent<SphereCollider>().sharedMaterial = bounce;

            // ----- 8. Ball spawn point -----
            GameObject spawn = new GameObject("BallSpawn");
            spawn.transform.position = new Vector3(0f, ballConfig.Radius + 0.02f, 0f);
            ballGo.transform.position = spawn.transform.position;

            // ----- 9. Camera -----
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            ThrowCamera throwCam = camGo.AddComponent<ThrowCamera>();
            throwCam.ConfigureAimView(new Vector3(0f, 1.7f, -3.2f), new Vector3(12f, 0f, 0f), ballGo.transform);
            EditorUtility.SetDirty(throwCam);

            // ----- 10. Managers + game controller -----
            GameObject managers = new GameObject("GameManager");
            managers.AddComponent<GameManager>();
            managers.AddComponent<SceneLoader>();
            managers.AddComponent<AudioManager>();

            GameObject gameGo = new GameObject("BowlingGame");
            BallLauncher launcher = gameGo.AddComponent<BallLauncher>();
            BowlingGameController controller = gameGo.AddComponent<BowlingGameController>();
            controller.Configure(ballConfig, laneConfig, ball, deck, launcher, throwCam, spawn.transform);
            gameGo.AddComponent<DebugHud>();
            EditorUtility.SetDirty(controller);

            // ----- 11. Save scene + register in build settings -----
            EnsureFolder(ProjectRoot + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("WeeSpurts: Greybox bowling scene built and saved to " + ScenePath +
                      ". Press PLAY — arrows aim, hold SPACE for power, release to throw.");
        }

        // ---------- helpers ----------

        private static void MakeBox(GameObject parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = ProjectRoot + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP projects render "Standard" as pink; pick whichever exists.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { color = color };
            EnsureFolder(ProjectRoot + "/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            // AssetDatabase needs folders created level by level.
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
