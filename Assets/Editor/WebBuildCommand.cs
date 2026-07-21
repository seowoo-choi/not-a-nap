using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

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
    }
}
