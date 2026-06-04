using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public abstract class TextureLayerBase
    {
        public bool Enabled = true;
        public BlendMode BlendMode = BlendMode.Normal;
        public float Opacity = 1f;
        public ChannelSwizzle InputSwizzle = ChannelSwizzle.Identity;
        public ChannelWriteMask WriteMask = ChannelWriteMask.RGBA;
        public StackMask Mask;
        public LayerFormatPolicy FormatPolicy;

        public abstract void Process(BakeContext ctx);
    }

    [Serializable]
    public sealed class StackMask
    {
        public StackSource Source;
        public MaskUsage Usage = MaskUsage.Grayscale;
        public bool Invert;
        public float Opacity = 1f;
        public LayerStack InlineStack;
        public TextureRecipe RecipeReference;
    }

    [Serializable]
    public sealed class TextureSource
    {
        public TextureSourceKind Kind = TextureSourceKind.RuntimeTextureReference;
        public string ProjectAssetGuid;
        public string ProjectAssetPath;
        public string ExternalRootId;
        public string ExternalRelativePath;
        public Texture RuntimeTexture;
    }

    [Serializable]
    public struct ChannelSwizzle
    {
        public TextureChannel R;
        public TextureChannel G;
        public TextureChannel B;
        public TextureChannel A;

        public static ChannelSwizzle Identity => new()
        {
            R = TextureChannel.R,
            G = TextureChannel.G,
            B = TextureChannel.B,
            A = TextureChannel.A
        };
    }

    [Flags]
    public enum ChannelWriteMask
    {
        R = 1 << 0,
        G = 1 << 1,
        B = 1 << 2,
        A = 1 << 3,
        RGB = R | G | B,
        RGBA = R | G | B | A
    }

    public enum TextureChannel
    {
        R,
        G,
        B,
        A
    }

    public enum BlendMode
    {
        Normal,
        Replace,
        Add,
        Multiply,
        Min,
        Max
    }

    public enum StackEvalPolicy
    {
        Sequential
    }

    public enum StackSource
    {
        None,
        InlineStack,
        RecipeReference
    }

    public enum MaskUsage
    {
        Grayscale,
        R,
        G,
        B,
        A
    }

    public enum LayerFormatPolicy
    {
        Clamp01,
        PreserveRange
    }

    public enum TextureSourceKind
    {
        ProjectAssetRawFile,
        ExternalRootRelative,
        RuntimeTextureReference
    }

    public enum ExportFileFormat
    {
        PNG,
        TGA,
        EXR
    }
}
