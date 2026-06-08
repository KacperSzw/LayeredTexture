using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Main ScriptableObject asset describing one layered texture bake recipe.
    /// </summary>
    [CreateAssetMenu(menuName = "LayeredTexture/Texture Recipe")]
    public sealed class TextureRecipe : ScriptableObject
    {
        /// <summary>
        /// Ordered root layer stack evaluated from top to bottom.
        /// </summary>
        public LayerStack RootStack = new();

        /// <summary>
        /// Output resolution, working format, export format, and import settings.
        /// </summary>
        public OutputProfile Output = OutputProfile.Default;
    }

    /// <summary>
    /// Serializable ordered stack of polymorphic texture layers.
    /// </summary>
    [Serializable]
    public sealed class LayerStack
    {
        /// <summary>
        /// Concrete layer instances evaluated sequentially.
        /// </summary>
        [SerializeReference]
        public List<TextureLayerBase> Layers = new();
    }

    /// <summary>
    /// Bake output configuration for preview, runtime evaluation, and editor export.
    /// </summary>
    [Serializable]
    public struct OutputProfile
    {
        /// <summary>
        /// Width and height of the generated texture.
        /// </summary>
        public Vector2Int Resolution;

        /// <summary>
        /// RenderTexture format used while evaluating layers.
        /// </summary>
        public GraphicsFormat WorkingFormat;

        /// <summary>
        /// Intended final graphics format for the exported/imported texture.
        /// </summary>
        public GraphicsFormat OutputGraphicsFormat;

        /// <summary>
        /// File format used by the editor bake pipeline.
        /// </summary>
        public ExportFileFormat ExportFormat;

        /// <summary>
        /// Whether the generated asset importer should generate mipmaps.
        /// </summary>
        public bool GenerateMips;

        /// <summary>
        /// Whether the generated asset importer should treat the texture as sRGB.
        /// </summary>
        public bool SRGB;

        /// <summary>
        /// Default 1024x1024 PNG output using UNorm working and output formats.
        /// </summary>
        public static OutputProfile Default => new()
        {
            Resolution = new Vector2Int(1024, 1024),
            WorkingFormat = GraphicsFormat.R16G16B16A16_UNorm,
            OutputGraphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
            ExportFormat = ExportFileFormat.PNG
        };
    }
}
