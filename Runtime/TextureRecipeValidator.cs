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
        public static bool ValidateRuntime(TextureRecipe recipe) => ValidateRuntime(recipe, out _);

        /// <summary>
        /// Validates compute support, output settings, supported layers, and recursive mask references.
        /// </summary>
        /// <param name="recipe">Recipe to validate.</param>
        /// <param name="error">Failure reason when validation fails.</param>
        /// <returns>True when the recipe can be evaluated at runtime.</returns>
        public static bool ValidateRuntime(TextureRecipe recipe, out string error)
        {
            if (!SystemInfo.supportsComputeShaders)
                return Fail("LayeredTexture requires compute shader support.", out error);

            return ValidateRecipe(
                recipe,
                recipe != null ? recipe.Output : default,
                new System.Collections.Generic.HashSet<TextureRecipe>(),
                out error);
        }

        /// <summary>
        /// Validates a recipe using output settings supplied by the caller.
        /// </summary>
        /// <param name="recipe">Recipe to validate.</param>
        /// <param name="output">Output settings to use for the root recipe evaluation.</param>
        /// <returns>True when the recipe can be evaluated at runtime.</returns>
        public static bool ValidateRuntime(TextureRecipe recipe, OutputProfile output) =>
            ValidateRuntime(recipe, output, out _);

        /// <summary>
        /// Validates a recipe using output settings supplied by the caller.
        /// </summary>
        /// <param name="recipe">Recipe to validate.</param>
        /// <param name="output">Output settings to use for the root recipe evaluation.</param>
        /// <param name="error">Failure reason when validation fails.</param>
        /// <returns>True when the recipe can be evaluated at runtime.</returns>
        public static bool ValidateRuntime(TextureRecipe recipe, OutputProfile output, out string error)
        {
            if (!SystemInfo.supportsComputeShaders)
                return Fail("LayeredTexture requires compute shader support.", out error);

            return ValidateRecipe(recipe, output, new System.Collections.Generic.HashSet<TextureRecipe>(), out error);
        }

        public static bool ValidateRuntime(LayerStack stack, OutputProfile output) =>
            ValidateRuntime(stack, output, out _);

        public static bool ValidateRuntime(LayerStack stack, OutputProfile output, out string error)
        {
            if (!SystemInfo.supportsComputeShaders)
                return Fail("LayeredTexture requires compute shader support.", out error);

            var valid = ValidateOutput(output, out error);

            if (!ValidateStack(
                    stack,
                    output.Resolution,
                    new System.Collections.Generic.HashSet<TextureRecipe>(),
                    out var stackError))
            {
                if (error == null)
                    error = stackError;

                valid = false;
            }

            return valid;
        }

        static bool ValidateRecipe(
            TextureRecipe recipe,
            OutputProfile output,
            System.Collections.Generic.HashSet<TextureRecipe> visiting,
            out string error)
        {
            if (recipe == null)
                return Fail("TextureRecipe is missing.", out error);

            if (!visiting.Add(recipe))
                return Fail("TextureRecipe mask reference cycle detected.", out error);

            try
            {
                var valid = ValidateOutput(output, out error);

                if (!ValidateStack(recipe.RootStack, output.Resolution, visiting, out var stackError))
                {
                    if (error == null)
                        error = stackError;

                    valid = false;
                }

                return valid;
            }
            finally
            {
                visiting.Remove(recipe);
            }
        }

        static bool ValidateOutput(OutputProfile output, out string error)
        {
            error = null;

            if (output.Resolution.x <= 0 || output.Resolution.y <= 0)
                return Fail("TextureRecipe.Output.Resolution must be positive.", out error);

            if (output.WorkingFormat == GraphicsFormat.None)
                return Fail("TextureRecipe.Output.WorkingFormat is invalid.", out error);
            else if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.Render))
                return Fail($"TextureRecipe.Output.WorkingFormat is not renderable: {output.WorkingFormat}.", out error);
            else if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.LoadStore))
                return Fail(
                    $"TextureRecipe.Output.WorkingFormat does not support compute writes: {output.WorkingFormat}.",
                    out error);

            return true;
        }

        static bool ValidateStack(
            LayerStack stack,
            Vector2Int resolution,
            System.Collections.Generic.HashSet<TextureRecipe> visiting,
            out string error)
        {
            if (stack == null)
                return Fail("TextureRecipe.RootStack is missing.", out error);

            var valid = true;
            error = null;

            if (stack.Layers == null)
                return Fail("LayerStack.Layers is missing.", out error);

            for (var i = 0; i < stack.Layers.Count; i++)
            {
                var layer = stack.Layers[i];

                if (layer == null)
                {
                    if (error == null)
                        error = "LayerStack contains a missing layer.";

                    valid = false;
                    continue;
                }

                if (!layer.Enabled)
                    continue;

                if (!ValidateMask(layer.Mask, visiting, out var maskError))
                {
                    if (error == null)
                        error = maskError;

                    valid = false;
                }

                if (!ValidateLayer(layer, resolution, visiting, out var layerError))
                {
                    if (error == null)
                        error = layerError;

                    valid = false;
                }
            }

            return valid;
        }

        static bool ValidateLayer(
            TextureLayerBase layer,
            Vector2Int resolution,
            System.Collections.Generic.HashSet<TextureRecipe> visiting,
            out string error)
        {
            switch (layer)
            {
                case SolidColorLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.SolidColorKernel, out _, out _, out error);
                case TextureFileLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.TextureFileKernel, out _, out _, out error);
                case NoiseLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.NoiseKernel, out _, out _, out error);
                case WarpLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.WarpKernel, out _, out _, out error);
                case NormalFromHeightLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.NormalFromHeightKernel, out _, out _, out error);
                case WaterWavesLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.WaterWavesKernel, out _, out _, out error);
                case BlurLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.BlurHorizontalKernel, out _, out _, out error)
                        && LayerCompute.TryGetKernel(LayerCompute.BlurVerticalKernel, out _, out _, out error);
                case TransformLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.TransformKernel, out _, out _, out error);
                case InvertLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.InvertKernel, out _, out _, out error);
                case HistogramSelectLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.HistogramSelectKernel, out _, out _, out error);
                case SaturationLayer:
                    return LayerCompute.TryGetKernel(LayerCompute.SaturationKernel, out _, out _, out error);
                case SignedDistanceFieldLayer:
                    return SignedDistanceFieldCompute.TryGetKernel(out _, out _, out error);
                case RecipeReferenceLayer recipeReferenceLayer:
                    return ValidateRecipeReferenceLayer(recipeReferenceLayer, visiting, out error);
                default:
                    return Fail($"{layer.GetType().Name} is not supported at runtime.", out error);
            }
        }

        static bool ValidateRecipeReferenceLayer(
            RecipeReferenceLayer layer,
            System.Collections.Generic.HashSet<TextureRecipe> visiting,
            out string error)
        {
            if (layer.Recipe == null)
            {
                error = null;
                return true;
            }

            if (!LayerCompute.TryGetKernel(LayerCompute.TextureFileKernel, out _, out _, out error))
                return false;

            return ValidateRecipe(layer.Recipe, layer.Recipe.Output, visiting, out error);
        }

        static bool ValidateMask(StackMask mask, System.Collections.Generic.HashSet<TextureRecipe> visiting, out string error)
        {
            if (mask == null || mask.RecipeReference == null)
            {
                error = null;
                return true;
            }

            return ValidateRecipe(mask.RecipeReference, mask.RecipeReference.Output, visiting, out error);
        }

        static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
