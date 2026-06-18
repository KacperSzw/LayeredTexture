using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Editor-only evaluator for full recipe previews.
    /// </summary>
    public static class TextureRecipePreviewEvaluator
    {
        public static RenderTexture Evaluate(TextureRecipe recipe, ITextureSourceResolver sourceResolver = null)
        {
            if (recipe == null)
                return null;

            return TextureRecipeEvaluator.Evaluate(recipe, PreviewOutput(recipe.Output), sourceResolver);
        }

        public static RenderTexture Evaluate(
            LayerStack stack,
            OutputProfile output,
            ITextureSourceResolver sourceResolver = null) =>
            TextureRecipeEvaluator.Evaluate(stack, PreviewOutput(output), sourceResolver);

        static OutputProfile PreviewOutput(OutputProfile output)
        {
            output.GenerateMips = false;
            return output;
        }
    }
}
