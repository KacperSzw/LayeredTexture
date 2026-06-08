using System;
using System.IO;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    static class LayeredTextureBakeUtility
    {
        internal static bool TryGetOutputPath(
            string outputPath,
            string label,
            string expectedExtension,
            string extensionContext,
            string unsupportedExtensionError,
            out string assetPath,
            out string fullPath,
            out string error)
        {
            assetPath = null;
            fullPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                error = $"{label} is missing.";
                return false;
            }

            assetPath = outputPath.Replace('\\', '/');

            if (Path.IsPathRooted(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"{label} must be under Assets/.";
                return false;
            }

            if (expectedExtension == null)
            {
                error = unsupportedExtensionError;
                return false;
            }

            if (!string.Equals(Path.GetExtension(assetPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = string.IsNullOrEmpty(extensionContext)
                    ? $"{label} extension must be {expectedExtension}."
                    : $"{label} extension must be {expectedExtension} for {extensionContext}.";
                return false;
            }

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));

            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = $"{label} must be under Assets/.";
                return false;
            }

            return true;
        }

        internal static Texture2D ReadBack(RenderTexture renderTexture, TextureFormat format) =>
            ReadBack(renderTexture, format, false);

        internal static Texture2D ReadBack(RenderTexture renderTexture, TextureFormat format, bool mipChain)
        {
            var texture = new Texture2D(renderTexture.width, renderTexture.height, format, mipChain, true);
            var active = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply(mipChain, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = active;
            }
        }

        internal static void Release(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
