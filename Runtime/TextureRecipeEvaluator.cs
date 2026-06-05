using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Validates and evaluates TextureRecipe assets into transient render textures.
    /// </summary>
    public static class TextureRecipeEvaluator
    {
        /// <summary>
        /// Validates whether a recipe can be evaluated in the current runtime environment.
        /// </summary>
        /// <param name="recipe">Recipe to validate.</param>
        /// <returns>True when the recipe can be evaluated.</returns>
        public static bool ValidateRuntime(TextureRecipe recipe) => TextureRecipeValidator.ValidateRuntime(recipe);

        /// <summary>
        /// Evaluates a recipe using runtime texture references only.
        /// </summary>
        /// <param name="recipe">Recipe to evaluate.</param>
        /// <returns>New render texture owned by the caller, or null when validation/evaluation fails.</returns>
        public static RenderTexture Evaluate(TextureRecipe recipe) => Evaluate(recipe, null);

        /// <summary>
        /// Evaluates a recipe with an optional resolver for editor or custom texture source kinds.
        /// </summary>
        /// <param name="recipe">Recipe to evaluate.</param>
        /// <param name="sourceResolver">Optional resolver for non-runtime texture sources.</param>
        /// <returns>New render texture owned by the caller, or null when validation/evaluation fails.</returns>
        public static RenderTexture Evaluate(TextureRecipe recipe, ITextureSourceResolver sourceResolver)
        {
            if (!ValidateRuntime(recipe))
                return null;

            return EvaluateRecipe(recipe, sourceResolver, new System.Collections.Generic.HashSet<TextureRecipe>());
        }

        static RenderTexture EvaluateRecipe(
            TextureRecipe recipe,
            ITextureSourceResolver sourceResolver,
            System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            if (recipe == null)
                return null;

            if (!visiting.Add(recipe))
            {
                Debug.LogError("TextureRecipe mask reference cycle detected.");
                return null;
            }

            try
            {
                using var ctx = new BakeContext(recipe.Output, recipe, sourceResolver);

                ctx.ClearCurrent(Color.clear);

                for (var i = 0; i < recipe.RootStack.Layers.Count; i++)
                {
                    var layer = recipe.RootStack.Layers[i];

                    if (!layer.Enabled)
                        continue;

                    if (!CanProcess(layer, ctx))
                        continue;

                    if (!PrepareMask(ctx, layer.Mask, sourceResolver, visiting))
                        return null;

                    ctx.SwapCurrentToPrevious();
                    layer.Process(ctx);
                }

                return ctx.DetachCurrent();
            }
            finally
            {
                visiting.Remove(recipe);
            }
        }

        static bool PrepareMask(
            BakeContext ctx,
            StackMask stackMask,
            ITextureSourceResolver sourceResolver,
            System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            if (stackMask == null || stackMask.RecipeReference == null)
            {
                ctx.ClearMask();
                return true;
            }

            var mask = EvaluateRecipe(stackMask.RecipeReference, sourceResolver, visiting);

            if (mask == null)
                return false;

            ctx.SetMask(mask, stackMask);
            return true;
        }

        static bool CanProcess(TextureLayerBase layer, BakeContext ctx) =>
            layer switch
            {
                TextureFileLayer textureFileLayer => TextureSourceUtility.TryResolve(
                    ctx.recipe,
                    textureFileLayer.Source,
                    ctx.sourceResolver,
                    out _),
                _ => true
            };
    }
}
