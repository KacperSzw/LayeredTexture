using NUnit.Framework;
using UnityEngine;
using static LayeredTextureTestUtility;

public sealed class TexturePreviewGUITests
{
    [Test]
    public void PreviewChannelShader_SrgbDisplayConvertsRgbOnly()
    {
        if (QualitySettings.activeColorSpace != ColorSpace.Linear)
            Assert.Ignore("Preview sRGB display conversion is only active in linear color space.");

        var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);

        AssertColor(RenderPreviewChannel(color, 0, false), new Color(color.r, color.g, color.b, 1f), "Values RGB");
        AssertColor(RenderPreviewChannel(color, 0, true), GammaRgb(color), "sRGB RGB");
        AssertColor(RenderPreviewChannel(color, 4, true), new Color(color.a, color.a, color.a, 1f), "Alpha");
    }

    static Color RenderPreviewChannel(Color color, int channel, bool srgbDisplay)
    {
        var shader = Shader.Find("Hidden/LayeredTexture/PreviewChannel");
        Assert.That(shader, Is.Not.Null);

        var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        var material = new Material(shader);
        var renderTexture = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        try
        {
            source.SetPixel(0, 0, color);
            source.Apply();
            material.SetInt("_Channel", channel);
            material.SetInt("_SrgbDisplay", srgbDisplay ? 1 : 0);
            renderTexture.Create();
            Graphics.Blit(source, renderTexture, material);
            return ReadPixels(renderTexture)[0];
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }
    }

    static Color GammaRgb(Color color) => new(
        Mathf.LinearToGammaSpace(color.r),
        Mathf.LinearToGammaSpace(color.g),
        Mathf.LinearToGammaSpace(color.b),
        1f);
}
