using System.Collections.Generic;
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

            var recipes = new HashSet<TextureRecipe>();
            var recipeStack = new HashSet<TextureRecipe>();
            var stacks = new HashSet<LayerStack>();
            var stackStack = new HashSet<LayerStack>();

            return ValidateRecipe(recipe, recipes, recipeStack, stacks, stackStack);
        }

        static bool ValidateRecipe(
            TextureRecipe recipe,
            HashSet<TextureRecipe> recipes,
            HashSet<TextureRecipe> recipeStack,
            HashSet<LayerStack> stacks,
            HashSet<LayerStack> stackStack)
        {
            if (recipe == null)
                return Fail("TextureRecipe is missing.");

            if (recipeStack.Contains(recipe))
                return Fail("Recursive recipe reference detected.");

            if (recipes.Contains(recipe))
                return true;

            recipeStack.Add(recipe);

            var valid = ValidateOutput(recipe.Output);
            valid &= ValidateStack(recipe.RootStack, recipes, recipeStack, stacks, stackStack);

            recipeStack.Remove(recipe);
            recipes.Add(recipe);
            return valid;
        }

        static bool ValidateOutput(OutputProfile output)
        {
            if (output == null)
                return Fail("TextureRecipe.Output is missing.");

            var valid = true;

            if (output.Resolution.x <= 0 || output.Resolution.y <= 0)
                valid &= Fail("TextureRecipe.Output.Resolution must be positive.");

            if (output.WorkingFormat == GraphicsFormat.None)
                valid &= Fail("TextureRecipe.Output.WorkingFormat is invalid.");
            else if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.Render))
                valid &= Fail($"TextureRecipe.Output.WorkingFormat is not renderable: {output.WorkingFormat}.");

            return valid;
        }

        static bool ValidateStack(
            LayerStack stack,
            HashSet<TextureRecipe> recipes,
            HashSet<TextureRecipe> recipeStack,
            HashSet<LayerStack> stacks,
            HashSet<LayerStack> stackStack)
        {
            if (stack == null)
                return Fail("TextureRecipe.RootStack is missing.");

            if (stackStack.Contains(stack))
                return Fail("Recursive inline stack detected.");

            if (stacks.Contains(stack))
                return true;

            stackStack.Add(stack);
            var valid = true;

            if (stack.Layers == null)
            {
                stackStack.Remove(stack);
                return Fail("LayerStack.Layers is missing.");
            }

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

                valid &= ValidateMask(layer.Mask, recipes, recipeStack, stacks, stackStack);
                valid &= ValidateLayer(layer, recipes, recipeStack, stacks, stackStack);
            }

            stackStack.Remove(stack);
            stacks.Add(stack);
            return valid;
        }

        static bool ValidateLayer(
            TextureLayerBase layer,
            HashSet<TextureRecipe> recipes,
            HashSet<TextureRecipe> recipeStack,
            HashSet<LayerStack> stacks,
            HashSet<LayerStack> stackStack)
        {
            switch (layer)
            {
                case TextureFileLayer textureFile:
                    return ValidateSource(textureFile.Source);
                case ChannelPackLayer channelPack:
                    var valid = ValidatePackSource(channelPack.R);
                    valid &= ValidatePackSource(channelPack.G);
                    valid &= ValidatePackSource(channelPack.B);
                    valid &= ValidatePackSource(channelPack.A);
                    return valid;
                case RecipeReferenceLayer recipeReference:
                    return recipeReference.Recipe == null
                        ? Fail("Recipe reference is missing.")
                        : ValidateRecipe(recipeReference.Recipe, recipes, recipeStack, stacks, stackStack);
                default:
                    return true;
            }
        }

        static bool ValidateMask(
            StackMask mask,
            HashSet<TextureRecipe> recipes,
            HashSet<TextureRecipe> recipeStack,
            HashSet<LayerStack> stacks,
            HashSet<LayerStack> stackStack)
        {
            if (mask == null)
                return true;

            switch (mask.Source)
            {
                case StackSource.None:
                    return true;
                case StackSource.InlineStack:
                    return mask.InlineStack == null
                        ? Fail("Inline mask stack is missing.")
                        : ValidateStack(mask.InlineStack, recipes, recipeStack, stacks, stackStack);
                case StackSource.RecipeReference:
                    return mask.RecipeReference == null
                        ? Fail("Recipe mask reference is missing.")
                        : ValidateRecipe(mask.RecipeReference, recipes, recipeStack, stacks, stackStack);
                default:
                    return Fail("Mask source is invalid.");
            }
        }

        static bool ValidatePackSource(ChannelPackSource source)
        {
            if (source == null)
                return Fail("Channel pack source is missing.");

            return ValidateSource(source.Texture);
        }

        static bool ValidateSource(TextureSource source)
        {
            if (source == null)
                return Fail("Texture source is missing.");

            if (source.Kind != TextureSourceKind.RuntimeTextureReference)
                return Fail("Runtime validation requires RuntimeTextureReference sources.");

            return source.RuntimeTexture == null
                ? Fail("RuntimeTextureReference source is missing a texture.")
                : true;
        }

        static bool Fail(string message)
        {
            Debug.LogError(message);
            return false;
        }
    }
}
