using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TwelfthStamp.EditorTools
{
    /// <summary>
    /// Headless builds:
    /// Unity.exe -batchmode -quit -projectPath Game -executeMethod TwelfthStamp.EditorTools.BuildScript.BuildWindows -buildPath "C:\...\TheTwelfthStamp.exe" [-devBuild]
    /// Unity.exe -batchmode -quit -projectPath Game -executeMethod TwelfthStamp.EditorTools.BuildScript.BuildWeb -buildPath "C:\...\Web" [-devBuild]
    /// Unity.exe -batchmode -quit -projectPath Game -executeMethod TwelfthStamp.EditorTools.BuildScript.BuildWebXR -buildPath "C:\...\WebXR" [-devBuild]
    /// </summary>
    public static class BuildScript
    {
        /// <summary>Idempotent project setup followed by the Windows build (one headless session).</summary>
        public static void SetupAndBuild()
        {
            ProjectSetup.Run();
            BuildWindows();
        }

        public static void BuildWindows()
        {
            string path = GetArg("-buildPath");
            if (string.IsNullOrEmpty(path)) path = Path.Combine(DefaultBuildsRoot(), "Windows", "TheTwelfthStamp.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (!ApplyIcon())
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            bool ok = Build(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = HasArg("-devBuild") ? BuildOptions.Development : BuildOptions.None
            });
            if (ok) CopyPlayerFiles(Path.GetDirectoryName(path));

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Browser build (WebGL): a folder with index.html that plays desktop mode in the browser.</summary>
        [MenuItem("Twelfth Stamp/Build Web")]
        public static void BuildWeb()
        {
            string path = GetArg("-buildPath");
            bool ok = BuildWebTo(string.IsNullOrEmpty(path) ? Path.Combine(DefaultBuildsRoot(), "Web") : path);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Browser build with WebXR (WebXRSetup): plays flat in desktop mode like BuildWeb, and switches to the VR rig when a
        /// headset browser's player presses the page's VR button.
        /// Unity.exe -batchmode -quit -projectPath Game -executeMethod TwelfthStamp.EditorTools.BuildScript.BuildWebXR -buildPath "C:\...\WebXR"
        /// </summary>
        [MenuItem("Twelfth Stamp/Build Web (WebXR)")]
        public static void BuildWebXR()
        {
            string path = GetArg("-buildPath");
            bool ok = WebXRSetup.Apply() && BuildWebTo(string.IsNullOrEmpty(path) ? Path.Combine(DefaultBuildsRoot(), "WebXR") : path);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool BuildWebTo(string path)
        {
            Directory.CreateDirectory(path);

            // Gzip with the decompression fallback, so the folder plays from any static file server (a local preview,
            // itch.io, GitHub Pages) without that server having to send Content-Encoding headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            bool ok = Build(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = path,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = HasArg("-devBuild") ? BuildOptions.Development : BuildOptions.None
            });
            return ok && PatchWebPage(Path.Combine(path, "index.html"));
        }

        /// <summary>
        /// Adapts Unity's default web page for an embed (itch.io): the canvas fills the frame instead of a fixed 960x600 box
        /// with a footer, saves persist across visits, and a pointer-lock or fullscreen request the browser refuses (a click
        /// right after Esc) no longer reaches Unity's loader, which would show it as a blocking "An error occurred" alert.
        /// The project's WebXR template (WebXRSetup.Template) has all of this built in, so its page is left as it is.
        /// </summary>
        static bool PatchWebPage(string indexPath)
        {
            if (PlayerSettings.WebGL.template == "PROJECT:" + WebXRSetup.Template)
            {
                Debug.Log("[TS-Build] Web page from the " + WebXRSetup.Template + " template (fills the frame, persistent saves, refused pointer lock handled)");
                return true;
            }
            string html = File.ReadAllText(indexPath);
            var edits = new (string from, string to)[]
            {
                ("<link rel=\"stylesheet\" href=\"TemplateData/style.css\">",
                 "<link rel=\"stylesheet\" href=\"TemplateData/style.css\">\n" +
                 "    <style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}" +
                 "#unity-container.unity-desktop{position:fixed;inset:0;transform:none}#unity-footer{display:none}" +
                 "#unity-canvas{background:#000}</style>\n" +
                 "    <script>\n" +
                 "      ['requestPointerLock', 'requestFullscreen'].forEach(function (n) {\n" +
                 "        var f = Element.prototype[n];\n" +
                 "        if (!f) return;\n" +
                 "        Element.prototype[n] = function () {\n" +
                 "          var r = f.apply(this, arguments);\n" +
                 "          if (r && r.catch) r.catch(function (e) { console.warn(n + ' refused: ' + e); });\n" +
                 "          return r;\n" +
                 "        };\n" +
                 "      });\n" +
                 "    </script>"),
                ("fullscreenContainer.style.width = \"960px\";", "fullscreenContainer.style.width = \"100vw\";"),
                ("fullscreenContainer.style.height = \"600px\";", "fullscreenContainer.style.height = \"100vh\";"),
                ("// config.autoSyncPersistentDataPath = true;", "config.autoSyncPersistentDataPath = true;"),
            };
            foreach (var (from, to) in edits)
            {
                if (!html.Contains(from))
                {
                    Debug.Log("[TS-Build] ERROR: web page patch: '" + from + "' not found in " + indexPath);
                    return false;
                }
                html = html.Replace(from, to);
            }
            File.WriteAllText(indexPath, html);
            Debug.Log("[TS-Build] Web page patched (fills the frame, persistent saves, refused pointer lock handled)");
            return true;
        }

        /// <summary>
        /// Next to the repo (C:\Dev\12th-Stamp -> C:\Dev\12th-Stamp-builds\Builds): outside source control, and not under
        /// AppData, which a packaged app such as the Claude desktop app redirects into its own private storage,
        /// where Explorer, Unity Hub and the desktop shortcuts can't see it.
        /// </summary>
        static string DefaultBuildsRoot()
        {
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(Path.GetDirectoryName(repo), Path.GetFileName(repo) + "-builds", "Builds");
        }

        static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        static bool Build(BuildPlayerOptions options)
        {
            Debug.Log("[TS-Build] Building to " + options.locationPathName);
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[TS-Build] Result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime}");
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        Debug.Log("[TS-Build] ERROR: " + m.content);
            return s.result == BuildResult.Succeeded;
        }

        /// <summary>Ships the shortcut installer, its icon and the player README next to the .exe.</summary>
        static void CopyPlayerFiles(string buildDir)
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Tools", "install"));
            foreach (var name in new[] { "Install-Shortcuts.ps1", "Install Shortcuts.cmd", "TheTwelfthStamp.ico", "README.txt" })
            {
                var from = Path.Combine(src, name);
                if (!File.Exists(from)) { Debug.Log("[TS-Build] Missing player file: " + from); continue; }
                File.Copy(from, Path.Combine(buildDir, name), true);
                Debug.Log("[TS-Build] Copied " + name);
            }
        }

        /// <summary>
        /// The Windows executable's icon (also the window, taskbar and Alt-Tab icon) from the approved key art: one
        /// texture per size Unity asks for, made by Tools/branding/make_branding.py (small sizes use the thumbnail crop,
        /// because the title is unreadable there). A missing icon fails the build instead of shipping a stand-in.
        /// </summary>
        static bool ApplyIcon()
        {
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                var path = "Assets/Branding/Icons/icon_" + sizes[i] + ".png";
                icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icons[i] == null)
                {
                    Debug.LogError("[TS-Build] ERROR: missing application icon " + path + " (run Tools/branding/make_branding.py)");
                    return false;
                }
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icons[0] }, IconKind.Any); // Player Settings > Default Icon
            Debug.Log("[TS-Build] Application icon set from Assets/Branding/Icons (sizes " + string.Join(", ", sizes) + ")");
            return true;
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains(name);
    }
}
