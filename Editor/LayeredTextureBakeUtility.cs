using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Shared editor bake helpers for render texture readback and cleanup.
    /// </summary>
    static class LayeredTextureBakeUtility
    {
        /// <summary>
        /// Copies a render texture into a readable Texture2D without mipmaps.
        /// </summary>
        /// <param name="renderTexture">Render texture to read from.</param>
        /// <param name="format">Texture2D format used for readback.</param>
        /// <returns>Readable texture owned by the caller.</returns>
        internal static Texture2D ReadBack(RenderTexture renderTexture, TextureFormat format) =>
            ReadBack(renderTexture, format, false);

        /// <summary>
        /// Copies a render texture into a readable Texture2D.
        /// </summary>
        /// <param name="renderTexture">Render texture to read from.</param>
        /// <param name="format">Texture2D format used for readback.</param>
        /// <param name="mipChain">Whether the created texture includes and updates mipmaps.</param>
        /// <returns>Readable texture owned by the caller.</returns>
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
