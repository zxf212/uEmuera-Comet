using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令行构建入口（仅供打包使用，不参与运行时逻辑）。
/// 用法：
///   Unity.exe -batchmode -quit -projectPath &lt;工程根&gt; -buildTarget Android
///             -executeMethod CometBuild.BuildAndroid -logFile &lt;日志&gt;
/// 说明：工程原本 ProjectSettings/EditorBuildSettings.asset 的 m_Scenes 为空，
///       这里会把它写成 Assets/Main.unity，避免打出黑屏包。
/// </summary>
public static class CometBuild
{
    const string ScenePath = "Assets/Main.unity";
    const string ApkName = "uEmuera-Comet-0.1.apk";

    [MenuItem("Comet/构建 Android APK")]
    public static void BuildAndroid()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[CometBuild] 场景缺失: " + ScenePath);
            EditorApplication.Exit(2);
            return;
        }

        // 确保场景进入构建列表（为空会打出无内容的包）
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[CometBuild] Scenes In Build 已设为: " + ScenePath);

        // 输出到工程根的 Builds 目录
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outDir = Path.Combine(projectRoot, "Builds");
        Directory.CreateDirectory(outDir);
        string apkPath = Path.Combine(outDir, ApkName);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        Debug.Log("[CometBuild] 开始构建 -> " + apkPath);
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log(string.Format(
            "[CometBuild] 结果={0} 错误={1} 警告={2} 大小={3} 耗时={4} 输出={5}",
            s.result, s.totalErrors, s.totalWarnings, s.totalSize, s.totalTime, s.outputPath));

        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[CometBuild] 构建失败");
            EditorApplication.Exit(1);
        }
    }
}
