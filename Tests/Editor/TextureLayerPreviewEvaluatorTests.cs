using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

public sealed class TextureLayerPreviewEvaluatorTests
{
    [Test]
    public void SourceLayers_HaveSourceRoleAndRawPreview()
    {
        AssertSourceLayer(new SolidColorLayer());
        AssertSourceLayer(new TextureFileLayer());
        AssertSourceLayer(new NoiseLayer());
        AssertSourceLayer(new WaterWavesLayer());
        AssertSourceLayer(new RecipeReferenceLayer());
    }

    [Test]
    public void NormalFromHeight_IsProcessorWithoutRawPreview()
    {
        var layer = new NormalFromHeightLayer();

        Assert.That(layer.Role, Is.EqualTo(TextureLayerRole.Processor));
        Assert.That(layer.SupportsRawPreview, Is.False);
    }

    [Test]
    public void EvaluateRaw_RecipeReferenceLayer_ReturnsReferencedRecipe()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var reference = CreateRecipe();
        reference.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.6f, 0.8f, 1f)
        });
        var layer = new RecipeReferenceLayer
        {
            Recipe = reference,
            Opacity = 0f,
            WriteMask = (ChannelWriteMask)0
        };

        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, layer);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.2f, 0.6f, 0.8f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(reference);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void EvaluateRaw_SolidColor_IgnoresCompositeControls()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var layer = new SolidColorLayer
        {
            Color = new Color(0.8f, 0.2f, 0.4f, 0.6f),
            BlendMode = BlendMode.Multiply,
            Opacity = 0f,
            WriteMask = (ChannelWriteMask)0
        };

        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, layer);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.8f, 0.2f, 0.4f, 0.6f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void EvaluateRaw_TextureFile_AppliesInputSwizzle()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var source = CreateTexture(new Color(0.2f, 0.4f, 0.8f, 1f));
        var layer = new TextureFileLayer
        {
            Source = RuntimeSource(source),
            InputSwizzle = new ChannelSwizzle
            {
                R = TextureChannel.B,
                G = TextureChannel.G,
                B = TextureChannel.R,
                A = TextureChannel.A
            },
            Opacity = 0f,
            WriteMask = ChannelWriteMask.R
        };

        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, layer);

        Assert.That(result, Is.Not.Null);
        AssertTexturePixels(result, new Color(0.8f, 0.4f, 0.2f, 1f));
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void EvaluateRaw_TextureFileMissingSource_ReturnsNull()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe();
        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, new TextureFileLayer());

        Assert.That(result, Is.Null);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void EvaluateRaw_Noise_ReturnsNonFlatGrayscale()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(32, 32);
        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, new NoiseLayer
        {
            Seed = 9,
            Scale = new Vector2(8f, 8f),
            NoiseType = NoiseType.WorleyF1
        });

        Assert.That(result, Is.Not.Null);
        AssertGrayscaleRgba(result);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void EvaluateRaw_WaterWaves_ReturnsNonFlatGrayscale()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(32, 32);
        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, new WaterWavesLayer
        {
            Seed = 12,
            WaveCount = 8
        });

        Assert.That(result, Is.Not.Null);
        AssertGrayscaleRgba(result);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void EvaluateRaw_WaterWavesFoam_ReturnsNonFlatGrayscale()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(32, 32);
        var result = TextureLayerPreviewEvaluator.EvaluateRaw(recipe, new WaterWavesLayer
        {
            OutputMode = WaterWavesOutputMode.Foam,
            FoamThreshold = 0.55f,
            FoamSoftness = 0.2f
        });

        Assert.That(result, Is.Not.Null);
        AssertGrayscaleRgba(result);
        AssertNonFlat(result);
        LogAssert.NoUnexpectedReceived();

        Release(result);
        Object.DestroyImmediate(recipe);
    }

    static void AssertSourceLayer(TextureLayerBase layer)
    {
        Assert.That(layer.Role, Is.EqualTo(TextureLayerRole.Source));
        Assert.That(layer.SupportsRawPreview, Is.True);
    }

    static TextureRecipe CreateRecipe() => CreateRecipe(2, 2);

    static TextureRecipe CreateRecipe(int width, int height)
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(width, height);
        return recipe;
    }

    static Texture2D CreateTexture(Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

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

    static void AssertTexturePixels(RenderTexture renderTexture, Color expected)
    {
        foreach (var pixel in ReadPixels(renderTexture))
            AssertColor(pixel, expected);
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

        Assert.Fail("Expected non-flat preview output.");
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

    static void AssertColor(Color actual, Color expected)
    {
        const float Tolerance = 0.02f;

        Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), "Red");
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), "Green");
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), "Blue");
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance), "Alpha");
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

        var format = GraphicsFormat.R16G16B16A16_UNorm;

        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
            Assert.Ignore("Default LayeredTexture working format is not renderable in this editor environment.");

        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore))
            Assert.Ignore("Default LayeredTexture working format does not support compute writes in this editor environment.");
    }
}
