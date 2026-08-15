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
                options = BuildOptions.None
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
                "Particles/Standard Unlit",
                "Hidden/NinetyNine/AnalogHorror"
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

            ConfigureTexture("Assets/Resources/Art/title_hall.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/brass_tile.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/wall_tile.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/hospital_tile.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/office_wall.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/apartment_wall.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/maintenance_metal.png", true, 1024);
            ConfigureTexture("Assets/Resources/Art/modular_surface_atlas.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/anomaly_decal_atlas.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/survival_item_atlas_v2.png", false, 2048);
            ConfigureTexture("Assets/Resources/Art/building_signage_atlas_v2.png", false, 2048);

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
            GameObject bootstrap = new GameObject("NinetyNineGame");
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
            bool valid = true;
            for (int seed = 1; seed <= 100; seed++)
            {
                EvacuationFloorDirector director = new EvacuationFloorDirector();
                FloorPressure previous = FloorPressure.Recovery;
                for (int floor = 99; floor >= 1; floor--)
                {
                    EvacuationFloorPlan plan = director.CreatePlan(seed * 7919, floor, 12f, 99 - floor);
                    themes.Add(plan.Theme);
                    events.Add(plan.Event);
                    valid &= plan.FloorNumber == floor &&
                        (floor == 99 ? plan.Length == 4 : floor == 1 ? plan.Length >= 8 : plan.Length >= 11);
                    valid &= !(floor == 99 && plan.SpawnMonster);
                    valid &= !(previous == FloorPressure.Chase && plan.Pressure == FloorPressure.Chase);
                    previous = plan.Pressure;
                }
            }
            valid &= themes.Count == 6;
            valid &= events.Count >= 20;
            Debug.Log("EVACUATION_DIRECTOR_100_SEEDS=" + (valid ? "PASS" : "FAIL") +
                " THEMES=" + themes.Count + " EVENTS=" + events.Count);
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
