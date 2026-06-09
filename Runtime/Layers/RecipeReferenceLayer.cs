using System;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Layer that samples another TextureRecipe and composites it into the stack.
    /// </summary>
    [Serializable]
    public sealed class RecipeReferenceLayer : TextureLayerBase
    {
        /// <summary>
        /// Referenced recipe intended to be evaluated as a layer.
        /// </summary>
        public TextureRecipe Recipe;

        /// <inheritdoc />
        public override TextureLayerRole Role => TextureLayerRole.Source;

        /// <inheritdoc />
        public override bool SupportsRawPreview => true;

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            if (Recipe == null)
                return;

            LayerCompute.GetKernelOrThrow(LayerCompute.TextureFileKernel, out var shader, out var kernel);
            var texture = TextureRecipeEvaluator.Evaluate(Recipe, ctx.sourceResolver);

            if (texture == null)
                return;

            try
            {
                LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
                shader.SetTexture(kernel, LayerCompute.SourceId, texture);
                LayerCompute.Dispatch(shader, kernel, ctx);
            }
            finally
            {
                BakeContext.Release(texture);
            }
        }
    }
}
