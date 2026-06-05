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

    [SetUp]
    public void SetUp() => DeleteTestFolder();

    [TearDown]
    public void TearDown() => DeleteTestFolder();

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
    public void Bake_TextureFileLayerWithRelativeProjectSource_UsesSourceDirectory()
    {
        IgnoreUnsupportedCompute();

        const string SourceFolder = TestFolder + "/Sources";
        const string SourcePath = SourceFolder + "/Source.asset";
        const string Path = TestFolder + "/RelativeTextureFile.png";
        CreateTextureAsset(SourcePath, new Color(0.2f, 0.6f, 0.8f, 1f));

        var recipe = CreateRecipe(Path);
        recipe.SourceDirectory = RelativePath.FromAssetPath(SourceFolder);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = ProjectSource("Source.asset")
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
    public void Bake_TextureFileLayerWithMissingRelativeProjectSource_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/MissingRelativeTextureFile.png";
        var recipe = CreateRecipe(Path);
        recipe.SourceDirectory = RelativePath.FromAssetPath(TestFolder + "/Sources");
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.6f, 0.4f, 0.2f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = ProjectSource("Missing.asset")
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
    public void Bake_RelativeProjectSourceCannotEscapeSourceDirectory()
    {
        IgnoreUnsupportedCompute();

        const string SourceFolder = TestFolder + "/Sources";
        const string Path = TestFolder + "/RelativeEscape.png";
        CreateTextureAsset(TestFolder + "/Outside.asset", Color.white);

        var recipe = CreateRecipe(Path);
        recipe.SourceDirectory = RelativePath.FromAssetPath(SourceFolder);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.25f, 0.5f, 0.75f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = ProjectSource("../Outside.asset")
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

    static TextureSource ProjectSource(string relativePath) => new()
    {
        Kind = TextureSourceKind.ProjectAssetRawFile,
        ProjectAssetPath = relativePath
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
