using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    sealed class TextureRecipeEditorSourceResolver : ITextureSourceResolver
    {
        internal static readonly TextureRecipeEditorSourceResolver Instance = new();

        public bool TryResolve(TextureRecipe recipe, TextureSource source, out Texture texture)
        {
            texture = null;

            if (source.Kind != TextureSourceKind.ProjectAssetRawFile)
                return false;

            if (!TryGetAssetPath(recipe, source.ProjectAssetPath, out var assetPath))
                return false;

            texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            return texture != null;
        }

        internal static bool TryGetAssetPath(TextureRecipe recipe, string relativePath, out string assetPath)
        {
            assetPath = null;

            if (recipe == null || string.IsNullOrWhiteSpace(relativePath))
                return false;

            if (!TryGetSourceDirectoryFullPath(recipe.SourceDirectory, out var sourceFullPath))
                return false;

            var relative = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(sourceFullPath, relative));

            if (!IsInside(fullPath, sourceFullPath))
                return false;

            var assetsFullPath = Path.GetFullPath(Application.dataPath);

            if (!IsInside(fullPath, assetsFullPath))
                return false;

            assetPath = "Assets" + fullPath.Substring(assetsFullPath.Length).Replace('\\', '/');
            return true;
        }

        internal static bool TryMakeSourceRelativePath(TextureRecipe recipe, string assetPath, out string relativePath)
        {
            relativePath = null;

            if (recipe == null || string.IsNullOrWhiteSpace(assetPath))
                return false;

            if (!TryGetSourceDirectoryFullPath(recipe.SourceDirectory, out var sourceFullPath))
                return false;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var assetFullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsInside(assetFullPath, sourceFullPath))
                return false;

            var prefix = sourceFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            relativePath = assetFullPath.Substring(prefix.Length).Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        internal static bool TryMakeSourceDirectory(
            string fullPath,
            RelativePathMode mode,
            out RelativePath sourceDirectory)
        {
            var projectPreferencesRoot = ProjectPreferencesRootFor(mode);
            return RelativePath.TryMake(fullPath, mode, projectPreferencesRoot, out sourceDirectory);
        }

        static bool TryGetSourceDirectoryFullPath(RelativePath sourceDirectory, out string fullPath)
        {
            var projectPreferencesRoot = ProjectPreferencesRootFor(sourceDirectory.Mode);
            return sourceDirectory.TryGetAbsolutePath(projectPreferencesRoot, out fullPath);
        }

        static string ProjectPreferencesRootFor(RelativePathMode mode)
        {
            if (mode != RelativePathMode.ProjectPreferences)
                return null;

            return LayeredTextureProjectPreferences.TryGetSourceRootAbsolutePath(out var root)
                ? root
                : null;
        }

        static bool IsInside(string path, string root)
        {
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        static bool SamePath(string a, string b) => string.Equals(
            a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
