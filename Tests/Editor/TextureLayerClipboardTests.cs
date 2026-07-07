using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEngine;

public sealed class TextureLayerClipboardTests
{
    [TearDown]
    public void TearDown() => TextureLayerClipboard.Clear();

    [Test]
    public void TryClone_PreservesLayerTypeAndValues()
    {
        var source = new SolidColorLayer
        {
            Enabled = false,
            BlendMode = BlendMode.Subtract,
            Opacity = 0.35f,
            WriteMask = ChannelWriteMask.R | ChannelWriteMask.A,
            InputSwizzle = new ChannelSwizzle
            {
                R = SwizzleChannelSource.B,
                G = SwizzleChannelSource.G,
                B = SwizzleChannelSource.R,
                A = SwizzleChannelSource.A
            },
            Color = new Color(0.2f, 0.4f, 0.6f, 0.8f)
        };

        var cloned = TextureLayerClipboard.TryClone(source, out var clone);

        Assert.That(cloned, Is.True);
        Assert.That(clone, Is.TypeOf<SolidColorLayer>());
        Assert.That(clone, Is.Not.SameAs(source));

        var solid = (SolidColorLayer)clone;
        Assert.That(solid.Enabled, Is.False);
        Assert.That(solid.BlendMode, Is.EqualTo(BlendMode.Subtract));
        Assert.That(solid.Opacity, Is.EqualTo(0.35f));
        Assert.That(solid.WriteMask, Is.EqualTo(ChannelWriteMask.R | ChannelWriteMask.A));
        Assert.That(solid.InputSwizzle.R, Is.EqualTo(SwizzleChannelSource.B));
        Assert.That(solid.InputSwizzle.G, Is.EqualTo(SwizzleChannelSource.G));
        Assert.That(solid.InputSwizzle.B, Is.EqualTo(SwizzleChannelSource.R));
        Assert.That(solid.InputSwizzle.A, Is.EqualTo(SwizzleChannelSource.A));
        Assert.That(solid.Color, Is.EqualTo(source.Color));
    }

    [Test]
    public void TryClone_PreservesUnityObjectReferences()
    {
        var texture = new Texture2D(1, 1);
        var source = new TextureFileLayer
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.RuntimeTextureReference,
                RuntimeTexture = texture
            }
        };

        try
        {
            var cloned = TextureLayerClipboard.TryClone(source, out var clone);

            Assert.That(cloned, Is.True);
            Assert.That(clone, Is.TypeOf<TextureFileLayer>());

            var textureLayer = (TextureFileLayer)clone;
            Assert.That(textureLayer.Source.Kind, Is.EqualTo(TextureSourceKind.RuntimeTextureReference));
            Assert.That(textureLayer.Source.RuntimeTexture, Is.SameAs(texture));
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void PasteValues_RejectsMismatchedConcreteType()
    {
        var source = new SolidColorLayer { Color = Color.red };
        var target = new NoiseLayer
        {
            Seed = 23,
            Scale = new Vector2(2f, 7f)
        };

        TextureLayerClipboard.Copy(source);
        var pasted = TextureLayerClipboard.TryPasteValues(target);

        Assert.That(pasted, Is.False);
        Assert.That(target.Seed, Is.EqualTo(23));
        Assert.That(target.Scale, Is.EqualTo(new Vector2(2f, 7f)));
    }

    [Test]
    public void PasteValues_OverwritesSameType()
    {
        var source = new NoiseLayer
        {
            NoiseType = NoiseType.WorleyEdge,
            Fractal = NoiseFractal.Billow,
            Seed = 91,
            Scale = new Vector2(3f, 11f),
            Offset = new Vector2(0.25f, 0.75f),
            Rotation = 33f,
            Octaves = 5,
            Lacunarity = 1.75f,
            Gain = 0.4f,
            Invert = true,
            Contrast = 1.2f,
            Brightness = -0.1f
        };
        var target = new NoiseLayer();

        TextureLayerClipboard.Copy(source);
        var pasted = TextureLayerClipboard.TryPasteValues(target);

        Assert.That(pasted, Is.True);
        Assert.That(target.NoiseType, Is.EqualTo(source.NoiseType));
        Assert.That(target.Fractal, Is.EqualTo(source.Fractal));
        Assert.That(target.Seed, Is.EqualTo(source.Seed));
        Assert.That(target.Scale, Is.EqualTo(source.Scale));
        Assert.That(target.Offset, Is.EqualTo(source.Offset));
        Assert.That(target.Rotation, Is.EqualTo(source.Rotation));
        Assert.That(target.Octaves, Is.EqualTo(source.Octaves));
        Assert.That(target.Lacunarity, Is.EqualTo(source.Lacunarity));
        Assert.That(target.Gain, Is.EqualTo(source.Gain));
        Assert.That(target.Invert, Is.EqualTo(source.Invert));
        Assert.That(target.Contrast, Is.EqualTo(source.Contrast));
        Assert.That(target.Brightness, Is.EqualTo(source.Brightness));
    }

    [Test]
    public void TryCloneCopiedLayer_ReturnsIndependentCopy()
    {
        var source = new SolidColorLayer
        {
            Color = new Color(0.1f, 0.2f, 0.3f, 0.4f)
        };

        TextureLayerClipboard.Copy(source);
        var cloned = TextureLayerClipboard.TryCloneCopiedLayer(out var clone);

        Assert.That(cloned, Is.True);
        Assert.That(clone, Is.TypeOf<SolidColorLayer>());
        Assert.That(clone, Is.Not.SameAs(source));

        ((SolidColorLayer)clone).Color = Color.white;

        Assert.That(source.Color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
    }
}
