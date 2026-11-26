using System.IO;
using UnityEditor;
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
