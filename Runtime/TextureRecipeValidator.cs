using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Runtime validation for TextureRecipe assets.
    /// </summary>
    public static class TextureRecipeValidator
    {
        /// <summary>
        /// Validates compute support, output settings, supported layers, and recursive mask references.
        /// </summary>
        /// <param name="recipe">Recipe to validate.</param>
        /// <returns>True when the recipe can be evaluated at runtime.</returns>
        public static bool ValidateRuntime(TextureRecipe recipe)
        {
            if (!SystemInfo.supportsComputeShaders)
                return Fail("LayeredTexture requires compute shader support.");

            return ValidateRecipe(
                recipe,
                recipe != null ? recipe.Output : default,
                new System.Collections.Generic.HashSet<TextureRecipe>());
        }

        /// <summary>
        /// Validates a recipe using output settings supplied by the caller.
        /// </summary>
        /// <param name="recipe">Recipe to validate.</param>
        /// <param name="output">Output settings to use for the root recipe evaluation.</param>
        /// <returns>True when the recipe can be evaluated at runtime.</returns>
        public static bool ValidateRuntime(TextureRecipe recipe, OutputProfile output)
        {
            if (!SystemInfo.supportsComputeShaders)
                return Fail("LayeredTexture requires compute shader support.");

            return ValidateRecipe(recipe, output, new System.Collections.Generic.HashSet<TextureRecipe>());
        }

        static bool ValidateRecipe(
            TextureRecipe recipe,
            OutputProfile output,
            System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            if (recipe == null)
                return Fail("TextureRecipe is missing.");

            if (!visiting.Add(recipe))
                return Fail("TextureRecipe mask reference cycle detected.");

            try
            {
                var valid = ValidateOutput(output);
                valid &= ValidateStack(recipe.RootStack, output.Resolution, visiting);
                return valid;
            }
            finally
            {
                visiting.Remove(recipe);
            }
        }

        static bool ValidateOutput(OutputProfile output)
        {
            var valid = true;

            if (output.Resolution.x <= 0 || output.Resolution.y <= 0)
                valid &= Fail("TextureRecipe.Output.Resolution must be positive.");

            if (output.WorkingFormat == GraphicsFormat.None)
                valid &= Fail("TextureRecipe.Output.WorkingFormat is invalid.");
            else if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.Render))
                valid &= Fail($"TextureRecipe.Output.WorkingFormat is not renderable: {output.WorkingFormat}.");
            else if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.LoadStore))
                valid &= Fail($"TextureRecipe.Output.WorkingFormat does not support compute writes: {output.WorkingFormat}.");

            return valid;
        }

        static bool ValidateStack(
            LayerStack stack,
            Vector2Int resolution,
            System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            if (stack == null)
                return Fail("TextureRecipe.RootStack is missing.");

            var valid = true;

            if (stack.Layers == null)
                return Fail("LayerStack.Layers is missing.");

            for (var i = 0; i < stack.Layers.Count; i++)
            {
                var layer = stack.Layers[i];

                if (layer == null)
                {
                    valid &= Fail("LayerStack contains a missing layer.");
                    continue;
                }

                if (!layer.Enabled)
                    continue;

                valid &= ValidateMask(layer.Mask, visiting);
                valid &= ValidateLayer(layer, resolution, visiting);
            }

            return valid;
        }

        static bool ValidateLayer(
            TextureLayerBase layer,
            Vector2Int resolution,
            System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            switch (layer)
            {
                case SolidColorLayer:
                    return SolidColorLayer.TryGetShaderKernel(out _, out _, out var error)
                        ? true
                        : Fail(error);
                case TextureFileLayer:
                    return TextureFileLayer.TryGetShaderKernel(out _, out _, out error)
                        ? true
                        : Fail(error);
                case NoiseLayer:
                    return NoiseLayer.TryGetShaderKernel(out _, out _, out error)
                        ? true
                        : Fail(error);
                case NormalFromHeightLayer:
                    return NormalFromHeightLayer.TryGetShaderKernel(out _, out _, out error)
                        ? true
                        : Fail(error);
                case WaterWavesLayer:
                    return WaterWavesLayer.TryGetShaderKernel(out _, out _, out error)
                        ? true
                        : Fail(error);
                case RecipeReferenceLayer recipeReferenceLayer:
                    return ValidateRecipeReferenceLayer(recipeReferenceLayer, visiting);
                default:
                    return Fail($"{layer.GetType().Name} is not supported at runtime.");
            }
        }

        static bool ValidateRecipeReferenceLayer(
            RecipeReferenceLayer layer,
            System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            if (layer.Recipe == null)
                return true;

            return TextureFileLayer.TryGetShaderKernel(out _, out _, out var error)
                ? ValidateRecipe(layer.Recipe, layer.Recipe.Output, visiting)
                : Fail(error);
        }

        static bool ValidateMask(StackMask mask, System.Collections.Generic.HashSet<TextureRecipe> visiting)
        {
            if (mask == null || mask.RecipeReference == null)
                return true;

            return ValidateRecipe(mask.RecipeReference, mask.RecipeReference.Output, visiting);
        }

        static bool Fail(string message)
        {
            Debug.LogError(message);
            return false;
        }
    }
}
