using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    sealed class TextureRecipeEditorSourceResolver : ITextureSourceResolver
    {
        internal static readonly TextureRecipeEditorSourceResolver Instance = new();
        readonly Dictionary<string, CachedTexture> externalTextures = new();

        static TextureRecipeEditorSourceResolver()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Instance.ClearExternalTextures;
        }

        public bool TryResolve(TextureRecipe _, TextureSource source, out Texture texture)
        {
            texture = null;

            if (source.Kind != TextureSourceKind.File)
                return false;

            if (!TryGetAbsolutePath(source.Path, out var fullPath))
                return false;

            if (TryGetAssetPath(source.Path, out var assetPath))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);

                if (texture != null)
                    return true;
            }

            return TryLoadExternalTexture(fullPath, out texture);
        }

        internal static bool TryGetAssetPath(AssetPath path, out string assetPath)
        {
            assetPath = null;

            if (!TryGetAbsolutePath(path, out var fullPath))
                return false;

            var assetsRoot = Path.GetFullPath(Application.dataPath);

            if (!SamePath(fullPath, assetsRoot) && !IsInside(fullPath, assetsRoot))
                return false;

            assetPath = SamePath(fullPath, assetsRoot)
                ? "Assets"
                : "Assets/" + fullPath.Substring(RootPrefix(assetsRoot).Length).Replace('\\', '/');
            return true;
        }

        internal static bool TryGetAbsolutePath(AssetPath path, out string absolutePath)
        {
            absolutePath = null;
            string relativeRoot = null;

            if (path.Mode == AssetPathMode.Relative
                && !LayeredTexturePreferences.TryGetRelativeRoot(out relativeRoot))
                return false;

            return path.TryGetAbsolutePath(relativeRoot, out absolutePath);
        }

        internal static bool TryMakeSourcePath(
            string assetPath,
            AssetPathMode mode,
            out AssetPath sourcePath)
        {
            sourcePath = default;

            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var assetFullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));

            return mode == AssetPathMode.Relative
                ? LayeredTexturePreferences.TryGetRelativeRoot(out var relativeRoot)
                  && AssetPath.TryMake(assetFullPath, relativeRoot, mode, out sourcePath)
                : AssetPath.TryMake(assetFullPath, null, mode, out sourcePath);
        }

        bool TryLoadExternalTexture(string fullPath, out Texture texture)
        {
            texture = null;

            if (!File.Exists(fullPath) || !TextureFileLoader.IsSupportedPath(fullPath))
                return false;

            var writeTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;

            if (externalTextures.TryGetValue(fullPath, out var cached)
                && cached.WriteTicks == writeTicks
                && cached.Texture != null)
            {
                texture = cached.Texture;
                return true;
            }

            if (cached.Texture != null)
                Object.DestroyImmediate(cached.Texture);

            if (!TextureFileLoader.TryLoad(fullPath, out var loadedTexture))
                return false;

            externalTextures[fullPath] = new CachedTexture
            {
                WriteTicks = writeTicks,
                Texture = loadedTexture
            };
            texture = loadedTexture;
            return true;
        }

        void ClearExternalTextures()
        {
            foreach (var cached in externalTextures.Values)
            {
                if (cached.Texture != null)
                    Object.DestroyImmediate(cached.Texture);
            }

            externalTextures.Clear();
        }

        static bool IsInside(string path, string root) =>
            path.StartsWith(RootPrefix(root), System.StringComparison.OrdinalIgnoreCase);

        static string RootPrefix(string root) =>
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        static bool SamePath(string a, string b) => string.Equals(
            a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            System.StringComparison.OrdinalIgnoreCase);

        struct CachedTexture
        {
            public long WriteTicks;
            public Texture2D Texture;
        }
    }
}
