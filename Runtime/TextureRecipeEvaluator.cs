using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    public static class TextureRecipeEvaluator
    {
        public static bool ValidateRuntime(TextureRecipe recipe) => TextureRecipeValidator.ValidateRuntime(recipe);

        public static RenderTexture Evaluate(TextureRecipe recipe)
        {
            if (!ValidateRuntime(recipe))
                return null;

            using var ctx = new BakeContext(recipe.Output);

            ctx.ClearCurrent(Color.clear);

            for (var i = 0; i < recipe.RootStack.Layers.Count; i++)
            {
                var layer = recipe.RootStack.Layers[i];

                if (!layer.Enabled)
                    continue;

                ctx.SwapCurrentToPrevious();
                layer.Process(ctx);
            }

            return ctx.DetachCurrent();
        }
    }
}
