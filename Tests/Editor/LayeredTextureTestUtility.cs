using System.IO;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

static class LayeredTextureTestUtility
{
    const float ColorTolerance = 0.02f;

    public static Texture2D CreateTexture(Color color) => CreateTexture(2, 2, color);

    public static Texture2D CreateTexture(int width, int height, Color color)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);

        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, color);

        texture.Apply();
        return texture;
    }

    public static TextureSource RuntimeSource(Texture texture) => new()
    {
        Kind = TextureSourceKind.RuntimeTextureReference,
        RuntimeTexture = texture
    };

    public static Color[] ReadPixels(RenderTexture renderTexture)
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

    public static void AssertColor(Color actual, Color expected)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColorTolerance), "Red");
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColorTolerance), "Green");
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColorTolerance), "Blue");
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(ColorTolerance), "Alpha");
    }

    public static void AssertColor(Color actual, Color expected, string message)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColorTolerance), $"{message} red");
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColorTolerance), $"{message} green");
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColorTolerance), $"{message} blue");
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(ColorTolerance), $"{message} alpha");
    }

    public static void Release(RenderTexture texture)
    {
        texture.Release();
        Object.DestroyImmediate(texture);
    }

    public static string FullPath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    public static void CreateTextureAsset(string assetPath, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(assetPath)));
        AssetDatabase.Refresh();

        var texture = CreateTexture(color);
        AssetDatabase.CreateAsset(texture, assetPath);
        AssetDatabase.SaveAssets();
    }

    public static void IgnoreUnsupportedCompute()
    {
        if (!SystemInfo.supportsComputeShaders)
            Assert.Ignore("Compute shaders are not supported in this editor environment.");

        IgnoreUnsupportedWorkingFormat(GraphicsFormat.R16G16B16A16_UNorm, "Default LayeredTexture working format");
    }

    public static void IgnoreUnsupportedWorkingFormat(GraphicsFormat format, string label)
    {
        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
            Assert.Ignore($"{label} is not renderable in this editor environment.");

        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore))
            Assert.Ignore($"{label} does not support compute writes in this editor environment.");
    }
}
