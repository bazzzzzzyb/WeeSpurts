using System.Collections.Generic;
using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeeSpurts.Player;

namespace WeeSpurts.Editor
{
    /// <summary>
    /// SPIKE ONLY — see Docs/spikes/MirrorKcpSpikePrompt.md and
    /// MirrorKcpSpikeStatus.md. Not part of the shipped game; delete with the
    /// rest of the spike once it has produced its findings doc.
    ///
    /// Step 2: NetworkManager + KcpTransport + the stock NetworkManagerHUD.
    /// Step 3 adds a minimal NETWORKED roaming player — CharacterController +
    /// FirstPersonController + PlayerAvatar + PlayerCameraDirector, reused
    /// as-is from the real game, plus NetworkIdentity + NetworkTransformUnreliable
    /// (a judgment call: Mirror ships Reliable/Unreliable/Hybrid side-by-side in
    /// Examples/PlayerTest/CharacterController for exactly this decision — this
    /// is not a discovered "correct" default). No bowling, no interaction, no
    /// venue geometry — those stay out of this spike's one question.
    ///
    /// Menu: WeeSpurts/Spike/Build Net KCP Scene
    /// </summary>
    public static class SpikeNetKcpSceneBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenePath = ProjectRoot + "/Scenes/SpikeNetKcp.unity";
        private const string RoamConfigPath = ProjectRoot + "/ScriptableObjects/RoamConfig.asset";
        private const string PlayerPrefabPath = ProjectRoot + "/Prefabs/SpikeNetKcpPlayer.prefab";

