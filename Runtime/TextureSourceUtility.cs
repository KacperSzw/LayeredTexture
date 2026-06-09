using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Resolves serialized texture sources into sampleable Unity texture objects.
    /// </summary>
    static class TextureSourceUtility
    {
        internal static bool CanSample(TextureSource source, Vector2Int resolution)
        {
            if (source.Kind != TextureSourceKind.RuntimeTextureReference)
                return false;

            return CanSample(source.RuntimeTexture);
        }

        /// <summary>
        /// Resolves direct runtime references or delegates file-backed sources to a resolver.
        /// </summary>
        /// <param name="recipe">Recipe being evaluated, used by custom resolvers for context.</param>
        /// <param name="source">Serialized texture source to resolve.</param>
        /// <param name="resolver">Optional resolver for non-runtime texture source kinds.</param>
        /// <param name="texture">Sampleable texture when resolution succeeds.</param>
        /// <returns>True when the resolved texture can be sampled by layer kernels.</returns>
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
