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
    public void Evaluate_NoiseLayer_ReturnsGrayscaleRgba()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(8, 8);
        recipe.RootStack.Layers.Add(new NoiseLayer());

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertGrayscaleRgba(result);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_NoiseLayer_SameSeedIsDeterministic()
    {
        IgnoreUnsupportedCompute();

        var firstRecipe = CreateRecipe(8, 8);
        var secondRecipe = CreateRecipe(8, 8);
        firstRecipe.RootStack.Layers.Add(new NoiseLayer
        {
            Seed = 42,
            NoiseType = NoiseType.WorleyF1,
            Fractal = NoiseFractal.Ridged,
            WarpStrength = 0.25f
        });
        secondRecipe.RootStack.Layers.Add(new NoiseLayer
        {
            Seed = 42,
            NoiseType = NoiseType.WorleyF1,
            Fractal = NoiseFractal.Ridged,
            WarpStrength = 0.25f
        });

        var first = TextureRecipeEvaluator.Evaluate(firstRecipe);
        var second = TextureRecipeEvaluator.Evaluate(secondRecipe);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        AssertTexturesEqual(first, second);
        LogAssert.NoUnexpectedReceived();

        Release(first);
        Release(second);
        Object.DestroyImmediate(firstRecipe);
        Object.DestroyImmediate(secondRecipe);
    }

    [Test]
    public void Evaluate_NoiseLayer_DifferentSeedsProduceDifferentOutput()
    {
        IgnoreUnsupportedCompute();

        var firstRecipe = CreateRecipe(8, 8);
        var secondRecipe = CreateRecipe(8, 8);
        firstRecipe.RootStack.Layers.Add(new NoiseLayer { Seed = 7 });
        secondRecipe.RootStack.Layers.Add(new NoiseLayer { Seed = 11 });

        var first = TextureRecipeEvaluator.Evaluate(firstRecipe);
        var second = TextureRecipeEvaluator.Evaluate(secondRecipe);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(TexturesDiffer(first, second), Is.True);
        LogAssert.NoUnexpectedReceived();

        Release(first);
        Release(second);
        Object.DestroyImmediate(firstRecipe);
        Object.DestroyImmediate(secondRecipe);
    }

    [TestCase(NoiseType.Value, NoiseFractal.FBM)]
    [TestCase(NoiseType.Simplex, NoiseFractal.Turbulence)]
    [TestCase(NoiseType.WorleyF1, NoiseFractal.Ridged)]
    [TestCase(NoiseType.WorleyEdge, NoiseFractal.Billow)]
    public void Evaluate_NoiseLayer_AdvancedModesProduceNonFlatOutput(NoiseType noiseType, NoiseFractal fractal)
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(8, 8);
        recipe.RootStack.Layers.Add(new NoiseLayer
        {
            NoiseType = noiseType,
            Fractal = fractal,
            WarpStrength = 0.2f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [TestCase(NoiseType.Value, NoiseFractal.None, 0f)]
    [TestCase(NoiseType.Gradient, NoiseFractal.FBM, 0f)]
    [TestCase(NoiseType.Gradient, NoiseFractal.FBM, 0.25f)]
    [TestCase(NoiseType.WorleyF1, NoiseFractal.Ridged, 0f)]
    public void Evaluate_NoiseLayer_IsContinuousAcrossTileSeams(
        NoiseType noiseType,
        NoiseFractal fractal,
        float warpStrength)
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(64, 64);
        recipe.RootStack.Layers.Add(new NoiseLayer
        {
            NoiseType = noiseType,
            Fractal = fractal,
            Seed = 17,
            Scale = 6f,
            Octaves = 3,
            Lacunarity = 2f,
            Gain = 0.45f,
            WarpStrength = warpStrength,
            WarpScale = 3f,
            WarpOctaves = 2
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertContinuousTileSeams(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_NoiseLayer_WriteMaskPreservesUntouchedChannels()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(8, 8);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.1f, 0.2f, 0.3f, 0.4f)
        });
        recipe.RootStack.Layers.Add(new NoiseLayer
        {
            WriteMask = ChannelWriteMask.R
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        foreach (var pixel in ReadPixels(result))
        {
            Assert.That(pixel.g, Is.EqualTo(0.2f).Within(0.02f));
            Assert.That(pixel.b, Is.EqualTo(0.3f).Within(0.02f));
            Assert.That(pixel.a, Is.EqualTo(0.4f).Within(0.02f));
        }

        LogAssert.NoUnexpectedReceived();

        Release(result);
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

    static TextureRecipe CreateRecipe(int width, int height)
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(width, height);
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

    static void AssertGrayscaleRgba(RenderTexture renderTexture)
    {
        foreach (var pixel in ReadPixels(renderTexture))
        {
            Assert.That(pixel.g, Is.EqualTo(pixel.r).Within(0.02f));
            Assert.That(pixel.b, Is.EqualTo(pixel.r).Within(0.02f));
            Assert.That(pixel.a, Is.EqualTo(pixel.r).Within(0.02f));
        }
    }

    static void AssertNonFlat(RenderTexture renderTexture)
    {
        var pixels = ReadPixels(renderTexture);
        var first = pixels[0].r;

        for (var i = 1; i < pixels.Length; i++)
        {
            if (Mathf.Abs(pixels[i].r - first) > 0.02f)
                return;
        }

        Assert.Fail("Expected non-flat noise output.");
    }

    static void AssertContinuousTileSeams(RenderTexture renderTexture)
    {
        var pixels = ReadPixels(renderTexture);
        var width = renderTexture.width;
        var height = renderTexture.height;
        var seamX = 0f;
        var seamY = 0f;
        var adjacentX = 0f;
        var adjacentY = 0f;

        for (var y = 0; y < height; y++)
        {
            seamX += Mathf.Abs(PixelRed(pixels, width, width - 1, y) - PixelRed(pixels, width, 0, y));

            for (var x = 1; x < width; x++)
                adjacentX += Mathf.Abs(PixelRed(pixels, width, x, y) - PixelRed(pixels, width, x - 1, y));
        }

        for (var x = 0; x < width; x++)
        {
            seamY += Mathf.Abs(PixelRed(pixels, width, x, height - 1) - PixelRed(pixels, width, x, 0));

            for (var y = 1; y < height; y++)
                adjacentY += Mathf.Abs(PixelRed(pixels, width, x, y) - PixelRed(pixels, width, x, y - 1));
        }

        var meanSeamX = seamX / height;
        var meanSeamY = seamY / width;
        var meanAdjacentX = adjacentX / (height * (width - 1));
        var meanAdjacentY = adjacentY / (width * (height - 1));

        Assert.That(meanSeamX, Is.LessThanOrEqualTo(meanAdjacentX * 3f + 0.02f), "Horizontal tile seam");
        Assert.That(meanSeamY, Is.LessThanOrEqualTo(meanAdjacentY * 3f + 0.02f), "Vertical tile seam");
    }

    static float PixelRed(Color[] pixels, int width, int x, int y) => pixels[y * width + x].r;

    static void AssertTexturesEqual(RenderTexture first, RenderTexture second)
    {
        var firstPixels = ReadPixels(first);
        var secondPixels = ReadPixels(second);

        Assert.That(firstPixels.Length, Is.EqualTo(secondPixels.Length));

        for (var i = 0; i < firstPixels.Length; i++)
            AssertColor(firstPixels[i], secondPixels[i], $"Pixel {i}");
    }

    static bool TexturesDiffer(RenderTexture first, RenderTexture second)
    {
        var firstPixels = ReadPixels(first);
        var secondPixels = ReadPixels(second);

        for (var i = 0; i < firstPixels.Length; i++)
        {
            if (Mathf.Abs(firstPixels[i].r - secondPixels[i].r) > 0.02f)
                return true;
        }

        return false;
    }

    static Color[] ReadPixels(RenderTexture renderTexture)
    {
        var active = RenderTexture.active;
        var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false, true);

        try
        {
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            return texture.GetPixels();
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
