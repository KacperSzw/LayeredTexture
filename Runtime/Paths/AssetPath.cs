using System;
using System.IO;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Serializable file path that resolves either directly or relative to a caller-provided root.
    /// </summary>
    [Serializable]
    public struct AssetPath
    {
        /// <summary>
        /// Path resolution mode.
        /// </summary>
        public AssetPathMode Mode;

        /// <summary>
        /// Relative or absolute filesystem path.
        /// </summary>
        public string Path;

        /// <summary>
        /// Creates a relative asset path.
        /// </summary>
        /// <param name="path">Path relative to the caller-provided root.</param>
        /// <returns>Relative asset path value.</returns>
        public static AssetPath Relative(string path = null) => new()
        {
            Mode = AssetPathMode.Relative,
            Path = Normalize(path)
        };

        /// <summary>
        /// Creates an absolute asset path.
        /// </summary>
        /// <param name="path">Absolute filesystem path.</param>
        /// <returns>Absolute asset path value.</returns>
        public static AssetPath Absolute(string path) => new()
        {
            Mode = AssetPathMode.Absolute,
            Path = Normalize(path)
        };

        /// <summary>
        /// Resolves this value to an absolute filesystem path.
        /// </summary>
        /// <param name="relativeRoot">Root used when Mode is Relative.</param>
        /// <param name="absolutePath">Resolved absolute path when successful.</param>
        /// <returns>True when the path is valid for its mode.</returns>
        public bool TryGetAbsolutePath(string relativeRoot, out string absolutePath)
        {
            absolutePath = null;

            if (Mode == AssetPathMode.Absolute)
            {
                if (!System.IO.Path.IsPathRooted(Path ?? string.Empty))
                    return false;

                absolutePath = System.IO.Path.GetFullPath(Path);
                return true;
            }

            if (System.IO.Path.IsPathRooted(Path ?? string.Empty) || string.IsNullOrWhiteSpace(relativeRoot))
                return false;

            var root = System.IO.Path.GetFullPath(relativeRoot);
            absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, Path ?? string.Empty));

            if (SamePath(absolutePath, root) || IsInside(absolutePath, root))
                return true;

            absolutePath = null;
            return false;
        }

        /// <summary>
        /// Returns whether this path resolves to an existing directory.
        /// </summary>
        /// <param name="relativeRoot">Root used when Mode is Relative.</param>
        /// <returns>True when the resolved path exists as a directory.</returns>
        public bool IsValidDirectory(string relativeRoot) =>
            TryGetAbsolutePath(relativeRoot, out var absolutePath) && Directory.Exists(absolutePath);

        /// <summary>
        /// Converts an absolute filesystem path into an AssetPath.
        /// </summary>
        /// <param name="absolutePath">Absolute filesystem path to convert.</param>
        /// <param name="relativeRoot">Root used when mode is Relative.</param>
        /// <param name="mode">Resulting path mode.</param>
        /// <param name="assetPath">Converted path when successful.</param>
        /// <returns>True when the path can be represented by the requested mode.</returns>
        public static bool TryMake(
            string absolutePath,
            string relativeRoot,
            AssetPathMode mode,
            out AssetPath assetPath)
        {
            assetPath = default;

            if (string.IsNullOrWhiteSpace(absolutePath))
                return false;

            if (!System.IO.Path.IsPathRooted(absolutePath))
                return false;

            var fullPath = System.IO.Path.GetFullPath(absolutePath);

            if (mode == AssetPathMode.Absolute)
            {
                assetPath = Absolute(fullPath);
                return true;
            }

            if (string.IsNullOrWhiteSpace(relativeRoot))
                return false;

            var root = System.IO.Path.GetFullPath(relativeRoot);

            if (!SamePath(fullPath, root) && !IsInside(fullPath, root))
                return false;

            assetPath = Relative(SamePath(fullPath, root)
                ? string.Empty
                : fullPath.Substring(RootPrefix(root).Length));
            return true;
        }

        /// <summary>
        /// Converts this value to an Assets/... path when it resolves inside the Unity Assets folder.
        /// </summary>
        /// <param name="relativeRoot">Root used when Mode is Relative.</param>
        /// <param name="assetPath">Unity asset path when successful.</param>
        /// <returns>True when the resolved path is inside Assets.</returns>
        public bool TryGetUnityAssetPath(string relativeRoot, out string assetPath)
        {
            assetPath = null;

            if (!TryGetAbsolutePath(relativeRoot, out var fullPath))
                return false;

            var assetsRoot = System.IO.Path.GetFullPath(Application.dataPath);

            if (!SamePath(fullPath, assetsRoot) && !IsInside(fullPath, assetsRoot))
                return false;

            assetPath = SamePath(fullPath, assetsRoot)
                ? "Assets"
                : "Assets/" + fullPath.Substring(RootPrefix(assetsRoot).Length).Replace('\\', '/');
            return true;
        }

        static string Normalize(string path) =>
            string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/');

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
    /// AssetPath resolution mode.
    /// </summary>
    public enum AssetPathMode
    {
        Relative,
        Absolute
    }
}
