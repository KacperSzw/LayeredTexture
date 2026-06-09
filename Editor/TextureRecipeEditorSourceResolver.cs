using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Editor resolver for TextureFileLayer sources backed by Unity assets or external image files.
    /// </summary>
    sealed class TextureRecipeEditorSourceResolver : ITextureSourceResolver
    {
        internal static readonly TextureRecipeEditorSourceResolver Instance = new();
        readonly Dictionary<string, CachedTexture> externalTextures = new();

        static TextureRecipeEditorSourceResolver()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Instance.ClearExternalTextures;
        }

        /// <summary>
        /// Resolves a serialized file source to a Unity asset texture or cached external texture.
        /// </summary>
        /// <param name="_">Recipe being evaluated; unused by the editor resolver.</param>
        /// <param name="source">Texture source to resolve.</param>
        /// <param name="texture">Resolved texture when successful.</param>
        /// <returns>True when the source resolves to a supported texture.</returns>
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

        /// <summary>
        /// Converts an AssetPath to an Assets/... project path when it points inside the Unity Assets folder.
        /// </summary>
        /// <param name="path">Source path to convert.</param>
        /// <param name="assetPath">Unity asset path when conversion succeeds.</param>
        /// <returns>True when the source path resolves to a Unity asset path.</returns>
        internal static bool TryGetAssetPath(AssetPath path, out string assetPath)
        {
            assetPath = null;
            string relativeRoot = null;

            if (path.Mode == AssetPathMode.Relative
                && !LayeredTexturePreferences.TryGetRelativeRoot(out relativeRoot))
                return false;

            return path.TryGetUnityAssetPath(relativeRoot, out assetPath);
        }

        /// <summary>
        /// Resolves an AssetPath using the configured editor relative root when needed.
        /// </summary>
        /// <param name="path">Source path to resolve.</param>
        /// <param name="absolutePath">Absolute filesystem path when resolution succeeds.</param>
        /// <returns>True when the path is valid for its mode.</returns>
        internal static bool TryGetAbsolutePath(AssetPath path, out string absolutePath)
        {
            absolutePath = null;
            string relativeRoot = null;

            if (path.Mode == AssetPathMode.Relative
                && !LayeredTexturePreferences.TryGetRelativeRoot(out relativeRoot))
                return false;

            return path.TryGetAbsolutePath(relativeRoot, out absolutePath);
        }

        /// <summary>
        /// Converts a Unity asset path into the selected TextureSource path mode.
        /// </summary>
        /// <param name="assetPath">Unity Assets/... path.</param>
        /// <param name="mode">Serialized path mode to create.</param>
        /// <param name="sourcePath">Texture source path when conversion succeeds.</param>
        /// <returns>True when the asset path can be represented in the requested mode.</returns>
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

        struct CachedTexture
        {
            public long WriteTicks;
            public Texture2D Texture;
        }
    }
}
