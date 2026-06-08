using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Base class for all serializable layer implementations.
    /// </summary>
    [Serializable]
    public abstract class TextureLayerBase
    {
        /// <summary>
        /// Whether the evaluator should process this layer.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// Blend operation used when compositing this layer over the previous result.
        /// </summary>
        public BlendMode BlendMode = BlendMode.Normal;

        /// <summary>
        /// Layer influence applied after the optional mask.
        /// </summary>
        public float Opacity = 1f;

        /// <summary>
        /// Channel remapping applied to this layer candidate before compositing.
        /// </summary>
        public ChannelSwizzle InputSwizzle = ChannelSwizzle.Identity;

        /// <summary>
        /// Output channels this layer is allowed to write.
        /// </summary>
        public ChannelWriteMask WriteMask = ChannelWriteMask.RGBA;

        /// <summary>
        /// Optional recipe-reference mask applied to this layer.
        /// </summary>
        public StackMask Mask = new();

        /// <summary>
        /// Broad editor/runtime role used to present this layer.
        /// </summary>
        public virtual TextureLayerRole Role => TextureLayerRole.Processor;

        /// <summary>
        /// Whether this layer can render a raw, non-composited preview.
        /// </summary>
        public virtual bool SupportsRawPreview => false;

        /// <summary>
        /// Writes this layer result into the active bake context.
        /// </summary>
        /// <param name="ctx">Current bake context containing working textures and mask state.</param>
        public abstract void Process(BakeContext ctx);
    }

    /// <summary>
    /// Recipe-reference mask settings for a layer.
    /// </summary>
    [Serializable]
    public sealed class StackMask
    {
        /// <summary>
        /// Mask channel selection; grayscale uses linear RGB luminance.
        /// </summary>
        public MaskUsage Usage = MaskUsage.Grayscale;

        /// <summary>
        /// Whether to invert the sampled mask value before opacity is applied.
        /// </summary>
        public bool Invert;

        /// <summary>
        /// Multiplier applied to the sampled mask value.
        /// </summary>
        public float Opacity = 1f;

        /// <summary>
        /// Recipe evaluated recursively as this layer's mask. Null means no active mask.
        /// </summary>
        public TextureRecipe RecipeReference;
    }

    /// <summary>
    /// Serializable texture source reference.
    /// </summary>
    [Serializable]
    public struct TextureSource
    {
        /// <summary>
        /// Source lookup mode.
        /// </summary>
        public TextureSourceKind Kind;

        /// <summary>
        /// File path used by file texture sources.
        /// </summary>
        public AssetPath Path;

        /// <summary>
        /// Direct runtime-safe Unity texture reference.
        /// </summary>
        public Texture RuntimeTexture;
    }

    /// <summary>
    /// RGBA channel remap.
    /// </summary>
    [Serializable]
    public struct ChannelSwizzle
    {
        public TextureChannel R;
        public TextureChannel G;
        public TextureChannel B;
        public TextureChannel A;

        /// <summary>
        /// Swizzle that preserves RGBA channel order.
        /// </summary>
        public static ChannelSwizzle Identity => new()
        {
            R = TextureChannel.R,
            G = TextureChannel.G,
            B = TextureChannel.B,
            A = TextureChannel.A
        };
    }

    /// <summary>
    /// Output channel mask used by layers and channel-fill operations.
    /// </summary>
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

    /// <summary>
    /// Single texture channel identifier.
    /// </summary>
    public enum TextureChannel
    {
        R,
        G,
        B,
        A
    }

    /// <summary>
    /// Layer compositing mode.
    /// </summary>
    public enum BlendMode
    {
        Normal = 0,
        Replace = 1,
        Add = 2,
        Subtract = 6,
        Multiply = 3,
        Min = 4,
        Max = 5
    }

    /// <summary>
    /// Mask sampling mode.
    /// </summary>
    public enum MaskUsage
    {
        Grayscale,
        R,
        G,
        B,
        A
    }

    /// <summary>
    /// Broad layer role used by editor UI and future layer organization.
    /// </summary>
    public enum TextureLayerRole
    {
        Source,
        Processor
    }

    /// <summary>
    /// Texture source storage and lookup mode.
    /// </summary>
    public enum TextureSourceKind
    {
        RuntimeTextureReference,
        File
    }

    /// <summary>
    /// Export file format for editor bakes.
    /// </summary>
    public enum ExportFileFormat
    {
        PNG,
        TGA,
        EXR
    }

    /// <summary>
    /// Optional resolver for texture sources that are not direct runtime texture references.
    /// </summary>
    public interface ITextureSourceResolver
    {
        /// <summary>
        /// Resolves a serialized texture source to a sampleable Unity texture.
        /// </summary>
        /// <param name="recipe">Owning recipe used for relative source context.</param>
        /// <param name="source">Serialized texture source to resolve.</param>
        /// <param name="texture">Resolved texture when successful.</param>
        /// <returns>True when the source resolves to a supported texture.</returns>
        bool TryResolve(TextureRecipe recipe, TextureSource source, out Texture texture);
    }
}
