using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unmanaged.LayeredTexture
{
    public static class TextureRecipeValidator
    {
        public static bool ValidateRuntime(TextureRecipe recipe)
        {
            if (!SystemInfo.supportsComputeShaders)
                return Fail("LayeredTexture requires compute shader support.");

            return ValidateRecipe(recipe);
        }

        static bool ValidateRecipe(TextureRecipe recipe)
        {
            if (recipe == null)
                return Fail("TextureRecipe is missing.");

            var valid = ValidateOutput(recipe.Output);
            valid &= ValidateStack(recipe.RootStack);
            return valid;
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

        static bool ValidateStack(LayerStack stack)
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

                valid &= ValidateMask(layer.Mask);
                valid &= ValidateLayer(layer);
            }

            return valid;
        }

        static bool ValidateLayer(TextureLayerBase layer)
        {
            switch (layer)
            {
                case SolidColorLayer:
                    return SolidColorLayer.TryGetShaderKernel(out _, out _, out var error)
                        ? true
                        : Fail(error);
                case TextureFileLayer:
                    return Fail("TextureFileLayer is not supported at runtime.");
                case ChannelPackLayer:
                    return Fail("ChannelPackLayer is not supported at runtime.");
                case ChannelFillLayer:
                    return Fail("ChannelFillLayer is not supported at runtime.");
                case RecipeReferenceLayer:
                    return Fail("RecipeReferenceLayer is not supported at runtime.");
                default:
                    return Fail($"{layer.GetType().Name} is not supported at runtime.");
            }
        }

        static bool ValidateMask(StackMask mask)
        {
            if (mask == null || mask.Source == StackSource.None)
                return true;

            return Fail("Layer masks are not supported at runtime.");
        }

        static bool Fail(string message)
        {
            Debug.LogError(message);
            return false;
        }
    }
}
