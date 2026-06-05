using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    static class TextureSourceUtility
    {
        internal static bool CanSample(TextureSource source, Vector2Int resolution)
        {
            if (source.Kind != TextureSourceKind.RuntimeTextureReference)
                return false;

            return CanSample(source.RuntimeTexture);
        }

        internal static bool TryResolve(
            TextureRecipe recipe,
            TextureSource source,
            ITextureSourceResolver resolver,
            out Texture texture)
        {
            if (source.Kind != TextureSourceKind.RuntimeTextureReference || source.RuntimeTexture == null)
            {
                texture = null;

                if (resolver == null)
                    return false;

                return resolver.TryResolve(recipe, source, out texture) && CanSample(texture);
            }

            texture = source.RuntimeTexture;
            return CanSample(texture);
        }

        internal static bool CanSample(Texture texture) => texture is Texture2D or RenderTexture;
    }
}
