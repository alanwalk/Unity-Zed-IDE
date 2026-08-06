using Unity.CodeEditor;
using System.Collections.Generic;
using System.Xml.XPath;
using System.Text;
using NiceIO;
using System;
using System.IO;

namespace UnityZed
{
    public class ZedDiscovery
    {
        public CodeEditor.Installation[] GetInstallations()
        {
            var results = new List<CodeEditor.Installation>();

            var candidates = new (NPath path, TryGetVersion tryGetVersion)[] {

                // [MacOS]
                ("/Applications/Zed.app/Contents/MacOS/cli", TryGetVersionFromPlist),
                ("/usr/local/bin/zed", null),

                // [Linux] (Flatpak)
                ("/var/lib/flatpak/app/dev.zed.Zed/current/active/files/bin/zed", null),

                // [Linux] (Repo)
                ("/usr/bin/zeditor", null),

                // [Linux] (NixOS)
                ("/run/current-system/sw/bin/zeditor", null),
                // [Linux] (NixOS HomeManager from Zed Flake)
                ("/etc/profiles/per-user/linx/bin/zed", null),
                // [Linux] (NixOS HomeManager from NixPkgs)
                ("/etc/profiles/per-user/linx/bin/zeditor", null),

                // [Linux] (Official Website)
                (NPath.HomeDirectory.Combine(".local/bin/zed"), null),

                // [Windows] (Official Website - CLI Local Install)
                (NPath.HomeDirectory.Combine("AppData/Local/Programs/Zed/bin/zed.exe"), null),
                // [Windows] (Official Website - CLI Global Install)
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zed/bin/zed.exe"), null),
                // [Windows] (Official Website - GUI Local Install)
                (NPath.HomeDirectory.Combine("AppData/Local/Programs/Zed/Zed.exe"), null),
                // [Windows] (Official Website - GUI Global Install)
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zed/Zed.exe"), null)
            };

            foreach (var candidate in candidates)
            {
                var candidatePath = candidate.path;
                var candidateTryGetVersion = candidate.tryGetVersion ?? TryGetVersionFallback;

                if (candidatePath.FileExists())
                {
                    var name = new StringBuilder("Zed");

                    if (candidateTryGetVersion(candidatePath, out var version))
                        name.Append($" [{version}]");

                    results.Add(new()
                    {
                        Name = name.ToString(),
                        Path = candidatePath.MakeAbsolute().ToString(SlashMode.Native),
                    });

                    break;
                }
            }

            return results.ToArray();
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            foreach (var installed in GetInstallations())
            {
                if (installed.Path == editorPath)
                {
                    installation = installed;
                    return true;
                }
            }

            // Unity allows selecting an executable manually. Do not reject a valid custom,
            // preview, portable, or future Zed install merely because it is not in our list.
            if (string.IsNullOrWhiteSpace(editorPath))
            {
                installation = default;
                return false;
            }

            try
            {
                var customPath = new NPath(editorPath);
                if (customPath.FileExists() && customPath.FileNameWithoutExtension.StartsWith("zed", StringComparison.OrdinalIgnoreCase))
                {
                    installation = new()
                    {
                        Name = "Zed [Custom]",
                        Path = customPath.MakeAbsolute().ToString(),
                    };
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Invalid paths can be stored by older Unity preferences. Treat them as
                // unsupported rather than breaking the External Tools preferences UI.
            }

            installation = default;
            return false;
        }

        //
        // TryGetVersion implementations
        //
        private delegate bool TryGetVersion(NPath path, out string vertion);

        private static bool TryGetVersionFallback(NPath path, out string version)
        {
            version = null;
            return false;
        }

        private static bool TryGetVersionFromPlist(NPath path, out string version)
        {
            version = null;

            var plistPath = path.Combine("../../").Combine("Info.plist");
            if (plistPath.FileExists() == false)
                return false;

            var xPath = new XPathDocument(plistPath.ToString());
            var xNavigator = xPath.CreateNavigator().SelectSingleNode("/plist/dict/key[text()='CFBundleShortVersionString']/following-sibling::string[1]/text()");
            if (xNavigator == null)
                return false;

            version = xNavigator.Value;
            return true;
        }
    }
}
