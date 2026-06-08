using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
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
            Assert.That(array.Output.OutputFormat, Is.EqualTo(TextureArrayOutputFormat.RGBA32));
            Assert.That(array.Output.CompressionQuality, Is.EqualTo(TextureArrayCompressionQuality.Normal));
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

            var textureArray = LoadBakedArray(Path);
            Assert.That(textureArray, Is.Not.Null);
            Assert.That(textureArray.width, Is.EqualTo(2));
            Assert.That(textureArray.height, Is.EqualTo(2));
            Assert.That(textureArray.depth, Is.EqualTo(2));
            Assert.That(textureArray.format, Is.EqualTo(TextureFormat.RGBA32));
            AssertArrayPixels(textureArray, 0, Color.red);
            AssertArrayPixels(textureArray, 1, Color.green);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            DestroyArray(array);
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

            var textureArray = LoadBakedArray(Path);
            Assert.That(textureArray, Is.Not.Null);
            Assert.That(textureArray.width, Is.EqualTo(4));
            Assert.That(textureArray.height, Is.EqualTo(2));
            AssertArrayPixels(textureArray, 0, Color.blue);
        }
        finally
        {
            Object.DestroyImmediate(recipe);
            DestroyArray(array);
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

            var textureArray = LoadBakedArray(Path);
            Assert.That(textureArray, Is.Not.Null);
            AssertArrayPixels(textureArray, 0, new Color(0.2f, 0.6f, 0.8f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
            DestroyArray(array);
        }
    }

    [Test]
    public void Bake_CompressedOutput_WritesCompressedTexture2DArray()
    {
        IgnoreUnsupportedCompute();
        IgnoreUnsupportedCompressedOutput(out var outputFormat, out var textureFormat);

        const string Path = TestFolder + "/CompressedArray.asset";
        var array = CreateArray(Path, 4, 4);
        array.Output.OutputFormat = outputFormat;
        array.Output.CompressionQuality = TextureArrayCompressionQuality.Fast;
        array.Pages.Add(CreateSolidRecipe(new Color(0.25f, 0.5f, 0.75f, 1f)));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.True, error);

            var textureArray = LoadBakedArray(Path);
            Assert.That(textureArray, Is.Not.Null);
            Assert.That(textureArray.width, Is.EqualTo(4));
            Assert.That(textureArray.height, Is.EqualTo(4));
            Assert.That(textureArray.depth, Is.EqualTo(1));
            Assert.That(textureArray.format, Is.EqualTo(textureFormat));
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            DestroyArray(array);
        }
    }

    [Test]
    public void Bake_OutputPath_IsSiblingAssetNextToArrayAsset()
    {
        const string Path = TestFolder + "/Nested/Array.asset";
        var array = CreateArray(Path, 2, 2);
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.True, error);

            const string ExpectedPath = TestFolder + "/Nested/Array_Texture2DArray.asset";
            Assert.That(AssetDatabase.GetAssetPath(array), Is.EqualTo(Path));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2DArray>(ExpectedPath), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            DestroyArray(array);
        }
    }

    [Test]
    public void Bake_CompressedOutputWithNonBlockAlignedResolution_Fails()
    {
        IgnoreUnsupportedCompressedOutput(out var outputFormat, out _);

        var array = CreateArray(TestFolder + "/CompressedInvalidSize.asset", 2, 4);
        array.Output.OutputFormat = outputFormat;
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo($"LayeredTextureArray.Output.Resolution must be a multiple of 4 for {outputFormat}."));
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            DestroyArray(array);
        }
    }

    [Test]
    public void Bake_InvalidOutputFormat_Fails()
    {
        var array = CreateArray(TestFolder + "/InvalidFormat.asset", 4, 4);
        array.Output.OutputFormat = (TextureArrayOutputFormat)999;
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Output.OutputFormat is unsupported: 999."));
        }
        finally
        {
            Object.DestroyImmediate(array.Pages[0]);
            DestroyArray(array);
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
            DestroyArray(array);
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
            DestroyArray(array);
        }
    }

    [Test]
    public void Bake_UnsavedArray_Fails()
    {
        var array = ScriptableObject.CreateInstance<LayeredTextureArray>();
        array.Pages.Add(CreateSolidRecipe(Color.white));

        try
        {
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray asset must be saved before baking."));
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
            Assert.That(LayeredTextureArrayBaker.Bake(array, out var error), Is.False);
            Assert.That(error, Is.EqualTo("LayeredTextureArray.Pages[0] evaluation failed."));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(recipe);
            DestroyArray(array);
        }
    }

    static LayeredTextureArray CreateArray(string path, int width, int height)
    {
        var array = ScriptableObject.CreateInstance<LayeredTextureArray>();
        array.Output.Resolution = new Vector2Int(width, height);
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path)));
        AssetDatabase.CreateAsset(array, path);
        AssetDatabase.SaveAssets();
        return array;
    }

    static void DestroyArray(LayeredTextureArray array)
    {
        var path = AssetDatabase.GetAssetPath(array);

        if (!string.IsNullOrEmpty(path))
            AssetDatabase.DeleteAsset(path);
        else
            Object.DestroyImmediate(array);
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

    static Texture2DArray LoadBakedArray(string arrayPath) =>
        AssetDatabase.LoadAssetAtPath<Texture2DArray>(BakedArrayPath(arrayPath));

    static string BakedArrayPath(string arrayPath) =>
        Path.Combine(
                Path.GetDirectoryName(arrayPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(arrayPath) + "_Texture2DArray.asset")
            .Replace('\\', '/');

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

    static void IgnoreUnsupportedCompressedOutput(
        out TextureArrayOutputFormat outputFormat,
        out TextureFormat textureFormat)
    {
        outputFormat = default;
        textureFormat = default;

        if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
            Assert.Ignore("Texture copy is not supported in this editor environment.");

        foreach (var format in new[]
                 {
                     (TextureArrayOutputFormat.BC7, TextureFormat.BC7),
                     (TextureArrayOutputFormat.BC3, TextureFormat.DXT5),
                     (TextureArrayOutputFormat.BC1, TextureFormat.DXT1)
                 })
        {
            if (!SystemInfo.SupportsTextureFormat(format.Item2))
                continue;

            outputFormat = format.Item1;
            textureFormat = format.Item2;
            return;
        }

        Assert.Ignore("No tested Texture2DArray compression format is supported in this editor environment.");
    }
}
