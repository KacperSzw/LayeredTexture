using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using static LayeredTextureTestUtility;

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
        Assert.That(recipe.Output.GenerateMips, Is.True);
        Assert.That(recipe.Output.SRGB, Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void LayerSourceDefaults_AreRuntimeTextureReferences()
    {
        var textureLayer = new TextureFileLayer();

        Assert.That(textureLayer.Source.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
    }

    [Test]
    public void BlurLayer_DefaultRadius_IsUvScale()
    {
        var blurLayer = new BlurLayer();

        Assert.That(blurLayer.RadiusMode, Is.EqualTo(BlurRadiusMode.UV));
        Assert.That(blurLayer.Radius, Is.EqualTo(0.02f).Within(0.0001f));
    }

    [Test]
    public void AssetPath_Relative_ResolvesProjectPath()
    {
        var path = AssetPath.Relative("LayeredTexture");

        Assert.That(path.TryGetUnityAssetPath(Application.dataPath, out var assetPath), Is.True);
        Assert.That(assetPath, Is.EqualTo("Assets/LayeredTexture"));
        Assert.That(path.TryGetAbsolutePath(Application.dataPath, out var absolutePath), Is.True);
        Assert.That(absolutePath, Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "LayeredTexture"))));
    }

    [Test]
    public void AssetPath_Absolute_ResolvesProjectPath()
    {
        var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "LayeredTexture"));
        var path = AssetPath.Absolute(absolute);

        Assert.That(path.TryGetUnityAssetPath(null, out var assetPath), Is.True);
        Assert.That(assetPath, Is.EqualTo("Assets/LayeredTexture"));
        Assert.That(path.TryGetAbsolutePath(null, out var absolutePath), Is.True);
        Assert.That(absolutePath, Is.EqualTo(absolute));
    }

    [Test]
    public void AssetPath_RelativeEscapeOutsideRoot_IsInvalid()
    {
        var path = AssetPath.Relative("../Outside");

        Assert.That(path.TryGetAbsolutePath(Application.dataPath, out _), Is.False);
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
    public void ValidateRuntime_TextureFileLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        var texture = new Texture2D(1, 1);

        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_NoiseLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new NoiseLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_WaterWavesLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new WaterWavesLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_NormalFromHeightLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new NormalFromHeightLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_WarpLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new WarpLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_BlurLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(4, 4);
        recipe.RootStack.Layers.Add(new BlurLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_TransformLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(4, 4);
        recipe.RootStack.Layers.Add(new TransformLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_TextureFileLayerWithoutRuntimeTexture_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new TextureFileLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_TextureFileEditorSource_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.File
            }
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_TextureFileResolutionMismatch_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(2, 2);
        var texture = new Texture2D(1, 2);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RuntimeSource(texture)
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RecipeReferenceLayer_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        var reference = CreateRecipe(1, 1);
        reference.RootStack.Layers.Add(new SolidColorLayer());
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer
        {
            Recipe = reference
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(reference);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RecipeReferenceLayerWithoutRecipe_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = CreateRecipe(1, 1);
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer());

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RecursiveRecipeReferenceLayer_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer
        {
            Recipe = recipe
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe, out var error), Is.False);
        Assert.That(error, Is.EqualTo("TextureRecipe mask reference cycle detected."));
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RecipeReferenceMask_IsValid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        var mask = ScriptableObject.CreateInstance<TextureRecipe>();
        mask.RootStack.Layers.Add(new SolidColorLayer());
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Mask = new StackMask
            {
                RecipeReference = mask
            }
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RecursiveMask_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Mask = new StackMask
            {
                RecipeReference = recipe
            }
        });

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe, out var error), Is.False);
        Assert.That(error, Is.EqualTo("TextureRecipe mask reference cycle detected."));
        LogAssert.NoUnexpectedReceived();

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

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe, out var error), Is.False);
        Assert.That(error, Is.EqualTo("TextureRecipe.Output.Resolution must be positive."));
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_WorkingFormatWithoutComputeWrites_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.WorkingFormat = GraphicsFormat.R8G8B8A8_SRGB;

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe, out var error), Is.False);
        Assert.That(error, Is.EqualTo("TextureRecipe.Output.WorkingFormat does not support compute writes: R8G8B8A8_SRGB."));
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(recipe);
    }

    static TextureRecipe CreateRecipe(int width, int height)
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(width, height);
        return recipe;
    }

}
