using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PacmanProjectSetup
{
    [MenuItem("MazeChase/Setup Project & Build Scene")]
    public static void SetupProject()
    {
        BuildMainScene();
        ConfigurePlayerSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MazeChaseSetup] Setup complete!");
    }

    [MenuItem("MazeChase/Build Android APK")]
    public static void BuildAndroidApk()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        ConfigurePlayerSettings();
        Directory.CreateDirectory("Releases");

        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.buildAppBundle = false;
        string apkPath = Path.GetFullPath("Releases/MazeChase3D.apk");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = apkPath,
            targetGroup = BuildTargetGroup.Android,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[MazeChaseSetup] Android APK build failed: {report.summary.result}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[MazeChaseSetup] Android APK built: {report.summary.outputPath} ({report.summary.totalSize} bytes)");
    }

    private static void BuildMainScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 42f;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camObj.AddComponent<AudioListener>();
        CameraFit fit = camObj.AddComponent<CameraFit>();
        fit.Apply25DView();

        GameObject gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();

        string scenePath = "Assets/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.productName = "Maze Chase 3D";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.allensandiego.mc3d");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
    }
}
