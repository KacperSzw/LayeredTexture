using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public sealed class TextureSetRecipeTests
{
    const string TestFolder = "Assets/TextureSetRecipeTests";
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
    public void Bake_TwoInlineSlots_WritesSiblingFiles()
    {
        IgnoreUnsupportedCompute();

        var set = CreateSet(TestFolder + "/Rock.asset");
        set.Recipes.Add(SolidSlot("Color", Color.red));
        set.Recipes.Add(SolidSlot("Normal", Color.blue));

        Assert.That(TextureSetRecipeBaker.Bake(set, out var error), Is.True, error);
        AssertPngPixels(TestFolder + "/Rock_Color.png", Color.red);
        AssertPngPixels(TestFolder + "/Rock_Normal.png", Color.blue);
    }

    [Test]
    public void Bake_SlotsUseOwnSrgbEncoding()
    {
        IgnoreUnsupportedCompute();

        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
        var set = CreateSet(TestFolder + "/Mixed.asset");
        var srgb = SolidSlot("Color", color);
        var linear = SolidSlot("Mask", color);
        srgb.Output.SRGB = true;
        linear.Output.SRGB = false;
        set.Recipes.Add(srgb);
        set.Recipes.Add(linear);

        Assert.That(TextureSetRecipeBaker.Bake(set, out var error), Is.True, error);
        AssertPngPixels(TestFolder + "/Mixed_Color.png", GammaRgb(color));
        AssertPngPixels(TestFolder + "/Mixed_Mask.png", color);
    }

    [Test]
    public void Bake_SlotTextureType_AppliesImporterTextureType()
    {
        IgnoreUnsupportedCompute();

        var set = CreateSet(TestFolder + "/Types.asset");
        var slot = SolidSlot("Mask", Color.white);
        slot.Output.TextureType = OutputTextureType.SingleChannel;
        set.Recipes.Add(slot);

        Assert.That(TextureSetRecipeBaker.Bake(set, out var error), Is.True, error);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TestFolder + "/Types_Mask.png");
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.SingleChannel));
    }

    [Test]
    public void Bake_DuplicateSanitizedSlotNames_Fails()
    {
        IgnoreUnsupportedCompute();

        var set = CreateSet(TestFolder + "/Duplicate.asset");
        set.Recipes.Add(SolidSlot("Mask A", Color.red));
        set.Recipes.Add(SolidSlot("Mask/A", Color.green));

        Assert.That(TextureSetRecipeBaker.Bake(set, out var error), Is.False);
        Assert.That(error, Is.EqualTo("TextureSetRecipe contains duplicate output name: Mask_A."));
    }

    [Test]
    public void Evaluate_InvertLayer_RespectsWriteMask()
    {
        IgnoreUnsupportedCompute();

        var stack = new LayerStack();
        stack.Layers.Add(new SolidColorLayer
        {
            Color = new Color(0.2f, 0.4f, 0.6f, 0.8f)
        });
        stack.Layers.Add(new InvertLayer
        {
            WriteMask = ChannelWriteMask.G | ChannelWriteMask.A
        });

        var output = TestOutput();
        var renderTexture = TextureRecipeEvaluator.Evaluate(stack, output, null);

        try
        {
            Assert.That(renderTexture, Is.Not.Null);
            AssertColor(ReadPixel(renderTexture), new Color(0.2f, 0.6f, 0.6f, 0.2f));
        }
        finally
        {
            if (renderTexture != null)
                Object.DestroyImmediate(renderTexture);
        }
    }

    [Test]
    public void PbrSetup_FillsMissingSlotsAndPreservesEditedStacks()
    {
        const string SourceFolder = TestFolder + "/Sources";
        WriteTexturePng(FullPath(SourceFolder + "/Rock_BaseColor.png"), Color.red);
        WriteTexturePng(FullPath(SourceFolder + "/Rock_Smoothness.png"), Color.white);
        WriteTexturePng(FullPath(SourceFolder + "/Rock_Metallic.png"), Color.black);
        WriteTexturePng(FullPath(SourceFolder + "/Rock_AO.png"), Color.white);
        WriteTexturePng(FullPath(SourceFolder + "/Rock_Height.png"), Color.gray);
        WriteTexturePng(FullPath(SourceFolder + "/Rock_Normal.png"), Color.blue);
        AssetDatabase.Refresh();
        LayeredTexturePreferences.SetRelativeRoot(FullPath(TestFolder));

        var set = CreateSet(TestFolder + "/Pbr.asset");
        var editedNormal = new TextureSetRecipeSlot
        {
            Name = "Normal"
        };
        editedNormal.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = Color.magenta
        });
        set.Recipes.Add(editedNormal);
        set.PbrSetup.SourceFolder = AssetPath.Relative("Sources");
        set.PbrSetup.Color = true;
        set.PbrSetup.PackAlpha = true;
        set.PbrSetup.ARM = true;
        set.PbrSetup.Mask = false;
        set.PbrSetup.Normal = true;

        Assert.That(TextureSetPbrSetupUtility.Setup(set, out var message), Is.True, message);

        var color = FindSlot(set, "Color");
        var arm = FindSlot(set, "ARM");
        var normal = FindSlot(set, "Normal");

        Assert.That(color, Is.Not.Null);
        Assert.That(color.RootStack.Layers[0], Is.TypeOf<SolidColorLayer>());
        Assert.That(color.RootStack.Layers[1], Is.TypeOf<TextureFileLayer>());

        Assert.That(arm, Is.Not.Null);
        Assert.That(ContainsLayer<InvertLayer>(arm.RootStack), Is.True);
        Assert.That(ContainsLayer<TextureFileLayer>(arm.RootStack), Is.True);

        Assert.That(normal, Is.SameAs(editedNormal));
        Assert.That(normal.RootStack.Layers, Has.Count.EqualTo(1));
        Assert.That(((SolidColorLayer)normal.RootStack.Layers[0]).Color, Is.EqualTo(Color.magenta));
    }

    [Test]
    public void AssetPathPickerAttributes_DefineTextureAndFolderFields()
    {
        Assert.That(PickerKind(typeof(TextureSource), "Path"), Is.EqualTo(AssetPathPickerKind.TextureFile));
        Assert.That(
            PickerKind(typeof(TextureSetPbrSetupSettings), "SourceFolder"),
            Is.EqualTo(AssetPathPickerKind.Folder));
    }

    static TextureSetRecipe CreateSet(string assetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(assetPath)));
        var set = ScriptableObject.CreateInstance<TextureSetRecipe>();
        AssetDatabase.CreateAsset(set, assetPath);
        AssetDatabase.SaveAssets();
        return set;
    }

    static TextureSetRecipeSlot SolidSlot(string name, Color color)
    {
        var slot = new TextureSetRecipeSlot
        {
            Name = name,
            Output = TestOutput()
        };
        slot.RootStack.Layers.Add(new SolidColorLayer
        {
            Color = color
        });
        return slot;
    }

    static OutputProfile TestOutput()
    {
        var output = OutputProfile.Default;
        output.Resolution = new Vector2Int(2, 2);
        output.ExportFormat = ExportFileFormat.PNG;
        return output;
    }

    static TextureSetRecipeSlot FindSlot(TextureSetRecipe set, string name)
    {
        for (var i = 0; i < set.Recipes.Count; i++)
        {
            if (set.Recipes[i] != null && set.Recipes[i].Name == name)
                return set.Recipes[i];
        }

        return null;
    }

    static bool ContainsLayer<T>(LayerStack stack)
    {
        for (var i = 0; i < stack.Layers.Count; i++)
        {
            if (stack.Layers[i] is T)
                return true;
        }

        return false;
    }

    static AssetPathPickerKind PickerKind(System.Type type, string fieldName)
    {
        var attributes = type.GetField(fieldName)
            .GetCustomAttributes(typeof(AssetPathPickerAttribute), false);
        Assert.That(attributes, Has.Length.EqualTo(1));
        return ((AssetPathPickerAttribute)attributes[0]).Kind;
    }

    static Color ReadPixel(RenderTexture renderTexture)
    {
        var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false, true);
        var active = RenderTexture.active;

        try
        {
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            return texture.GetPixel(0, 0);
        }
        finally
        {
            RenderTexture.active = active;
            Object.DestroyImmediate(texture);
        }
    }

    static void WriteTexturePng(string fullPath, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
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

    static Color GammaRgb(Color color) => new(
        Mathf.LinearToGammaSpace(color.r),
        Mathf.LinearToGammaSpace(color.g),
        Mathf.LinearToGammaSpace(color.b),
        color.a);

    static void AssertPngPixels(string assetPath, Color expected)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);

        try
        {
            Assert.That(File.Exists(FullPath(assetPath)), Is.True);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(FullPath(assetPath))), Is.True);

            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
                AssertColor(texture.GetPixel(x, y), expected);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
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
