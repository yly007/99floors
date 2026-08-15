using System.Linq;
using System.Collections.Generic;
using NinetyNine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NinetyNineEditor
{
    [InitializeOnLoad]
    public static class NinetyNineProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string AutoPlaySessionKey = "NinetyNine.AutoPlayed";

        static NinetyNineProjectBuilder()
        {
            EditorApplication.delayCall += EnsureProject;
        }

        [MenuItem("Tools/The 99th Floor/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            BuildScene(true);
        }

        [MenuItem("Tools/The 99th Floor/Play Main Scene")]
        public static void PlayMainScene()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/The 99th Floor/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            EnsureBuildSettings();
            EnsureRuntimeShaders();
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string outputPath = System.IO.Path.Combine(projectRoot, "Builds", "Windows", "99Floors.exe");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException("Windows build failed: " + report.summary.result);
            }
            Debug.Log("EVACUATION_WINDOWS_BUILD=PASS PATH=" + outputPath +
                " SIZE=" + report.summary.totalSize);
        }

        private static void EnsureRuntimeShaders()
        {
            Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            SerializedObject serializedSettings = new SerializedObject(graphicsSettings);
            SerializedProperty includedShaders = serializedSettings.FindProperty("m_AlwaysIncludedShaders");
            string[] requiredShaderNames =
            {
                "Standard",
                "Unlit/Texture",
                "Particles/Standard Unlit",
                "Hidden/NinetyNine/AnalogHorror",
                "NinetyNine/AdditiveParticle"
            };
            foreach (string shaderName in requiredShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new System.InvalidOperationException("Required shader not found: " + shaderName);
                }
                bool included = false;
                for (int i = 0; i < includedShaders.arraySize; i++)
                {
                    if (includedShaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        included = true;
                        break;
                    }
                }
                if (!included)
                {
                    int index = includedShaders.arraySize;
                    includedShaders.InsertArrayElementAtIndex(index);
                    includedShaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                }
            }
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureProject()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += EnsureProject;
                return;
            }

            ConfigureTexture("Assets/Resources/Art/brass_tile.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/modular_surface_atlas.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/anomaly_decal_atlas.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/survival_item_atlas_v2.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/building_signage_atlas_v2.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/elevator_control_atlas_v3.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/horror_particle_atlas_v1.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/opening_story_atlas_v1.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/horror_ui_panel_plate_v1.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/horror_ui_button_plate_v1.png", false, 2048);

            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (!sceneExists)
            {
                BuildScene(false);
                if (!Application.isBatchMode && !SessionState.GetBool(AutoPlaySessionKey, false))
                {
                    SessionState.SetBool(AutoPlaySessionKey, true);
                    EditorApplication.delayCall += PlayMainScene;
                }
            }
            else
            {
                EnsureBuildSettings();
            }
        }

        private static void BuildScene(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[The 99th Floor] Stop Play Mode before rebuilding the scene.");
                return;
            }

            if (!force && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EnsureBuildSettings();
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Main";
            GameObject bootstrap = new GameObject("NinetyNineEvacuationGame");
            bootstrap.AddComponent<NinetyNineEvacuationGame>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildSettings();
            Selection.activeGameObject = bootstrap;
            AssetDatabase.SaveAssets();
            Debug.Log("[The 99th Floor] Main scene generated. Press Play to enter the elevator.");
        }

        private static void EnsureBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Any(scene => scene.path == ScenePath && scene.enabled))
            {
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        public static void ValidateContent()
        {
            HashSet<EvacuationTheme> themes = new HashSet<EvacuationTheme>();
            HashSet<FloorEventKind> events = new HashSet<FloorEventKind>();
            HashSet<FloorLayoutKind> layouts = new HashSet<FloorLayoutKind>();
            bool valid = true;
            bool deterministic = true;
            for (int seed = 1; seed <= 1000; seed++)
            {
                EvacuationFloorDirector director = new EvacuationFloorDirector();
                EvacuationFloorDirector replayDirector = new EvacuationFloorDirector();
                FloorPressure previous = FloorPressure.Recovery;
                for (int floor = 99; floor >= 1; floor--)
                {
                    EvacuationFloorPlan plan = director.CreatePlan(seed * 7919, floor, 12f, 99 - floor);
                    EvacuationFloorPlan replay = replayDirector.CreatePlan(seed * 7919, floor, 12f,
                        99 - floor);
                    themes.Add(plan.Theme);
                    events.Add(plan.Event);
                    layouts.Add(plan.Layout);
                    deterministic &= plan.Seed == replay.Seed && plan.Theme == replay.Theme &&
                        plan.Event == replay.Event && plan.Pressure == replay.Pressure &&
                        plan.Monster == replay.Monster && plan.Layout == replay.Layout &&
                        plan.Length == replay.Length &&
                        plan.SpawnMonster == replay.SpawnMonster && plan.SpawnNpc == replay.SpawnNpc &&
                        plan.SpawnEvidence == replay.SpawnEvidence;
                    valid &= plan.FloorNumber == floor &&
                        (floor == 99 ? plan.Length == 4 : floor == 1 ? plan.Length >= 8 : plan.Length >= 11);
                    valid &= !(floor == 99 && plan.SpawnMonster);
                    valid &= !(floor == 99 && plan.SpawnNpc);
                    valid &= !(plan.SpawnMonster && plan.SpawnNpc);
                    valid &= !EvacuationFloorEventUtility.IsPureAnomaly(plan.Event) ||
                        (!plan.SpawnMonster && !plan.SpawnNpc);
                    valid &= !(previous == FloorPressure.Chase && plan.Pressure == FloorPressure.Chase);
                    List<Vector2Int> generatedPath = new List<Vector2Int>();
                    HashSet<Vector2Int> generatedCells = new HashSet<Vector2Int>();
                    EvacuationLayoutUtility.Build(plan.Layout, plan.Length,
                        new System.Random(plan.Seed), generatedPath, generatedCells);
                    EvacuationNavigationGraph generatedNavigation =
                        new EvacuationNavigationGraph(generatedCells);
                    Vector3 generatedWaypoint;
                    valid &= generatedPath.Count == plan.Length &&
                        generatedNavigation.TryGetNextWaypoint(
                            new Vector3(0f, 0f, 4f),
                            new Vector3(generatedPath[generatedPath.Count - 1].x * 3f, 0f,
                                4f + generatedPath[generatedPath.Count - 1].y * 3f),
                            out generatedWaypoint);
                    previous = plan.Pressure;
                }
            }
            valid &= themes.Count == 6;
            valid &= events.Count >= 20;
            valid &= layouts.Count == 5;
            valid &= deterministic;
            HashSet<Vector2Int> navigationCells = new HashSet<Vector2Int>
            {
                Vector2Int.zero,
                Vector2Int.up,
                Vector2Int.up + Vector2Int.right
            };
            EvacuationNavigationGraph navigation = new EvacuationNavigationGraph(navigationCells);
            Vector3 navigationWaypoint;
            bool navigationValid = navigation.TryGetNextWaypoint(new Vector3(0f, 0f, 4f),
                new Vector3(3f, 0f, 7f), out navigationWaypoint) &&
                Vector3.Distance(navigationWaypoint, new Vector3(0f, 0f, 7f)) < 0.05f;
            valid &= navigationValid;
            Debug.Log("EVACUATION_DIRECTOR_1000_SEEDS=" + (valid ? "PASS" : "FAIL") +
                " THEMES=" + themes.Count + " EVENTS=" + events.Count +
                " LAYOUTS=" + layouts.Count);
            Debug.Log("EVACUATION_SEED_REPLAY_TEST=" + (deterministic ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_GRID_NAVIGATION_TEST=" + (navigationValid ? "PASS" : "FAIL"));
            if (!valid)
            {
                throw new System.InvalidOperationException("Evacuation content validation failed.");
            }
        }

        private static void ConfigureTexture(string path, bool repeat, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            TextureWrapMode desiredWrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (importer.wrapMode != desiredWrap)
            {
                importer.wrapMode = desiredWrap;
                changed = true;
            }
            if (importer.maxTextureSize != maxSize)
            {
                importer.maxTextureSize = maxSize;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
