using NUnit.Framework;
using Unmanaged.LayeredTexture;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

public sealed class TextureRecipeValidatorTests
{
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
    public void ValidateRuntime_EditorOnlyTextureSource_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.ProjectAssetRawFile
            }
        });

        LogAssert.Expect(LogType.Error, "Runtime validation requires RuntimeTextureReference sources.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_RuntimeTextureReference_IsValid()
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

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.True);
        LogAssert.NoUnexpectedReceived();

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_DirectRecipeCycle_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer
        {
            Recipe = recipe
        });

        LogAssert.Expect(LogType.Error, "Recursive recipe reference detected.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_InlineMaskStackCycle_IsInvalid()
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

        LogAssert.Expect(LogType.Error, "Recursive inline stack detected.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void ValidateRuntime_MissingRecipeReference_IsInvalid()
    {
        IgnoreUnsupportedCompute();

        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new RecipeReferenceLayer());

        LogAssert.Expect(LogType.Error, "Recipe reference is missing.");

        Assert.That(TextureRecipeValidator.ValidateRuntime(recipe), Is.False);

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

    static void IgnoreUnsupportedCompute()
    {
        if (!SystemInfo.supportsComputeShaders)
            Assert.Ignore("Compute shaders are not supported in this editor environment.");

        if (!SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_UNorm, GraphicsFormatUsage.Render))
            Assert.Ignore("Default LayeredTexture working format is not renderable in this editor environment.");
    }
}
