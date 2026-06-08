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

    static Texture2D CreateGreenGradientTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point
        };

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, new Color(0.25f, x / (float)(width - 1), 0f, 1f));

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
