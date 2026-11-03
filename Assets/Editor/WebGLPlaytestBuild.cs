using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line WebGL build for GitHub Pages playtest links (see the gh-pages README):
/// Unity -batchmode -quit -projectPath . -executeMethod WebGLPlaytestBuild.Build -buildOutput path/to/folder
/// Uses Gzip with decompression fallback for this build only, then restores the project settings.
/// </summary>
public static class WebGLPlaytestBuild
{
    public static void Build()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-buildOutput");
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -buildOutput <folder>.");
        string output = args[index + 1];

        var compression = PlayerSettings.WebGL.compressionFormat;
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[WebGLPlaytestBuild] {report.summary.result}: {report.summary.totalSize / 1048576f:0.0} MB, {report.summary.totalErrors} errors -> {output}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
        }
    }
}
