using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Editor-only evaluator for raw per-layer preview thumbnails.
    /// </summary>
    public static class TextureLayerPreviewEvaluator
    {
        const int MaxPreviewSize = 96;

        /// <summary>
        /// Evaluates one source layer without blend, opacity, write-mask, or stack mask.
        /// </summary>
        /// <param name="recipe">Owning recipe used for resolution and source context.</param>
        /// <param name="layer">Layer to preview.</param>
        /// <returns>New render texture owned by the caller, or null when preview is unavailable.</returns>
        public static RenderTexture EvaluateRaw(TextureRecipe recipe, TextureLayerBase layer) =>
            EvaluateRaw(recipe, layer, recipe != null ? recipe.Output : default, TextureRecipeEditorSourceResolver.Instance);

        /// <summary>
        /// Evaluates one source layer with caller-supplied output and source resolver.
        /// </summary>
        /// <param name="recipe">Owning recipe used for source context.</param>
        /// <param name="layer">Layer to preview.</param>
        /// <param name="output">Output settings used as the preview baseline.</param>
        /// <param name="sourceResolver">Optional resolver for file-backed texture sources.</param>
        /// <returns>New render texture owned by the caller, or null when preview is unavailable.</returns>
        public static RenderTexture EvaluateRaw(
            TextureRecipe recipe,
            TextureLayerBase layer,
            OutputProfile output,
            ITextureSourceResolver sourceResolver)
        {
            if (recipe == null || layer == null || !layer.SupportsRawPreview)
                return null;

            output = PreviewOutput(output);

            if (output.Resolution.x <= 0 || output.Resolution.y <= 0)
                return null;

            BakeContext ctx = null;

            try
            {
                ctx = new BakeContext(output, recipe, sourceResolver)
                {
                    rawPreview = true
                };
                ctx.ClearCurrent(Color.clear);
                ctx.SwapCurrentToPrevious();
                layer.Process(ctx);
                return ctx.DetachCurrent();
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                ctx?.Dispose();
            }
        }

        static OutputProfile PreviewOutput(OutputProfile output)
        {
            var resolution = output.Resolution;

            if (resolution.x <= 0 || resolution.y <= 0)
                return output;

            var scale = Mathf.Min(
                1f,
                MaxPreviewSize / (float)Mathf.Max(resolution.x, resolution.y));

            output.Resolution = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(resolution.x * scale)),
                Mathf.Max(1, Mathf.RoundToInt(resolution.y * scale)));
            output.GenerateMips = false;
            return output;
        }
    }
}
