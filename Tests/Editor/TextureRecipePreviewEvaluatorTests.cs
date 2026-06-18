using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEngine;
using UnityEngine.TestTools;
using static LayeredTextureTestUtility;

public sealed class TextureRecipePreviewEvaluatorTests
{
    [Test]
    public void Evaluate_RecipeWithMipOutput_DisablesPreviewMips()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.Output.GenerateMips = true;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.5f, 0.25f, 0.75f, 1f)
        });

        var result = TextureRecipePreviewEvaluator.Evaluate(recipe);

        try
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.useMipMap, Is.False);
            Assert.That(recipe.Output.GenerateMips, Is.True);
            AssertScaledBlitHasColor(result);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (result != null)
                Release(result);

            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Evaluate_StackWithMipOutput_DisablesPreviewMips()
    {
        IgnoreUnsupportedCompute();

        var output = OutputProfile.Default;
        output.Resolution = new Vector2Int(64, 16);
        output.GenerateMips = true;
        var stack = new LayerStack();
        stack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 1f)
        });

        var result = TextureRecipePreviewEvaluator.Evaluate(stack, output);

        try
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.useMipMap, Is.False);
            Assert.That(output.GenerateMips, Is.True);
            AssertScaledBlitHasColor(result);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (result != null)
                Release(result);
        }
    }

    static TextureRecipe CreateRecipe()
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(64, 16);
        return recipe;
    }

    static void AssertScaledBlitHasColor(RenderTexture source)
    {
        var target = RenderTexture.GetTemporary(8, 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var active = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);

        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();

            foreach (var pixel in texture.GetPixels())
                Assert.That(pixel.maxColorComponent, Is.GreaterThan(0.1f));
        }
        finally
        {
            Object.DestroyImmediate(texture);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