        [MenuItem("WeeSpurts/Spike/Build Net KCP Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A floor + light, nothing more — NOT venue geometry, just enough
            // ground to stand on so roaming replication is actually observable.
            // Without this the player free-falls through an unlit void forever
            // (no isGrounded, no light to see by), which looks identical to a
            // broken camera/input gate but isn't one.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f); // top face lands at y=0

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject managerGo = new GameObject("NetworkManager");
            NetworkManager manager = managerGo.AddComponent<NetworkManager>();
            KcpTransport transport = managerGo.AddComponent<KcpTransport>();
            // NetworkManager.InitializeSingleton() only auto-finds a Transport on
            // the same object as a fallback (with a warning) if this is left
            // null — assigning it directly is the real wiring, not a workaround.
            manager.transport = transport;
            managerGo.AddComponent<NetworkManagerHUD>();

            GameObject playerPrefab = BuildPlayerPrefab();
            manager.playerPrefab = playerPrefab;

            EnsureFolder(ProjectRoot + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Append, don't replace: BowlingAlley.unity is already the sole
            // Build Settings entry and this spike has no business removing it.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("WeeSpurts spike: built " + ScenePath + " with playerPrefab '" + playerPrefab.name +
                       "'. Press Play, click Host (Server + Client) in one instance and Client in the " +
                       "other — each connection should spawn a walkable body (WASD + mouse-look) that " +
                       "only its OWNING machine can move or see through.");
        }

        /// <summary>
        /// Rebuilt from scratch every run (unlike RoamConfig, nothing here is
        /// meant to survive hand-tuning between reruns) — same "safe to run
        /// repeatedly" precedent as GreyboxSceneBuilder's scene.
        /// </summary>
        private static GameObject BuildPlayerPrefab()
        {
            bool roamConfigAlreadyExisted = AssetDatabase.LoadAssetAtPath<RoamConfig>(RoamConfigPath) != null;
            RoamConfig roamConfig = LoadOrCreateAsset<RoamConfig>(RoamConfigPath);
            if (!roamConfigAlreadyExisted) EditorUtility.SetDirty(roamConfig);

            GameObject playerGo = new GameObject("Player");
            // NetworkIdentity FIRST: NetworkBehaviour.OnValidate (NetworkBehaviour.cs:176-203)
            // fires synchronously the moment a NetworkBehaviour-derived component
            // (PlayerAvatar, NetworkTransformUnreliable) is added, and logs a
            // console error if no NetworkIdentity exists yet anywhere in the
            // object's hierarchy. Adding it last "worked" (the saved prefab was
            // still correct) but spammed that error on every rebuild for no
            // reason — this ordering is the actual fix, not just a quieter one.
            playerGo.AddComponent<NetworkIdentity>();
            CharacterController controller = playerGo.AddComponent<CharacterController>();
            // Same numbers RoamingSetupTool uses for the real venue, so this
            // spike's movement feel is directly comparable, not a guess.
            controller.radius = roamConfig.ControllerRadius;
            controller.height = roamConfig.ControllerHeight;
            controller.center = new Vector3(0f, roamConfig.ControllerHeight * 0.5f, 0f);
            controller.stepOffset = roamConfig.StepOffset;
            controller.slopeLimit = roamConfig.SlopeLimit;
            controller.skinWidth = Mathf.Max(0.01f, roamConfig.ControllerRadius * 0.1f);

            GameObject cameraGo = new GameObject("FirstPersonCamera");
            cameraGo.transform.SetParent(playerGo.transform, worldPositionStays: false);
            cameraGo.transform.localPosition = new Vector3(0f, roamConfig.EyeHeight, 0f);
            Camera fpCamera = cameraGo.AddComponent<Camera>();
            AudioListener fpListener = cameraGo.AddComponent<AudioListener>();
            // Start disabled, same as RoamingSetupTool: PlayerCameraDirector is
            // what turns this on, and only for the local player (see below).
            fpCamera.enabled = false;
            fpListener.enabled = false;

            // Visual only — without this, Step 3's actual point (does the OTHER
            // machine's roaming movement replicate) is unobservable: you could
            // only confirm your OWN camera/input isn't hijacked, never see the
            // remote avatar move. No collider (the CharacterController already
            // handles collision) — same cosmetic-primitive pattern as
            // GreyboxSceneBuilder.MakeCosmeticBox. Not culled from your own
            // first-person view (that needs the 'LocalPlayerModel' layer
            // RoamingSetupTool sets up) — a known, accepted corner-cut for a
            // spike: you'll see the inside of your own capsule, harmlessly.
            GameObject bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(playerGo.transform, worldPositionStays: false);
            bodyGo.transform.localPosition = new Vector3(0f, roamConfig.ControllerHeight * 0.5f, 0f);
            // Capsule primitive is 2m tall at scale 1, so scale.y = height/2 —
            // same math GreyboxSceneBuilder uses for the pin cylinder.
            bodyGo.transform.localScale = new Vector3(roamConfig.ControllerRadius * 2f,
                                                        roamConfig.ControllerHeight * 0.5f,
                                                        roamConfig.ControllerRadius * 2f);
            Object.DestroyImmediate(bodyGo.GetComponent<CapsuleCollider>());

            FirstPersonController firstPerson = playerGo.AddComponent<FirstPersonController>();
            PlayerAvatar avatar = playerGo.AddComponent<PlayerAvatar>();
            PlayerCameraDirector cameraDirector = playerGo.AddComponent<PlayerCameraDirector>();

            var firstPersonSo = new SerializedObject(firstPerson);
            Wire(firstPersonSo, "config", roamConfig);
            Wire(firstPersonSo, "cameraPivot", cameraGo.transform);
            // "reactionActor" left unwired — no character model in this spike,
            // so no animator to drive. FirstPersonController treats it as
            // optional (null just means no walk-cycle animation).
            firstPersonSo.ApplyModifiedPropertiesWithoutUndo();

            var avatarSo = new SerializedObject(avatar);
            Wire(avatarSo, "characterController", controller);
            Wire(avatarSo, "firstPersonController", firstPerson);
            // "interactor", "throwerAimSlide", "throwerModel" left unwired —
            // bowling and interaction are out of this spike's one question.
            avatarSo.ApplyModifiedPropertiesWithoutUndo();

            var directorSo = new SerializedObject(cameraDirector);
            Wire(directorSo, "avatar", avatar);
            Wire(directorSo, "firstPersonCamera", fpCamera);
            Wire(directorSo, "firstPersonListener", fpListener);
            // "bowlingCamera"/"bowlingListener" left unwired — both are
            // null-checked in PlayerCameraDirector.Apply, so a roaming-only
            // spike with no bowling camera is a supported, not a broken, state.
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            playerGo.AddComponent<NetworkTransformUnreliable>();

            EnsureFolder(ProjectRoot + "/Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerGo, PlayerPrefabPath);
            Object.DestroyImmediate(playerGo);
            return prefab;
        }

        private static void Wire(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[SpikeNetKcp] '{so.targetObject.GetType().Name}' has no serialized field " +
                               $"called '{propertyName}', so it was left unwired. Did the field get renamed?");
                return;
            }
            property.objectReferenceValue = value;
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
            string[] parts = path.Split('/');
            string current = parts[0];
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
