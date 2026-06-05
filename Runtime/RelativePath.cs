using System;
using System.IO;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Serializable relative path with a selectable root mode.
    /// </summary>
    [Serializable]
    public struct RelativePath
    {
        /// <summary>
        /// Root used to resolve Path.
        /// </summary>
        public RelativePathMode Mode;

        /// <summary>
        /// Relative path below the selected root.
        /// </summary>
        public string Path;

        /// <summary>
        /// Default recipe source directory resolved from LayeredTexture project preferences.
        /// </summary>
        public static RelativePath ProjectPreferences => new()
        {
            Mode = RelativePathMode.ProjectPreferences
        };

        /// <summary>
        /// Creates a path relative to the Unity Assets folder from an Assets/... asset path.
        /// </summary>
        /// <param name="assetPath">Project asset path under Assets.</param>
        /// <returns>Relative path using ProjectAssets mode.</returns>
        public static RelativePath FromAssetPath(string assetPath) => new()
        {
            Mode = RelativePathMode.ProjectAssets,
            Path = AssetRelativePath(assetPath)
        };

        /// <summary>
        /// Resolves this value to an absolute filesystem path.
        /// </summary>
        /// <param name="absolutePath">Resolved absolute path when successful.</param>
        /// <returns>True when the path is valid and stays inside its selected root.</returns>
        public bool TryGetAbsolutePath(out string absolutePath) => TryGetAbsolutePath(null, out absolutePath);

        /// <summary>
        /// Resolves this value to an absolute filesystem path.
        /// </summary>
        /// <param name="projectPreferencesRoot">Absolute root used by ProjectPreferences mode.</param>
        /// <param name="absolutePath">Resolved absolute path when successful.</param>
        /// <returns>True when the path is valid and stays inside its selected root.</returns>
        public bool TryGetAbsolutePath(string projectPreferencesRoot, out string absolutePath)
        {
            absolutePath = null;

            if (System.IO.Path.IsPathRooted(Path ?? string.Empty))
                return false;

            var root = RootForMode(Mode, projectPreferencesRoot);

            if (string.IsNullOrWhiteSpace(root))
                return false;

            root = System.IO.Path.GetFullPath(root);
            absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, Path ?? string.Empty));

            if (!SamePath(absolutePath, root) && !IsInside(absolutePath, root))
            {
                absolutePath = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether this path resolves to an existing directory.
        /// </summary>
        /// <param name="projectPreferencesRoot">Absolute root used by ProjectPreferences mode.</param>
        /// <returns>True when the path resolves and the directory exists.</returns>
        public bool IsValidDirectory(string projectPreferencesRoot = null) =>
            TryGetAbsolutePath(projectPreferencesRoot, out var absolutePath) && Directory.Exists(absolutePath);

        /// <summary>
        /// Converts an absolute filesystem path to a RelativePath using the requested mode.
        /// </summary>
        /// <param name="absolutePath">Absolute filesystem path to convert.</param>
        /// <param name="mode">Mode that provides the relative root.</param>
        /// <param name="projectPreferencesRoot">Absolute root used by ProjectPreferences mode.</param>
        /// <param name="relativePath">Converted relative path when successful.</param>
        /// <returns>True when the absolute path is inside the selected root.</returns>
        public static bool TryMake(
            string absolutePath,
            RelativePathMode mode,
            string projectPreferencesRoot,
            out RelativePath relativePath)
        {
            relativePath = default;

            if (string.IsNullOrWhiteSpace(absolutePath))
                return false;

            var root = RootForMode(mode, projectPreferencesRoot);

            if (string.IsNullOrWhiteSpace(root))
                return false;

            root = System.IO.Path.GetFullPath(root);
            var fullPath = System.IO.Path.GetFullPath(absolutePath);

            if (!SamePath(fullPath, root) && !IsInside(fullPath, root))
                return false;

            relativePath = new RelativePath
            {
                Mode = mode,
                Path = SamePath(fullPath, root)
                    ? string.Empty
                    : fullPath.Substring(RootPrefix(root).Length).Replace('\\', '/')
            };
            return true;
        }

        /// <summary>
        /// Converts this path to a Unity Assets/... path when it resolves inside the Assets folder.
        /// </summary>
        /// <param name="projectPreferencesRoot">Absolute root used by ProjectPreferences mode.</param>
        /// <param name="assetPath">Unity asset path when successful.</param>
        /// <returns>True when the resolved path is inside the Assets folder.</returns>
        public bool TryGetAssetPath(string projectPreferencesRoot, out string assetPath)
        {
            assetPath = null;

            if (!TryGetAbsolutePath(projectPreferencesRoot, out var fullPath))
                return false;

            var assetsRoot = System.IO.Path.GetFullPath(Application.dataPath);

            if (!SamePath(fullPath, assetsRoot) && !IsInside(fullPath, assetsRoot))
                return false;

            assetPath = SamePath(fullPath, assetsRoot)
                ? "Assets"
                : "Assets/" + fullPath.Substring(RootPrefix(assetsRoot).Length).Replace('\\', '/');
            return true;
        }

        static string RootForMode(RelativePathMode mode, string projectPreferencesRoot) =>
            mode switch
            {
                RelativePathMode.ProjectPreferences => projectPreferencesRoot,
                RelativePathMode.ProjectAssets => Application.dataPath,
                RelativePathMode.ProjectRoot => Directory.GetParent(Application.dataPath).FullName,
                _ => null
            };

        static string AssetRelativePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || assetPath == "Assets")
                return null;

            return assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;
        }

        static bool IsInside(string path, string root) =>
            path.StartsWith(RootPrefix(root), StringComparison.OrdinalIgnoreCase);

        static string RootPrefix(string root) =>
            root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
            + System.IO.Path.DirectorySeparatorChar;

        static bool SamePath(string a, string b) => string.Equals(
            a.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
            b.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Root mode used by RelativePath.
    /// </summary>
    public enum RelativePathMode
    {
        ProjectPreferences,
        ProjectAssets,
        ProjectRoot
    }
}
