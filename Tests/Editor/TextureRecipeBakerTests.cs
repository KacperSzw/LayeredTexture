using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEditor;
using UnityEngine;
using static LayeredTextureTestUtility;

public sealed class TextureRecipeBakerTests
{
    const string TestFolder = "Assets/LayeredTextureBakerTests";
    const string FixturePsdPath = "Packages/com.unmanaged.layered-texture/Tests/Editor/Resource/LayeredTex_TestPSD.psd";
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
        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
        var recipe = CreateRecipe(Path);
        recipe.Output.SRGB = true;
        recipe.Output.GenerateMips = true;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = color
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            Assert.That(File.Exists(FullPath(Path)), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(Path), Is.Not.Null);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.isReadable, Is.False);
            AssertPngPixels(Path, GammaRgb(color));
            AssertImportedTextureSamples(Path, color);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_SolidColorPngWithSrgbDisabled_WritesLinearRgb()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/SolidLinear.png";
        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
        var recipe = CreateRecipe(Path);
        recipe.Output.SRGB = false;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = color
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            Assert.That(importer.sRGBTexture, Is.False);
            AssertPngPixels(Path, color);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_SolidColorTgaWithSrgbEnabled_WritesGammaEncodedRgb()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/SolidTga.tga";
        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
        var recipe = CreateRecipe(Path);
        recipe.Output.ExportFormat = ExportFileFormat.TGA;
        recipe.Output.SRGB = true;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = color
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            Assert.That(importer.sRGBTexture, Is.True);
            AssertTgaPixels(Path, GammaRgb(color));
            AssertImportedTextureSamples(Path, color);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_DoesNotOverwriteImporterCompression()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/PreserveCompression.png";
        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.white
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();

            Assert.That(TextureRecipeBaker.Bake(recipe, out error), Is.True, error);
            importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_OutputTextureType_AppliesImporterTextureType()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/SingleChannel.png";
        var recipe = CreateRecipe(Path);
        recipe.Output.TextureType = OutputTextureType.SingleChannel;
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.white
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.SingleChannel));
        }
        finally
        {
            DestroyRecipe(recipe);
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
            DestroyRecipe(recipe);
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
            DestroyRecipe(recipe);
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
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithSrgbOutput_DecodesAutoExternalPngAsSrgb()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/AutoSrgbExternalTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "Source.png");
        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
        WriteTexturePng(sourcePath, color);
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.Output.SRGB = true;
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.png")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, color);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithSrgbOutput_LeavesLinearExternalPngRaw()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/LinearSourceExternalTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "Source.png");
        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
        WriteTexturePng(sourcePath, color);
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.Output.SRGB = true;
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.png", TextureSourceColorSpace.Linear)
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, GammaRgb(color));
        }
        finally
        {
            DestroyRecipe(recipe);
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
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeExternalPsd_UsesGlobalRelativeRoot()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/RelativeExternalPsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "Source.psd");
        var pixels = CornerPixels();
        WriteTexturePsd(sourcePath, pixels);
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Source.psd")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, pixels);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeExternalRlePsd_UsesGlobalRelativeRoot()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/RelativeExternalRlePsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "SourceRle.psd");
        var pixels = CornerPixels();
        WriteTexturePsd(sourcePath, pixels, 4, PsdCompression.Rle);
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("SourceRle.psd")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, pixels);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeExternalRgbPsd_DefaultsAlphaToOne()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/RelativeExternalRgbPsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "SourceRgb.psd");
        WriteTexturePsd(sourcePath, CornerPixels(), 3);
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("SourceRgb.psd")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, OpaqueCornerPixels());
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithRelativeExternalGrayscalePsd_ExpandsToRgba()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/RelativeExternalGrayscalePsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "SourceGray.psd");
        WriteGrayscalePsd(sourcePath, new byte[] { 0, 85, 170, 255 });
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("SourceGray.psd")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new[]
            {
                new Color32(0, 0, 0, 255),
                new Color32(85, 85, 85, 255),
                new Color32(170, 170, 170, 255),
                new Color32(255, 255, 255, 255)
            });
        }
        finally
        {
            DestroyRecipe(recipe);
        }
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
            DestroyRecipe(recipe);
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
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithCorruptExternalPsd_SkipsLayer()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/CorruptExternalPsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "Corrupt.psd");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sourcePath));
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.6f, 0.4f, 0.2f, 1f)
        });
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("Corrupt.psd")
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color(0.6f, 0.4f, 0.2f, 1f));
        }
        finally
        {
            DestroyRecipe(recipe);
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
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_UnsavedRecipe_Fails()
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.False);
            Assert.That(error, Is.EqualTo("TextureRecipe asset must be saved before baking."));
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
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithAbsoluteExternalPsd_UsesFilePath()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/AbsoluteExternalPsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "AbsoluteSource.psd");
        var color = new Color32(26, 77, 179, 255);
        WriteTexturePsd(sourcePath, SolidPixels(color));

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.File,
                Path = AssetPath.Absolute(sourcePath)
            }
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, (Color)color);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithFixturePsd_DecodesCompositeRleImage()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/FixturePsdTextureFile.png";
        var recipe = CreateRecipe(Path);
        recipe.Output.Resolution = new Vector2Int(256, 256);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.File,
                Path = AssetPath.Absolute(FullPath(FixturePsdPath))
            }
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixel(Path, 20, 235, Color.white);
            AssertPngPixel(Path, 220, 220, new Color(0.47f, 0.47f, 0.47f, 1f));
            AssertPngPixel(Path, 64, 192, Color.red);
            AssertPngPixel(Path, 190, 128, new Color(0f, 1f, 0.03f, 1f));
            AssertPngPixel(Path, 80, 64, Color.blue);
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithSrgbOutput_DecodesAutoFixturePsdAsSrgb()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/FixturePsdSrgbTextureFile.png";
        var recipe = CreateRecipe(Path);
        recipe.Output.Resolution = new Vector2Int(256, 256);
        recipe.Output.SRGB = true;
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.File,
                Path = AssetPath.Absolute(FullPath(FixturePsdPath))
            }
        });

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixel(Path, 220, 220, new Color(0.47f, 0.47f, 0.47f, 1f));
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_TextureFileLayerWithUpdatedExternalPsd_ReloadsChangedFile()
    {
        IgnoreUnsupportedCompute();

        const string Path = TestFolder + "/UpdatedExternalPsdTextureFile.png";
        var sourcePath = System.IO.Path.Combine(externalTestFolder, "UpdatedSource.psd");
        LayeredTexturePreferences.SetRelativeRoot(externalTestFolder);

        var recipe = CreateRecipe(Path);
        recipe.RootStack.Layers.Add(new TextureFileLayer
        {
            Source = RelativeFileSource("UpdatedSource.psd")
        });

        try
        {
            WriteTexturePsd(sourcePath, SolidPixels(new Color32(26, 77, 179, 255)));
            File.SetLastWriteTimeUtc(sourcePath, System.DateTime.UtcNow.AddMinutes(-2));
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.True, error);
            AssertPngPixels(Path, new Color32(26, 77, 179, 255));

            WriteTexturePsd(sourcePath, SolidPixels(new Color32(204, 51, 102, 255)));
            File.SetLastWriteTimeUtc(sourcePath, System.DateTime.UtcNow);
            Assert.That(TextureRecipeBaker.Bake(recipe, out error), Is.True, error);
            AssertPngPixels(Path, new Color32(204, 51, 102, 255));
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    [Test]
    public void Bake_UnsupportedExportFormat_Fails()
    {
        var recipe = CreateRecipe(TestFolder + "/Unsupported.png");
        recipe.Output.ExportFormat = (ExportFileFormat)999;

        try
        {
            Assert.That(TextureRecipeBaker.Bake(recipe, out var error), Is.False);
            Assert.That(error, Is.EqualTo("TextureRecipe.Output.ExportFormat is unsupported: 999."));
        }
        finally
        {
            DestroyRecipe(recipe);
        }
    }

    static TextureRecipe CreateRecipe(string path)
    {
        var recipe = ScriptableObject.CreateInstance<TextureRecipe>();
        recipe.Output.Resolution = new Vector2Int(2, 2);
        recipe.Output.ExportFormat = ExportFileFormat.PNG;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FullPath(path)));
        AssetDatabase.CreateAsset(recipe, System.IO.Path.ChangeExtension(path, ".asset").Replace('\\', '/'));
        AssetDatabase.SaveAssets();
        return recipe;
    }

    static void DestroyRecipe(TextureRecipe recipe)
    {
        var path = AssetDatabase.GetAssetPath(recipe);

        if (!string.IsNullOrEmpty(path))
            AssetDatabase.DeleteAsset(path);
        else
            Object.DestroyImmediate(recipe);
    }

    static TextureSource RelativeFileSource(
        string relativePath,
        TextureSourceColorSpace colorSpace = TextureSourceColorSpace.Auto) => new()
    {
        Kind = TextureSourceKind.File,
        Path = AssetPath.Relative(relativePath),
        ColorSpace = colorSpace
    };

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

    static void WriteTexturePsd(
        string fullPath,
        Color32[] pixels,
        int channels = 4,
        PsdCompression compression = PsdCompression.Raw)
    {
        WritePsd(fullPath, channels, 3, compression, channel => Channel(pixels, channel));
    }

    static void WriteGrayscalePsd(string fullPath, byte[] values) =>
        WritePsd(fullPath, 1, 1, PsdCompression.Raw, _ => ValuesInPsdRowOrder(values));

    static void WritePsd(
        string fullPath,
        int channels,
        int colorMode,
        PsdCompression compression,
        System.Func<int, byte[]> channelData)
    {
        const int Width = 2;
        const int Height = 2;

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
        using var stream = File.Create(fullPath);
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[] { (byte)'8', (byte)'B', (byte)'P', (byte)'S' });
        WriteUInt16(writer, 1);
        writer.Write(new byte[6]);
        WriteUInt16(writer, channels);
        WriteUInt32(writer, Height);
        WriteUInt32(writer, Width);
        WriteUInt16(writer, 8);
        WriteUInt16(writer, colorMode);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt16(writer, (int)compression);

        if (compression == PsdCompression.Rle)
        {
            for (var channel = 0; channel < channels; channel++)
            for (var y = 0; y < Height; y++)
                WriteUInt16(writer, RleRowLength(channelData(channel), Width, y));
        }

        for (var channel = 0; channel < channels; channel++)
        {
            var data = channelData(channel);

            if (compression == PsdCompression.Rle)
                WriteRleChannel(writer, data, Width, Height);
            else
                writer.Write(data);
        }
    }

    static byte[] Channel(Color32[] pixels, int channel)
    {
        const int Width = 2;
        const int Height = 2;
        var data = new byte[Width * Height];
        var offset = 0;

        for (var y = Height - 1; y >= 0; y--)
        for (var x = 0; x < Width; x++)
            data[offset++] = Component(pixels[y * Width + x], channel);

        return data;
    }

    static byte[] ValuesInPsdRowOrder(byte[] values)
    {
        const int Width = 2;
        const int Height = 2;
        var data = new byte[Width * Height];
        var offset = 0;

        for (var y = Height - 1; y >= 0; y--)
        for (var x = 0; x < Width; x++)
            data[offset++] = values[y * Width + x];

        return data;
    }

    static void WriteUInt16(BinaryWriter writer, int value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    static void WriteUInt32(BinaryWriter writer, int value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    static int RleRowLength(byte[] data, int width, int row) =>
        RowIsRepeating(data, width, row) ? 2 : width + 1;

    static void WriteRleChannel(BinaryWriter writer, byte[] data, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var offset = y * width;

            if (RowIsRepeating(data, width, y))
            {
                writer.Write((byte)(257 - width));
                writer.Write(data[offset]);
            }
            else
            {
                writer.Write((byte)(width - 1));

                for (var x = 0; x < width; x++)
                    writer.Write(data[offset + x]);
            }
        }
    }

    static bool RowIsRepeating(byte[] data, int width, int row)
    {
        var offset = row * width;

        for (var x = 1; x < width; x++)
        {
            if (data[offset + x] != data[offset])
                return false;
        }

        return true;
    }

    static byte Component(Color32 color, int channel) =>
        channel switch
        {
            0 => color.r,
            1 => color.g,
            2 => color.b,
            _ => color.a
        };

    static Color32[] CornerPixels() => new[]
    {
        new Color32(255, 0, 0, 255),
        new Color32(0, 255, 0, 255),
        new Color32(0, 0, 255, 255),
        new Color32(255, 255, 255, 128)
    };

    static Color32[] OpaqueCornerPixels() => new[]
    {
        new Color32(255, 0, 0, 255),
        new Color32(0, 255, 0, 255),
        new Color32(0, 0, 255, 255),
        new Color32(255, 255, 255, 255)
    };

    static Color32[] SolidPixels(Color32 color) => new[]
    {
        color,
        color,
        color,
        color
    };

    static Color GammaRgb(Color color) => new(
        Mathf.LinearToGammaSpace(color.r),
        Mathf.LinearToGammaSpace(color.g),
        Mathf.LinearToGammaSpace(color.b),
        color.a);

    enum PsdCompression
    {
        Raw,
        Rle
    }

    static void AssertImportedTextureSamples(string path, Color expected)
    {
        var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.That(imported, Is.Not.Null);

        var renderTexture = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        try
        {
            renderTexture.Create();
            Graphics.Blit(imported, renderTexture);

            var pixels = ReadPixels(renderTexture);

            for (var i = 0; i < pixels.Length; i++)
                AssertColor(pixels[i], expected, $"Imported pixel {i}");
        }
        finally
        {
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }
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

    static void AssertPngPixels(string path, Color32[] expected)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

        try
        {
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(FullPath(path))), Is.True);
            Assert.That(texture.width, Is.EqualTo(2));
            Assert.That(texture.height, Is.EqualTo(2));

            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
                AssertColor(texture.GetPixel(x, y), (Color)expected[y * texture.width + x], $"Pixel ({x}, {y})");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    static void AssertPngPixel(string path, int x, int y, Color expected)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

        try
        {
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(FullPath(path))), Is.True);
            AssertColor(texture.GetPixel(x, y), expected, $"Pixel ({x}, {y})");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    static void AssertTgaPixels(string path, Color expected)
    {
        var bytes = File.ReadAllBytes(FullPath(path));
        Assert.That(bytes, Has.Length.GreaterThanOrEqualTo(34));
        Assert.That(bytes[2], Is.EqualTo(2));
        Assert.That(bytes[12] | (bytes[13] << 8), Is.EqualTo(2));
        Assert.That(bytes[14] | (bytes[15] << 8), Is.EqualTo(2));
        Assert.That(bytes[16], Is.EqualTo(32));

        var expected32 = (Color32)expected;
        var offset = 18;

        for (var i = 0; i < 4; i++)
        {
            var actual = new Color32(
                bytes[offset + 2],
                bytes[offset + 1],
                bytes[offset],
                bytes[offset + 3]);
            AssertColor((Color)actual, (Color)expected32, $"TGA pixel {i}");
            offset += 4;
        }
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
}
