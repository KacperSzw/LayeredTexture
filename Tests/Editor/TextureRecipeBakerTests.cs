using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public sealed class TextureRecipeBakerTests
{
    const string TestFolder = "Assets/LayeredTextureBakerTests";
    readonly string externalTestFolder = Path.Combine(Path.GetTempPath(), "LayeredTextureBakerTests");
    string previousRelativeRoot;

    [SetUp]
    public void SetUp()
    {
        previousRelativeRoot = LayeredTexturePreferences.RelativeRoot;
        LayeredTexturePreferences.SetRelativeRoot(null);
        DeleteTestFolder();
        DeleteExternalTestFolder();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTestFolder();
        DeleteExternalTestFolder();
        LayeredTexturePreferences.SetRelativeRoot(previousRelativeRoot);
    }

    [Test]
    public void Bake_SolidColorPng_WritesFileAndAppliesImporterSettings()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/Solid.png";
        var recipe = CreateRecipe(Path);
        recipe.Output.SRGB = true;
        recipe.Output.GenerateMips = true;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 1f)
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            Assert.That(File.Exists(FullPath(Path)), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(Path), Is.Not.Null);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.isReadable, Is.False);
            AssertPngPixels(Path, new Color(0.25f, 0.5f, 0.75f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithoutRuntimeTexture_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/SkippedTextureFile.png";
        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.6f, 0.4f, 0.2f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer());

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.6f, 0.4f, 0.2f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeFileSource_UsesGlobalRelativeRoot()
    {
        IgnoreUnsupportedCompute();

        const string SourceFolder = TestFolder + "/Sources";
        const string SourcePath = SourceFolder + "/Source.asset";
        const string Path = TestFolder + "/RelativeTextureFile.png";
        CreateTextureAsset(SourcePath, new Color(0.2f, 0.6f, 0.8f, 1f));

        LayeredTexturePreferences.SetRelativeRoot(FullPath(SourceFolder));

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.asset")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.2f, 0.6f, 0.8f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeExternalPng_UsesGlobalRelativeRoot()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/RelativeExternalTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "Source.png");
        WriteTexturePng(sourcePath, new Color(0.2f, 0.6f, 0.8f, 1f));
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.png")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.2f, 0.6f, 0.8f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeExternalTga_UsesGlobalRelativeRoot()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/RelativeExternalTgaTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "Source.tga");
        WriteTextureTga(sourcePath, new Color32(51, 153, 204, 255));
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.tga")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.2f, 0.6f, 0.8f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void RelativeTexturePicker_CollectRelativePaths_ReturnsSupportedFilesBelowRoot()
    {
        var nestedFolder = System.IO.Path.Combine(externalTestFolder, "Nested");
        WriteTexturePng(System.IO.Path.Combine(externalTestFolder, "Albedo.png"), Color.white);
        WriteTextureTga(System.IO.Path.Combine(nestedFolder, "Mask.tga"), new Color32(255, 255, 255, 255));
        Directory.CreateDirectory(nestedFolder);
        File.WriteAllText(System.IO.Path.Combine(nestedFolder, "Notes.txt"), "ignore");

        var paths = RelativeTexturePickerWindow.CollectRelativePaths(externalTestFolder, null);

        Assert.That(paths, Is.EquivalentTo(new[] { "Albedo.png", "Nested/Mask.tga" }));
        Assert.That(RelativeTexturePickerWindow.CollectRelativePaths(externalTestFolder, "mask"), Is.EquivalentTo(new[] { "Nested/Mask.tga" }));
    }

    [Test]
    public void Bake_TextureFileLayerWithMissingRelativeFileSource_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/MissingRelativeTextureFile.png";
        var recipe = CreateRecipe(Path);
        Directory.CreateDirectory(FullPath(TestFolder + "/Sources"));
        AssetDatabase.Refresh();
        LayeredTexturePreferences.SetRelativeRoot(FullPath(TestFolder + "/Sources"));
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.6f, 0.4f, 0.2f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Missing.asset")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.6f, 0.4f, 0.2f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_RelativeFileSourceCannotEscapeGlobalRelativeRoot()
    {
        IgnoreUnsupportedCompute();

        const string SourceFolder = TestFolder + "/Sources";
        const string Path = TestFolder + "/RelativeEscape.png";
        Directory.CreateDirectory(FullPath(SourceFolder));
        CreateTextureAsset(TestFolder + "/Outside.asset", Color.white);

        LayeredTexturePreferences.SetRelativeRoot(FullPath(SourceFolder));

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("../Outside.asset")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.25f, 0.5f, 0.75f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithMissingGlobalRelativeRoot_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/MissingRelativeRoot.png";
        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.6f, 0.4f, 0.2f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.asset")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.6f, 0.4f, 0.2f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_EmptyOutputPath_Fails()
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.False);
            Assert.That(error, Is.EqualTo("TextureRecipe.Output.OutputPath is missing."));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithAbsoluteFileSource_UsesFilePath()
    {
        IgnoreUnsupportedCompute();

        const string SourcePath = TestFolder + "/AbsoluteSource.asset";
        const string Path = TestFolder + "/AbsoluteTextureFile.png";
        CreateTextureAsset(SourcePath, new Color(0.1f, 0.3f, 0.7f, 1f));

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.File,
                Path = AssetPath.Absolute(FullPath(SourcePath))
            }
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.1f, 0.3f, 0.7f, 1f));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_PathOutsideAssets_Fails()
    {
        var recipe = CreateRecipe("Temp/Solid.png");

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.False);
            Assert.That(error, Is.EqualTo("TextureRecipe.Output.OutputPath must be under Assets/."));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    [Test]
    public void Bake_MismatchedExtension_Fails()
    {
        var recipe = CreateRecipe(TestFolder + "/Solid.tga");

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.False);
            Assert.That(error, Is.EqualTo("TextureRecipe.Output.OutputPath extension must be .png for PNG."));
        }
        finally
        {
            Object.DestroyImmediate(recipe);
        }
    }

    static TextureRecipe CreateRecipe(string path)
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(2, 2);
        recipe.Output.OutputPath = path;
        recipe.Output.ExportFormat = ExportFileFormat.PNG;
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

    static void WriteTexturePng(string fullPath, Color color)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

        try
        {
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, color);

            texture.Apply();
            File.WriteAllBytes(fullPath, ImageConversion.EncodeToPNG(texture));
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    static void WriteTextureTga(string fullPath, Color32 color)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
        var bytes = new byte[18 + 2 * 2 * 4];
        bytes[2] = 2;
        bytes[12] = 2;
        bytes[14] = 2;
        bytes[16] = 32;
        bytes[17] = 32;
        var offset = 18;

        for (var i = 0; i < 4; i++)
        {
            bytes[offset++] = color.b;
            bytes[offset++] = color.g;
            bytes[offset++] = color.r;
            bytes[offset++] = color.a;
        }

        File.WriteAllBytes(fullPath, bytes);
    }

    static void AssertPngPixels(string path, Color expected)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

        try
        {
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(FullPath(path))), Is.True);
            Assert.That(texture.width, Is.EqualTo(2));
            Assert.That(texture.height, Is.EqualTo(2));

            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
                AssertColor(texture.GetPixel(x, y), expected, $"Pixel ({x}, {y})");
        }
        finally
        {
            Object.DestroyImmediate(texture);
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

    static void DeleteTestFolder()
    {
        AssetDatabase.DeleteAsset(TestFolder);

        var fullPath = FullPath(TestFolder);

        if (Directory.Exists(fullPath))
            Directory.Delete(fullPath, true);

        if (File.Exists(fullPath + ".meta"))
            File.Delete(fullPath + ".meta");

        AssetDatabase.Refresh();
    }

    void DeleteExternalTestFolder()
    {
        if (Directory.Exists(externalTestFolder))
            Directory.Delete(externalTestFolder, true);
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
