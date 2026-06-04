using NUnit.Framework;
using Unmanaged.LayeredTexture;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

public sealed class TextureRecipeValidatorTests
{
    [Test]
    public void TextureRecipe_DefaultOutputProfile_UsesExpectedSettings()
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();

        Assert.That(recipe.Output.Resolution, Is.EqualTo(new Vector2Int(1024, 1024)));
        Assert.That(recipe.Output.WorkingFormat, Is.EqualTo(GraphicsFormat.R16G16B16A16_UNorm));
        Assert.That(recipe.Output.OutputGraphicsFormat, Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
        Assert.That(recipe.Output.ExportFormat, Is.EqualTo(ExportFileFormat.PNG));
        Assert.That(recipe.Output.OutputPath, Is.Null);
        Assert.That(recipe.Output.GenerateMips, Is.False);
        Assert.That(recipe.Output.SRGB, Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void LayerSourceDefaults_AreRuntimeTextureReferences()
    {
        var textureLayer = new TextureFileLayer();
        var channelPack = new ChannelPackLayer();

        Assert.That(textureLayer.Source.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
        Assert.That(channelPack.R.Texture.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
        Assert.That(channelPack.G.Texture.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
        Assert.That(channelPack.B.Texture.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
        Assert.That(channelPack.A.Texture.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
    }

    [Test]
    public void ValidateRuntime_DefaultRecipe_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_SolidColorLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new SolidColorLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_TextureFileLayer_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        var texture = new Texture2D(1, 1);

        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.RuntimeTextureReference,
                RuntimeTexture = texture
            }
        });

        LogAssert.Expect(LogType.Error, "TextureFileLayer is not supported at runtime.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_ChannelPackLayer_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new ChannelPackLayer());

        LogAssert.Expect(LogType.Error, "ChannelPackLayer is not supported at runtime.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_ChannelFillLayer_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new ChannelFillLayer());

        LogAssert.Expect(LogType.Error, "ChannelFillLayer is not supported at runtime.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RecipeReferenceLayer_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer
        {
            Recipe = recipe
        });

        LogAssert.Expect(LogType.Error, "RecipeReferenceLayer is not supported at runtime.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_NonNoneMask_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Mask = new StackMask
            {
                Source = StackSource.InlineStack,
                InlineStack = recipe.RootStack
            }
        });

        LogAssert.Expect(LogType.Error, "Layer masks are not supported at runtime.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_NoneMask_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Mask = new StackMask()
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_InvalidOutputResolution_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = Vector2Int.zero;

        LogAssert.Expect(LogType.Error, "TextureRecipe.Output.Resolution must be positive.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_WorkingFormatWithoutComputeWrites_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.WorkingFormat = GraphicsFormat.R8G8B8A8_SRGB;

        LogAssert.Expect(LogType.Error, "TextureRecipe.Output.WorkingFormat does not support compute writes: R8G8B8A8_SRGB.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    static void IgnoreUnsupportedCompute()
    {
        if (!SystemInfo.supportsComputeShaders)
            Assert.Ignore("Compute shaders are not supported in this editor environment.");

        if (!SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_UNorm, GraphicsFormatUsage.Render))
            Assert.Ignore("Default LayeredTexture working format is not renderable in this editor environment.");

        if (!SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_UNorm, GraphicsFormatUsage.LoadStore))
            Assert.Ignore("Default LayeredTexture working format does not support compute writes in this editor environment.");
    }
}
