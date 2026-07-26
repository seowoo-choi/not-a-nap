using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NotANap.Editor
{
    public static class WebBuildCommand
    {
        public static void Build()
        {
            string outputPath = GetArgument("-outputPath") ?? "Builds/WebGL";
            bool uncompressed = HasArgument("-uncompressedWebGL");
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("활성화된 빌드 씬이 없습니다.");

            PrepareOutputDirectory(outputPath);

            WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
            BuildReport report;
            try
            {
                if (uncompressed)
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"WebGL build failed: {report.summary.result}, errors={report.summary.totalErrors}");
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static bool HasArgument(string name)
            => Environment.GetCommandLineArgs().Contains(name);

        private static void PrepareOutputDirectory(string outputPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Builds")) +
                Path.DirectorySeparatorChar;
            string fullOutputPath = Path.GetFullPath(
                Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(projectRoot, outputPath));

            // 빌드 산출물 외의 경로를 재귀 삭제하지 못하도록 Builds/ 하위만 허용한다.
            if (!fullOutputPath.StartsWith(buildsRoot, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"WebGL output must be inside {buildsRoot}: {fullOutputPath}");

            if (Directory.Exists(fullOutputPath))
                Directory.Delete(fullOutputPath, true);
            Directory.CreateDirectory(fullOutputPath);
        }
    }
}
