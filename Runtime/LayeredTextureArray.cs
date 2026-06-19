using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// ScriptableObject asset that describes a Texture2DArray bake from ordered TextureRecipe pages.
    /// </summary>
    [CreateAssetMenu(menuName = "LayeredTexture/Texture Array")]
    public sealed class LayeredTextureArray : ScriptableObject
    {
        /// <summary>
        /// Shared output settings used for every page in the array bake.
        /// </summary>
        public TextureArrayOutputProfile Output = TextureArrayOutputProfile.Default;

        /// <summary>
        /// Ordered TextureRecipe pages. Page index in this list is the slice index in the baked array.
        /// </summary>
        public List<TextureRecipe> Pages = new();
    }

    /// <summary>
    /// Shared output configuration for baking a Texture2DArray asset.
    /// </summary>
    [Serializable]
    public struct TextureArrayOutputProfile
    {
        /// <summary>
        /// Width and height of every baked array page.
        /// </summary>
        public Vector2Int Resolution;

        /// <summary>
        /// <summary>
        /// Stored Texture2DArray format.
        /// </summary>
        public TextureArrayOutputFormat OutputFormat;

        /// <summary>
        /// Quality used when the output format is block-compressed.
        /// </summary>
        public TextureArrayCompressionQuality CompressionQuality;

        /// <summary>
        /// Whether the generated Texture2DArray should contain mipmaps.
        /// </summary>
        public bool GenerateMips;

        /// <summary>
        /// Whether the generated Texture2DArray should be sampled as sRGB.
        /// </summary>
        public bool SRGB;

        /// <summary>
        /// Default 1024x1024 linear data array output.
        /// </summary>
        public static TextureArrayOutputProfile Default => new()
        {
            Resolution = new Vector2Int(1024, 1024),
            OutputFormat = TextureArrayOutputFormat.RGBA32,
            CompressionQuality = TextureArrayCompressionQuality.Normal
        };

        /// <summary>
        /// Converts array output settings into the recipe output settings needed for page evaluation.
        /// </summary>
        /// <returns>Output profile for one page evaluation.</returns>
        public OutputProfile ToRecipeOutput() => new()
        {
            Resolution = Resolution,
            OutputGraphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
            ExportFormat = ExportFileFormat.PNG,
            GenerateMips = GenerateMips,
            SRGB = SRGB
        };
    }

    /// <summary>
    /// Curated Texture2DArray storage formats supported by the editor bake pipeline.
    /// </summary>
    public enum TextureArrayOutputFormat
    {
        RGBA32,
        BC7,
        BC3,
        BC1
    }

    /// <summary>
    /// Editor compression quality for block-compressed Texture2DArray output.
    /// </summary>
    public enum TextureArrayCompressionQuality
    {
        Normal,
        Fast,
        Best
    }
}
