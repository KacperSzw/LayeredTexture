using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

public sealed class LayeredTextureArrayBakerTests
{
    const string TestFolder = "Assets/LayeredTextureArrayBakerTests";
    string previousRelativeRoot;

    [SetUp]
    public void SetUp()
    {
        previousRelativeRoot = LayeredTexturePreferences.RelativeRoot;
        LayeredTexturePreferences.SetRelativeRoot(null);
        DeleteTestFolder();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTestFolder();
        LayeredTexturePreferences.SetRelativeRoot(previousRelativeRoot);
    }

    [Test]
    public void LayeredTextureArray_DefaultOutput_UsesExpectedSettings()
    {
        var array = ScriptableObject.CreateInstance<LayeredTextureArray>();

        try
        {
            Assert.That(array.Output.Resolution, Is.EqualTo(new Vector2Int(1024, 1024)));
            Assert.That(array.Output.WorkingFormat, Is.EqualTo(GraphicsFormat.R16G16B16A16_UNorm));
            Assert.That(array.Output.OutputPath, Is.Null);
            Assert.That(array.Output.GenerateMips, Is.False);
            Assert.That(array.Output.SRGB, Is.False);
            Assert.That(array.Pages, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_TwoSolidRecipePages_WritesTexture2DArrayAssetInOrder()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/Array.asset";
        var array = CreateArray(Path, 2, 2);
        var first = CreateSolidRecipe(Color.red);
        var second = CreateSolidRecipe(Color.green);
        array.Pages.Add(first);
        array.Pages.Add(second);

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.True, error);

            var textureArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Path);
            Assert.That(textureArray, Is.Not.Null);
            Assert.That(textureArray.width, Is.EqualTo(2));
            Assert.That(textureArray.height, Is.EqualTo(2));
            Assert.That(textureArray.depth, Is.EqualTo(2));
            AssertArrayPixels(textureArray, 0, Color.red);
            AssertArrayPixels(textureArray, 1, Color.green);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_UsesArrayResolutionInsteadOfPageResolution()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/ArrayResolution.asset";
        var array = CreateArray(Path, 4, 2);
        var recipe = CreateSolidRecipe(Color.blue);
        recipe.Output.Resolution = new Vector2Int(1, 1);
        array.Pages.Add(recipe);

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.True, error);

            var textureArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Path);
            Assert.That(textureArray, Is.Not.Null);
            Assert.That(textureArray.width, Is.EqualTo(4));
            Assert.That(textureArray.height, Is.EqualTo(2));
            AssertArrayPixels(textureArray, 0, Color.blue);
        }
        finally
        {
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_RelativeTextureFilePage_UsesEditorSourceResolver()
    {
        IgnoreUnsupportedCompute();

        const string SourceFolder = TestFolder + "/Sources";
        const string SourcePath = SourceFolder + "/Source.asset";
        const string Path = TestFolder + "/RelativeArray.asset";
        CreateTextureAsset(SourcePath, new Color(0.2f, 0.6f, 0.8f, 1f));
        LayeredTexturePreferences.SetRelativeRoot(FullPath(SourceFolder));

        var array = CreateArray(Path, 2, 2);
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.asset")
        });
        array.Pages.Add(recipe);

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.True, error);

            var textureArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Path);
            Assert.That(textureArray, Is.Not.Null);
            AssertArrayPixels(textureArray, 0, new Color(0.2f, 0.6f, 0.8f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_EmptyPages_Fails()
    {
        var array = CreateArray(TestFolder + "/Empty.asset", 2, 2);

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Pages is empty."));
        }
        finally
        {
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_MissingPage_Fails()
    {
        var array = CreateArray(TestFolder + "/MissingPage.asset", 2, 2);
        array.Pages.Add(null);

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Pages[0] is missing."));
        }
        finally
        {
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_MissingOutputPath_Fails()
    {
        var array = ScriptableObject.CreateInstance<LayeredTextureArray>();
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Output.OutputPath is missing."));
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_PathOutsideAssets_Fails()
    {
        var array = CreateArray("Temp/Array.asset", 2, 2);
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Output.OutputPath must be under Assets/."));
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_NonAssetExtension_Fails()
    {
        var array = CreateArray(TestFolder + "/Array.png", 2, 2);
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Output.OutputPath extension must be .asset."));
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            Object.DestroyImmediate(array);
        }
    }

    [Test]
    public void Bake_InvalidPageRecipe_Fails()
    {
        IgnoreUnsupportedCompute();

        var array = CreateArray(TestFolder + "/InvalidPage.asset", 2, 2);
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack = null;
        array.Pages.Add(recipe);

        try
        {
            LogAssert.Expect(LogType.Error, "TextureRecipe.RootStack is missing.");
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Pages[0] evaluation failed."));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(array);
        }
    }

    static LayeredTextureArray CreateArray(string path, int width, int height)
    {
        var array = ScriptableObject.CreateInstance<LayeredTextureArray>();
        array.Output.Resolution = new Vector2Int(width, height);
        array.Output.OutputPath = path;
        return array;
    }

    static TextureRecipe CreateSolidRecipe(Color color)
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = color
        });
        return recipe;
    }

    static TextureSource RelativeFileSource(string relativePath) => new()
    {
        Kind = TextureSourceKind.File,
        Path = AssetPath.Relative(relativePath)
    };

    static void CreateTextureAsset(string assetPath, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(assetPath)));
        AssetDatabase.Refresh();

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, color);

        texture.Apply();
        AssetDatabase.CreateAsset(texture, assetPath);
        AssetDatabase.SaveAssets();
    }

    static void AssertArrayPixels(Texture2DArray textureArray, int page, Color expected)
    {
        foreach (var pixel in textureArray.GetPixels(page))
            AssertColor(pixel, expected);
    }

    static void AssertColor(Color actual, Color expected)
    {
        const float Tolerance = 0.02f;

        Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), "red");
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), "green");
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), "blue");
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance), "alpha");
    }

    static void DeleteTestFolder()
    {
        AssetDatabase.DeleteAsset(TestFolder);

        if (Directory.Exists(FullPath(TestFolder)))
            Directory.Delete(FullPath(TestFolder), true);

        AssetDatabase.Refresh();
    }

    static string FullPath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
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
