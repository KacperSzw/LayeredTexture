using System;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Placeholder layer for recipe-as-layer evaluation; currently unsupported at runtime.
    /// </summary>
    [Serializable]
    public sealed class RecipeReferenceLayer : TextureLayerBase
    {
        /// <summary>
        /// Referenced recipe intended to be evaluated as a layer.
        /// </summary>
        public TextureRecipe Recipe;

        /// <inheritdoc />
        public override void Process(BakeContext ctx) => throw new NotImplementedException();
    }
}
