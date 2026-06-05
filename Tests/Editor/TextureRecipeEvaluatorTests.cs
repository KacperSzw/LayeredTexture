using NUnit.Framework;
using Unmanaged.LayeredTexture;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

public sealed class TextureRecipeEvaluatorTests
{
    [Test]
    public void Evaluate_EmptyRecipe_ReturnsClearTexture()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, Color.clear);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SolidColorLayer_ReturnsColor()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 1f)
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.25f, 0.5f, 0.75f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [TestCase(BlendMode.Normal, 0.6f, 0.25f, 0.9f, 0.2f)]
    [TestCase(BlendMode.Replace, 0.6f, 0.25f, 0.9f, 0.2f)]
    [TestCase(BlendMode.Add, 0.85f, 0.75f, 1f, 0.6f)]
    [TestCase(BlendMode.Multiply, 0.15f, 0.125f, 0.675f, 0.08f)]
    [TestCase(BlendMode.Min, 0.25f, 0.25f, 0.75f, 0.2f)]
    [TestCase(BlendMode.Max, 0.6f, 0.5f, 0.9f, 0.4f)]
    public void Evaluate_SolidColorBlendMode_ReturnsExpectedColor(
        BlendMode blendMode,
        float expectedR,
        float expectedG,
        float expectedB,
        float expectedA)
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 0.4f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.6f, 0.25f, 0.9f, 0.2f),
            BlendMode = blendMode
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(expectedR, expectedG, expectedB, expectedA));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_TwoSolidColorLayers_BlendsOpacity()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.blue,
            Opacity = 0.5f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.5f, 0f, 0.5f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SolidColorLayerOutOfRange_ClampsValuesToZeroOne()
    {
        IgnoreUnsupportedCompute();
        IgnoreUnsupportedWorkingFormat(GraphicsFormat.R32G32B32A32_SFloat, "Float LayeredTexture working format");

        var recipe = CreateRecipe();
        recipe.Output.WorkingFormat = GraphicsFormat.R32G32B32A32_SFloat;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(1.5f, -0.25f, 0.5f, 2f)
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(1f, 0f, 0.5f, 1f), TextureFormat.RGBAFloat);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_WriteMask_PreservesUntouchedChannels()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.1f, 0.2f, 0.3f, 0.4f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.white,
            WriteMask = ChannelWriteMask.R
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(1f, 0.2f, 0.3f, 0.4f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_LayerMaskRecipe_UsesGrayscaleMask()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var mask = CreateRecipe();
        mask.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.25f, 0.25f, 1f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.blue,
            Mask = new StackMask
            {
                RecipeReference = mask
            }
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.75f, 0f, 0.25f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_LayerMaskRecipe_UsesSelectedChannel()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var mask = CreateRecipe();
        mask.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.6f, 0.9f, 0.4f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.blue,
            Mask = new StackMask
            {
                RecipeReference = mask,
                Usage = MaskUsage.G
            }
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.4f, 0f, 0.6f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_LayerMaskRecipe_AppliesInvertAndOpacity()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var mask = CreateRecipe();
        mask.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.25f, 0.25f, 1f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.blue,
            Mask = new StackMask
            {
                RecipeReference = mask,
                Invert = true,
                Opacity = 0.5f
            }
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.625f, 0f, 0.375f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_LayerMaskRecipe_ResamplesDifferentResolution()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var mask = CreateRecipe();
        mask.Output.Resolution = new Vector2Int(4, 4);
        mask.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.5f, 0.5f, 0.5f, 1f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.blue,
            Mask = new StackMask
            {
                RecipeReference = mask
            }
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.5f, 0f, 0.5f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_RecursiveMask_ReturnsNull()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red,
            Mask = new StackMask
            {
                RecipeReference = recipe
            }
        });

        LogAssert.Expect(LogType.Error, "TextureRecipe mask reference cycle detected.");

        Assert.That(TextureRecipeEvaluator.Evaluate(recipe), Is.Null);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_TextureFileLayer_AppliesSourceSwizzleAndWriteMask()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var source = CreateTexture(new Color(0.7f, 0.6f, 0.5f, 0.9f));
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.1f, 0.2f, 0.3f, 0.4f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source),
            InputSwizzle = new ChannelSwizzle
            {
                R = TextureChannel.B,
                G = TextureChannel.G,
                B = TextureChannel.R,
                A = TextureChannel.A
            },
            WriteMask = ChannelWriteMask.R | ChannelWriteMask.G
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.5f, 0.6f, 0.3f, 0.4f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_TextureFileLayer_ResamplesDifferentSourceResolution()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var source = CreateTexture(4, 4, new Color(0.6f, 0.3f, 0.8f, 1f));
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.6f, 0.3f, 0.8f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_InvalidRecipe_ReturnsNull()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.Output.Resolution = Vector2Int.zero;

        LogAssert.Expect(LogType.Error, "TextureRecipe.Output.Resolution must be positive.");

        Assert.That(TextureRecipeEvaluator.Evaluate(recipe), Is.Null);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_TextureFileLayerWithoutRuntimeTexture_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.6f, 0.8f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer());

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(TextureRecipeEvaluator.ValidateRuntime(recipe), Is.True);
        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.2f, 0.4f, 0.6f, 0.8f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    static TextureRecipe CreateRecipe()
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(2, 2);
        return recipe;
    }

    static Texture2D CreateTexture(Color color) => CreateTexture(2, 2, color);

    static Texture2D CreateTexture(int width, int height, Color color)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, color);

        texture.Apply();
        return texture;
    }

    static TextureSource RuntimeSource(Texture texture) => new()
    {
        Kind = TextureSourceKind.RuntimeTextureReference,
        RuntimeTexture = texture
    };

    static void AssertTexturePixels(RenderTexture renderTexture, Color expected, TextureFormat textureFormat = TextureFormat.RGBA32)
    {
        Assert.That(renderTexture.width, Is.EqualTo(2));
        Assert.That(renderTexture.height, Is.EqualTo(2));

        var active = RenderTexture.active;
        var texture = new Texture2D(renderTexture.width, renderTexture.height, textureFormat, false, true);

        try
        {
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
                AssertColor(texture.GetPixel(x, y), expected, $"Pixel ({x}, {y})");
        }
        finally
        {
            Object.DestroyImmediate(texture);
            RenderTexture.active = active;
        }
    }

    static void AssertColor(Color actual, Color expected, string message)
    {
        const float Tolerance = 0.02f;

        Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), $"{message} red");
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), $"{message} green");
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), $"{message} blue");
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance), $"{message} alpha");
    }

    static void Release(RenderTexture texture)
    {
        texture.Release();
        Object.DestroyImmediate(texture);
    }

    static void IgnoreUnsupportedCompute()
    {
        if (!SystemInfo.supportsComputeShaders)
            Assert.Ignore("Compute shaders are not supported in this editor environment.");

        IgnoreUnsupportedWorkingFormat(GraphicsFormat.R16G16B16A16_UNorm, "Default LayeredTexture working format");
    }

    static void IgnoreUnsupportedWorkingFormat(GraphicsFormat format, string label)
    {
        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
            Assert.Ignore($"{label} is not renderable in this editor environment.");

        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore))
            Assert.Ignore($"{label} does not support compute writes in this editor environment.");
    }
}
