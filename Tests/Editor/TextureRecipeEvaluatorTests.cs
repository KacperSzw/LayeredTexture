using NUnit.Framework;
using Unmanaged.LayeredTexture;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using static LayeredTextureTestUtility;

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
    [TestCase(BlendMode.Subtract, 0f, 0.25f, 0f, 0.2f)]
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
    public void Evaluate_SubtractBlendMode_RespectsOpacityAndWriteMask()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.8f, 0.7f, 0.6f, 0.5f)
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.3f, 0.9f, 0.2f, 0.4f),
            BlendMode = BlendMode.Subtract,
            Opacity = 0.5f,
            WriteMask = ChannelWriteMask.R | ChannelWriteMask.B
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.65f, 0.7f, 0.5f, 0.5f));
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

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(1.5f, -0.25f, 0.5f, 2f)
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(1f, 0f, 0.5f, 1f));
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

        Assert.That(TextureRecipeEvaluator.Evaluate(recipe), Is.Null);
        LogAssert.NoUnexpectedReceived();

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
                R = SwizzleChannelSource.B,
                G = SwizzleChannelSource.G,
                B = SwizzleChannelSource.R,
                A = SwizzleChannelSource.A
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
    public void Evaluate_TextureFileLayer_AppliesFullInputSwizzle()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var source = CreateTexture(new Color(0.2f, 0.4f, 0.75f, 0.9f));
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source),
            InputSwizzle = new ChannelSwizzle
            {
                R = SwizzleChannelSource.Zero,
                G = SwizzleChannelSource.R,
                B = SwizzleChannelSource.OneMinusB,
                A = SwizzleChannelSource.One
            }
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0f, 0.2f, 0.25f, 1f));
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
    public void Evaluate_RecipeReferenceLayer_CompositesReferencedRecipe()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var reference = CreateRecipe();
        reference.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.blue
        });
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer
        {
            Recipe = reference,
            Opacity = 0.5f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.5f, 0f, 0.5f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(reference);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_RecipeReferenceLayerWithoutRecipe_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.6f, 0.8f)
        });
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer());

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(TextureRecipeEvaluator.ValidateRuntime(recipe), Is.True);
        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.2f, 0.4f, 0.6f, 0.8f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
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
            Fractal = NoiseFractal.Ridged
        });
        secondRecipe.RootStack.Layers.Add(new NoiseLayer
        {
            Seed = 42,
            NoiseType = NoiseType.WorleyF1,
            Fractal = NoiseFractal.Ridged
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
            Fractal = fractal
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_NoiseLayer_VectorScaleProducesNonFlatOutput()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(16, 16);
        recipe.RootStack.Layers.Add(new NoiseLayer
        {
            Scale = new Vector2(3f, 9f),
            Seed = 23
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [TestCase(NoiseType.Value, NoiseFractal.None)]
    [TestCase(NoiseType.Gradient, NoiseFractal.FBM)]
    [TestCase(NoiseType.WorleyF1, NoiseFractal.Ridged)]
    public void Evaluate_NoiseLayer_IsContinuousAcrossTileSeams(
        NoiseType noiseType,
        NoiseFractal fractal)
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(64, 64);
        recipe.RootStack.Layers.Add(new NoiseLayer
        {
            NoiseType = noiseType,
            Fractal = fractal,
            Seed = 17,
            Scale = new Vector2(6f, 6f),
            Octaves = 3,
            Lacunarity = 2f,
            Gain = 0.45f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertContinuousTileSeams(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_WarpLayer_DistortsPreviousStack()
    {
        IgnoreUnsupportedCompute();

        var baseRecipe = CreateRecipe(32, 32);
        var warpedRecipe = CreateRecipe(32, 32);
        var noise = new NoiseLayer
        {
            Seed = 17,
            Scale = new Vector2(8f, 8f),
            NoiseType = NoiseType.Gradient
        };
        baseRecipe.RootStack.Layers.Add(noise);
        warpedRecipe.RootStack.Layers.Add(new NoiseLayer
        {
            Seed = 17,
            Scale = new Vector2(8f, 8f),
            NoiseType = NoiseType.Gradient
        });
        warpedRecipe.RootStack.Layers.Add(new WarpLayer
        {
            Seed = 3,
            Strength = 0.08f,
            Scale = 3f,
            Octaves = 2
        });

        var baseline = TextureRecipeEvaluator.Evaluate(baseRecipe);
        var warped = TextureRecipeEvaluator.Evaluate(warpedRecipe);

        Assert.That(baseline, Is.Not.Null);
        Assert.That(warped, Is.Not.Null);
        Assert.That(TexturesDiffer(baseline, warped), Is.True);
        LogAssert.NoUnexpectedReceived();

        Release(baseline);
        Release(warped);
        Object.DestroyImmediate(baseRecipe);
        Object.DestroyImmediate(warpedRecipe);
    }

    [Test]
    public void Evaluate_WarpLayer_IsContinuousAcrossTileSeams()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(64, 64);
        recipe.RootStack.Layers.Add(new NoiseLayer
        {
            NoiseType = NoiseType.Gradient,
            Fractal = NoiseFractal.FBM,
            Seed = 17,
            Scale = new Vector2(6f, 6f),
            Octaves = 3
        });
        recipe.RootStack.Layers.Add(new WarpLayer
        {
            Seed = 5,
            Strength = 0.08f,
            Scale = 3f,
            Octaves = 2
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertContinuousTileSeams(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_BlurLayer_RadiusZeroKeepsPreviousStack()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(4, 4);
        var texture = CreateRedImpulseTexture(4, 4, 1, 1);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        recipe.RootStack.Layers.Add(new BlurLayer
        {
            RadiusMode = BlurRadiusMode.PerPixel,
            Radius = 0f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        var pixels = ReadPixels(result);
        Assert.That(PixelRed(pixels, 4, 1, 1), Is.GreaterThan(0.95f));
        Assert.That(PixelRed(pixels, 4, 0, 0), Is.LessThan(0.02f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_BlurLayer_PreservesConstantColor()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(8, 8);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 0.6f)
        });
        recipe.RootStack.Layers.Add(new BlurLayer
        {
            RadiusMode = BlurRadiusMode.PerPixel,
            Radius = 16f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        foreach (var pixel in ReadPixels(result))
            AssertColor(pixel, new Color(0.25f, 0.5f, 0.75f, 0.6f), "Constant blur");

        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_BlurLayer_SoftensImpulseAndWrapsAcrossSeams()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(8, 8);
        var texture = CreateRedImpulseTexture(8, 8, 0, 0);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        recipe.RootStack.Layers.Add(new BlurLayer
        {
            RadiusMode = BlurRadiusMode.PerPixel,
            Radius = 3f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        var pixels = ReadPixels(result);
        Assert.That(PixelRed(pixels, 8, 0, 0), Is.GreaterThan(0.02f).And.LessThan(0.95f));
        Assert.That(PixelRed(pixels, 8, 7, 0), Is.GreaterThan(0.02f));
        Assert.That(PixelRed(pixels, 8, 0, 7), Is.GreaterThan(0.02f));
        Assert.That(PixelRed(pixels, 8, 4, 4), Is.LessThan(0.02f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_BlurLayer_WriteMaskPreservesUntouchedChannels()
    {
        IgnoreUnsupportedCompute();

        var baselineRecipe = CreateRecipe(8, 8);
        var blurredRecipe = CreateRecipe(8, 8);
        var texture = CreatePatternTexture(8, 8);
        baselineRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        blurredRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        blurredRecipe.RootStack.Layers.Add(new BlurLayer
        {
            RadiusMode = BlurRadiusMode.PerPixel,
            Radius = 2f,
            WriteMask = ChannelWriteMask.R
        });

        var baseline = TextureRecipeEvaluator.Evaluate(baselineRecipe);
        var blurred = TextureRecipeEvaluator.Evaluate(blurredRecipe);

        Assert.That(baseline, Is.Not.Null);
        Assert.That(blurred, Is.Not.Null);

        var baselinePixels = ReadPixels(baseline);
        var blurredPixels = ReadPixels(blurred);

        for (var i = 0; i < baselinePixels.Length; i++)
        {
            Assert.That(blurredPixels[i].g, Is.EqualTo(baselinePixels[i].g).Within(0.02f), $"Pixel {i} green");
            Assert.That(blurredPixels[i].b, Is.EqualTo(baselinePixels[i].b).Within(0.02f), $"Pixel {i} blue");
            Assert.That(blurredPixels[i].a, Is.EqualTo(baselinePixels[i].a).Within(0.02f), $"Pixel {i} alpha");
        }

        Assert.That(TexturesDiffer(baseline, blurred), Is.True);
        LogAssert.NoUnexpectedReceived();

        Release(baseline);
        Release(blurred);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(baselineRecipe);
        Object.DestroyImmediate(blurredRecipe);
    }

    [Test]
    public void Evaluate_TransformLayer_OffsetWrapsAcrossSeams()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(4, 1);
        var texture = CreateRedImpulseTexture(4, 1, 0, 0);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        recipe.RootStack.Layers.Add(new TransformLayer
        {
            Offset = new Vector2(0.25f, 0f)
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        var pixels = ReadPixels(result);
        Assert.That(PixelRed(pixels, 4, 1, 0), Is.GreaterThan(0.95f));
        Assert.That(PixelRed(pixels, 4, 0, 0), Is.LessThan(0.02f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_TransformLayer_ScaleZoomsAroundPivot()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(8, 1);
        var texture = CreateGreenGradientTexture(8, 1);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        recipe.RootStack.Layers.Add(new TransformLayer
        {
            Scale = new Vector2(2f, 1f)
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        var pixels = ReadPixels(result);
        Assert.That(pixels[0].g, Is.GreaterThan(0.1f));
        Assert.That(pixels[7].g, Is.LessThan(0.9f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_TransformLayer_RotatesAroundPivot()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(3, 3);
        var texture = CreateRedImpulseTexture(3, 3, 0, 0);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });
        recipe.RootStack.Layers.Add(new TransformLayer
        {
            Rotation = 180f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);

        var pixels = ReadPixels(result);
        Assert.That(PixelRed(pixels, 3, 2, 2), Is.GreaterThan(0.95f));
        Assert.That(PixelRed(pixels, 3, 0, 0), Is.LessThan(0.02f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(texture);
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
    public void Evaluate_WaterWavesLayer_ReturnsNonFlatGrayscaleRgba()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(32, 32);
        recipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            Seed = 12,
            WaveCount = 8
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertGrayscaleRgba(result);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_WaterWavesLayer_SameSeedIsDeterministic()
    {
        IgnoreUnsupportedCompute();

        var firstRecipe = CreateRecipe(32, 32);
        var secondRecipe = CreateRecipe(32, 32);
        firstRecipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            Seed = 42,
            WaveCount = 12
        });
        secondRecipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            Seed = 42,
            WaveCount = 12
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
    public void Evaluate_WaterWavesLayer_DifferentSeedsProduceDifferentOutput()
    {
        IgnoreUnsupportedCompute();

        var firstRecipe = CreateRecipe(32, 32);
        var secondRecipe = CreateRecipe(32, 32);
        firstRecipe.RootStack.Layers.Add(new WaterWavesLayer { Seed = 7 });
        secondRecipe.RootStack.Layers.Add(new WaterWavesLayer { Seed = 11 });

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

    [Test]
    public void Evaluate_WaterWavesLayer_IsContinuousAcrossTileSeams()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(64, 64);
        recipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            Seed = 17,
            WaveCount = 12,
            CycleRange = new Vector2(2f, 10f),
            DirectionSpread = 70f,
            Choppiness = 0.35f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertContinuousTileSeams(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_WaterWavesLayer_WriteMaskPreservesUntouchedChannels()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(16, 16);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.1f, 0.2f, 0.3f, 0.4f)
        });
        recipe.RootStack.Layers.Add(new WaterWavesLayer
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
    public void Evaluate_WaterWavesLayer_FoamModeReturnsGrayscaleRgba()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(32, 32);
        recipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            OutputMode = WaterWavesOutputMode.Foam,
            FoamThreshold = 0.55f,
            FoamSoftness = 0.2f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertGrayscaleRgba(result);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_WaterWavesLayer_FoamThresholdChangesCoverage()
    {
        IgnoreUnsupportedCompute();

        var lowThresholdRecipe = CreateRecipe(32, 32);
        var highThresholdRecipe = CreateRecipe(32, 32);
        lowThresholdRecipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            OutputMode = WaterWavesOutputMode.Foam,
            Seed = 12,
            FoamThreshold = 0.45f,
            FoamSoftness = 0.08f
        });
        highThresholdRecipe.RootStack.Layers.Add(new WaterWavesLayer
        {
            OutputMode = WaterWavesOutputMode.Foam,
            Seed = 12,
            FoamThreshold = 0.85f,
            FoamSoftness = 0.08f
        });

        var lowThreshold = TextureRecipeEvaluator.Evaluate(lowThresholdRecipe);
        var highThreshold = TextureRecipeEvaluator.Evaluate(highThresholdRecipe);

        Assert.That(lowThreshold, Is.Not.Null);
        Assert.That(highThreshold, Is.Not.Null);
        Assert.That(AverageRed(lowThreshold), Is.GreaterThan(AverageRed(highThreshold) + 0.1f));
        LogAssert.NoUnexpectedReceived();

        Release(lowThreshold);
        Release(highThreshold);
        Object.DestroyImmediate(lowThresholdRecipe);
        Object.DestroyImmediate(highThresholdRecipe);
    }

    [Test]
    public void Evaluate_HistogramSelectLayer_SelectsCenteredLuminanceBand()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(3, 1);
        var source = CreateRowTexture(new[]
        {
            new Color(0.25f, 0.25f, 0.25f, 1f),
            new Color(0.5f, 0.5f, 0.5f, 1f),
            new Color(0.9f, 0.9f, 0.9f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new HistogramSelectLayer
        {
            Position = 0.5f,
            Range = 0.2f,
            Gradient = 0f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        var pixels = ReadPixels(result);
        AssertColor(pixels[0], Color.clear, "Pixel 0");
        AssertColor(pixels[1], Color.white, "Pixel 1");
        AssertColor(pixels[2], Color.clear, "Pixel 2");
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_HistogramSelectLayer_ValueModeUsesMaxChannel()
    {
        IgnoreUnsupportedCompute();

        var luminanceRecipe = CreateRecipe();
        var valueRecipe = CreateRecipe();
        var source = CreateTexture(Color.red);
        luminanceRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        luminanceRecipe.RootStack.Layers.Add(new HistogramSelectLayer
        {
            Mode = HistogramSelectionMode.Luminance,
            Position = 1f,
            Range = 0.1f,
            Gradient = 0f
        });
        valueRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        valueRecipe.RootStack.Layers.Add(new HistogramSelectLayer
        {
            Mode = HistogramSelectionMode.Value,
            Position = 1f,
            Range = 0.1f,
            Gradient = 0f
        });

        var luminance = TextureRecipeEvaluator.Evaluate(luminanceRecipe);
        var value = TextureRecipeEvaluator.Evaluate(valueRecipe);

        Assert.That(luminance, Is.Not.Null);
        Assert.That(value, Is.Not.Null);
        AssertTexturePixels(luminance, Color.clear);
        AssertTexturePixels(value, Color.white);
        LogAssert.NoUnexpectedReceived();

        Release(luminance);
        Release(value);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(luminanceRecipe);
        Object.DestroyImmediate(valueRecipe);
    }

    [Test]
    public void Evaluate_HistogramSelectLayer_GradientFeathersSelection()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(3, 1);
        var source = CreateRowTexture(new[]
        {
            new Color(0.5f, 0.5f, 0.5f, 1f),
            new Color(0.65f, 0.65f, 0.65f, 1f),
            new Color(0.8f, 0.8f, 0.8f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new HistogramSelectLayer
        {
            Position = 0.5f,
            Range = 0.2f,
            Gradient = 0.2f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        var pixels = ReadPixels(result);
        Assert.That(pixels[0].r, Is.EqualTo(1f).Within(0.02f));
        Assert.That(pixels[1].r, Is.EqualTo(0.84f).Within(0.04f));
        Assert.That(pixels[2].r, Is.EqualTo(0f).Within(0.02f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SwizzleLayer_DefaultsPreservePrevious()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.6f, 0.8f)
        });
        recipe.RootStack.Layers.Add(new SwizzleLayer());

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.2f, 0.4f, 0.6f, 0.8f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SwizzleLayer_MovesInvertsAndWritesConstants()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.75f, 0.9f)
        });
        recipe.RootStack.Layers.Add(new SwizzleLayer
        {
            R = SwizzleChannelSource.Zero,
            G = SwizzleChannelSource.R,
            B = SwizzleChannelSource.OneMinusB,
            A = SwizzleChannelSource.One
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0f, 0.2f, 0.25f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SwizzleLayer_RespectsOpacityAndWriteMask()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.6f, 0.8f)
        });
        recipe.RootStack.Layers.Add(new SwizzleLayer
        {
            R = SwizzleChannelSource.One,
            G = SwizzleChannelSource.One,
            B = SwizzleChannelSource.One,
            A = SwizzleChannelSource.One,
            Opacity = 0.5f,
            WriteMask = ChannelWriteMask.R | ChannelWriteMask.B
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.6f, 0.4f, 0.8f, 0.8f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_ColorSelectLayer_RgbSelectsNearTarget()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(3, 1);
        var source = CreateRowTexture(new[]
        {
            Color.red,
            new Color(0.95f, 0.05f, 0.02f, 1f),
            Color.blue
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new ColorSelectLayer
        {
            TargetColor = Color.red,
            Mode = ColorSelectionMode.RGB,
            Tolerance = 0.1f,
            Softness = 0f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        var pixels = ReadPixels(result);
        AssertColor(pixels[0], Color.white, "Pixel 0");
        AssertColor(pixels[1], Color.white, "Pixel 1");
        AssertColor(pixels[2], Color.clear, "Pixel 2");
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_ColorSelectLayer_HueSelectsHueAndIgnoresGray()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(4, 1);
        var source = CreateRowTexture(new[]
        {
            Color.red,
            new Color(0.5f, 0f, 0f, 1f),
            new Color(0.5f, 0.5f, 0.5f, 1f),
            Color.green
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new ColorSelectLayer
        {
            TargetColor = Color.red,
            Mode = ColorSelectionMode.Hue,
            Tolerance = 0.05f,
            Softness = 0f,
            MinimumSaturation = 0.1f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        var pixels = ReadPixels(result);
        AssertColor(pixels[0], Color.white, "Pixel 0");
        AssertColor(pixels[1], Color.white, "Pixel 1");
        AssertColor(pixels[2], Color.clear, "Pixel 2");
        AssertColor(pixels[3], Color.clear, "Pixel 3");
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_ColorSelectLayer_SoftnessFeathersSelection()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(3, 1);
        var source = CreateRowTexture(new[]
        {
            Color.black,
            new Color(0.15f, 0f, 0f, 1f),
            new Color(0.4f, 0f, 0f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new ColorSelectLayer
        {
            TargetColor = Color.black,
            Mode = ColorSelectionMode.RGB,
            Tolerance = 0.05f,
            Softness = 0.1f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        var pixels = ReadPixels(result);
        Assert.That(pixels[0].r, Is.EqualTo(1f).Within(0.02f));
        Assert.That(pixels[1].r, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(pixels[2].r, Is.EqualTo(0f).Within(0.02f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SaturationLayer_HueOffsetRotatesColor()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(1f, 0f, 0f, 0.7f)
        });
        recipe.RootStack.Layers.Add(new SaturationLayer
        {
            HueOffset = 120f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0f, 1f, 0f, 0.7f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SaturationLayer_NegativeSaturationDesaturates()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.red
        });
        recipe.RootStack.Layers.Add(new SaturationLayer
        {
            Saturation = -1f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.5f, 0.5f, 0.5f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SaturationLayer_PositiveLuminanceLightens()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.6f, 1f)
        });
        recipe.RootStack.Layers.Add(new SaturationLayer
        {
            Luminance = 1f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, Color.white);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_EncodesVerticalThresholdEdge()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(5, 5);
        var source = CreateVerticalStepTexture(5, 5, 2, Color.black, Color.white);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            SpreadPixels = 2f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        var pixels = ReadPixels(result);
        Assert.That(PixelRed(pixels, 5, 1, 2), Is.EqualTo(0.375f).Within(0.04f));
        Assert.That(PixelRed(pixels, 5, 2, 2), Is.EqualTo(0.625f).Within(0.04f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_ThresholdMovesContour()
    {
        IgnoreUnsupportedCompute();

        var lowThresholdRecipe = CreateRecipe(5, 5);
        var highThresholdRecipe = CreateRecipe(5, 5);
        var source = CreateVerticalStepTexture(5, 5, 2, Color.black, Color.white);
        lowThresholdRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        lowThresholdRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            Threshold = 0.25f,
            SpreadPixels = 2f
        });
        highThresholdRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        highThresholdRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            Threshold = 0.75f,
            SpreadPixels = 2f
        });

        var lowThreshold = TextureRecipeEvaluator.Evaluate(lowThresholdRecipe);
        var highThreshold = TextureRecipeEvaluator.Evaluate(highThresholdRecipe);

        Assert.That(lowThreshold, Is.Not.Null);
        Assert.That(highThreshold, Is.Not.Null);
        Assert.That(
            PixelRed(ReadPixels(lowThreshold), 5, 1, 2),
            Is.GreaterThan(PixelRed(ReadPixels(highThreshold), 5, 1, 2) + 0.1f));
        LogAssert.NoUnexpectedReceived();

        Release(lowThreshold);
        Release(highThreshold);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(lowThresholdRecipe);
        Object.DestroyImmediate(highThresholdRecipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_EdgeValueControlsZeroEncoding()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(5, 5);
        var source = CreateVerticalStepTexture(5, 5, 2, Color.black, Color.white);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            SpreadPixels = 1f,
            EdgeValue = 0.25f
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        Assert.That(PixelRed(ReadPixels(result), 5, 2, 2), Is.EqualTo(0.625f).Within(0.04f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_UsesSelectedInputChannel()
    {
        IgnoreUnsupportedCompute();

        var redRecipe = CreateRecipe(5, 5);
        var greenRecipe = CreateRecipe(5, 5);
        var source = CreateVerticalStepTexture(
            5,
            5,
            2,
            Color.black,
            new Color(0f, 1f, 0f, 1f));
        redRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        redRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            InputUsage = MaskUsage.R,
            SpreadPixels = 2f
        });
        greenRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        greenRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            InputUsage = MaskUsage.G,
            SpreadPixels = 2f
        });

        var red = TextureRecipeEvaluator.Evaluate(redRecipe);
        var green = TextureRecipeEvaluator.Evaluate(greenRecipe);

        Assert.That(red, Is.Not.Null);
        Assert.That(green, Is.Not.Null);
        Assert.That(PixelRed(ReadPixels(red), 5, 2, 2), Is.LessThan(0.02f));
        Assert.That(PixelRed(ReadPixels(green), 5, 2, 2), Is.GreaterThan(0.5f));
        LogAssert.NoUnexpectedReceived();

        Release(red);
        Release(green);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(redRecipe);
        Object.DestroyImmediate(greenRecipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_InvertSignFlipsEncoding()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(5, 5);
        var source = CreateVerticalStepTexture(5, 5, 2, Color.black, Color.white);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        recipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            SpreadPixels = 2f,
            InvertSign = true
        });

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        Assert.That(PixelRed(ReadPixels(result), 5, 2, 2), Is.EqualTo(0.375f).Within(0.04f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_NoContourSaturatesOutput()
    {
        IgnoreUnsupportedCompute();

        var insideRecipe = CreateRecipe();
        var outsideRecipe = CreateRecipe();
        insideRecipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.white
        });
        insideRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer());
        outsideRecipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.black
        });
        outsideRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer());

        var inside = TextureRecipeEvaluator.Evaluate(insideRecipe);
        var outside = TextureRecipeEvaluator.Evaluate(outsideRecipe);

        Assert.That(inside, Is.Not.Null);
        Assert.That(outside, Is.Not.Null);
        AssertTexturePixels(inside, Color.white);
        AssertTexturePixels(outside, Color.clear);
        LogAssert.NoUnexpectedReceived();

        Release(inside);
        Release(outside);
        Object.DestroyImmediate(insideRecipe);
        Object.DestroyImmediate(outsideRecipe);
    }

    [Test]
    public void Evaluate_SignedDistanceFieldLayer_SpreadPixelsClampsToMax()
    {
        IgnoreUnsupportedCompute();

        var cappedRecipe = CreateRecipe(5, 5);
        var oversizedRecipe = CreateRecipe(5, 5);
        var source = CreateVerticalStepTexture(5, 5, 2, Color.black, Color.white);
        cappedRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        cappedRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            SpreadPixels = SignedDistanceFieldLayer.MaxSpreadPixels
        });
        oversizedRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        oversizedRecipe.RootStack.Layers.Add(new SignedDistanceFieldLayer
        {
            SpreadPixels = SignedDistanceFieldLayer.MaxSpreadPixels + 20f
        });

        var capped = TextureRecipeEvaluator.Evaluate(cappedRecipe);
        var oversized = TextureRecipeEvaluator.Evaluate(oversizedRecipe);

        Assert.That(capped, Is.Not.Null);
        Assert.That(oversized, Is.Not.Null);
        AssertTexturesEqual(capped, oversized);
        LogAssert.NoUnexpectedReceived();

        Release(capped);
        Release(oversized);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(cappedRecipe);
        Object.DestroyImmediate(oversizedRecipe);
    }

    [Test]
    public void Evaluate_NormalFromHeightLayer_ConstantHeightReturnsFlatNormalAndPreservesAlpha()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.25f, 0.25f, 0.8f)
        });
        recipe.RootStack.Layers.Add(new NormalFromHeightLayer());

        var result = TextureRecipeEvaluator.Evaluate(recipe);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.5f, 0.5f, 1f, 0.8f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void Evaluate_NormalFromHeightLayer_UsesSelectedHeightChannel()
    {
        IgnoreUnsupportedCompute();

        var flatRecipe = CreateRecipe(4, 4);
        var tiltedRecipe = CreateRecipe(4, 4);
        var source = CreateGreenGradientTexture(4, 4);
        var sourceLayer = new TextureFileLayer
        {
            Source = RuntimeSource(source)
        };

        flatRecipe.RootStack.Layers.Add(sourceLayer);
        flatRecipe.RootStack.Layers.Add(new NormalFromHeightLayer
        {
            HeightUsage = MaskUsage.R
        });
        tiltedRecipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(source)
        });
        tiltedRecipe.RootStack.Layers.Add(new NormalFromHeightLayer
        {
            HeightUsage = MaskUsage.G
        });

        var flat = TextureRecipeEvaluator.Evaluate(flatRecipe);
        var tilted = TextureRecipeEvaluator.Evaluate(tiltedRecipe);

        Assert.That(flat, Is.Not.Null);
        Assert.That(tilted, Is.Not.Null);
        AssertFlatNormal(flat);
        Assert.That(ContainsTiltedNormal(tilted), Is.True);
        LogAssert.NoUnexpectedReceived();

        Release(flat);
        Release(tilted);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(flatRecipe);
        Object.DestroyImmediate(tiltedRecipe);
    }

    [Test]
    public void Evaluate_InvalidRecipe_ReturnsNull()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        recipe.Output.Resolution = Vector2Int.zero;

        Assert.That(TextureRecipeEvaluator.Evaluate(recipe), Is.Null);
        LogAssert.NoUnexpectedReceived();

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

    static Texture2D CreateRowTexture(Color[] colors)
    {
        var texture = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (var x = 0; x < colors.Length; x++)
            texture.SetPixel(x, 0, colors[x]);

        texture.Apply();
        return texture;
    }

    static Texture2D CreateVerticalStepTexture(int width, int height, int insideStartX, Color outside, Color inside)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, x >= insideStartX ? inside : outside);

        texture.Apply();
        return texture;
    }

    static Texture2D CreateRedImpulseTexture(int width, int height, int impulseX, int impulseY)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, x == impulseX && y == impulseY ? Color.red : Color.clear);

        texture.Apply();
        return texture;
    }

    static Texture2D CreatePatternTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, new Color(
                x == 0 && y == 0 ? 1f : 0f,
                x / (float)(width - 1),
                y / (float)(height - 1),
                0.5f));

        texture.Apply();
        return texture;
    }

    static Texture2D CreateGreenGradientTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, new Color(0.25f, x / (float)(width - 1), 0f, 1f));

        texture.Apply();
        return texture;
    }

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

    static void AssertFlatNormal(RenderTexture renderTexture)
    {
        foreach (var pixel in ReadPixels(renderTexture))
            AssertColor(pixel, new Color(0.5f, 0.5f, 1f, 1f), "Flat normal");
    }

    static bool ContainsTiltedNormal(RenderTexture renderTexture)
    {
        foreach (var pixel in ReadPixels(renderTexture))
        {
            if (Mathf.Abs(pixel.r - 0.5f) > 0.02f || Mathf.Abs(pixel.g - 0.5f) > 0.02f)
                return true;
        }

        return false;
    }

    static float AverageRed(RenderTexture renderTexture)
    {
        var pixels = ReadPixels(renderTexture);
        var total = 0f;

        for (var i = 0; i < pixels.Length; i++)
            total += pixels[i].r;

        return total / pixels.Length;
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
}
