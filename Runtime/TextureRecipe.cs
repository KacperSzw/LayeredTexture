using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unmanaged.LayeredTexture
{
    [CreateAssetMenu(menuName = "LayeredTexture/Texture Recipe")]
    public sealed class TextureRecipe : ScriptableObject
    {
        public LayerStack RootStack = new();
        public OutputProfile Output = OutputProfile.Default;
    }

    [Serializable]
    public sealed class LayerStack
    {
        [SerializeReference]
        public List<TextureLayerBase> Layers = new();
        public StackEvalPolicy EvalPolicy;
    }

    [Serializable]
    public struct OutputProfile
    {
        public Vector2Int Resolution;
        public GraphicsFormat WorkingFormat;
        public GraphicsFormat OutputGraphicsFormat;
        public ExportFileFormat ExportFormat;
        public string OutputPath;
        public bool GenerateMips;
        public bool SRGB;

        public static OutputProfile Default => new()
        {
            Resolution = new Vector2Int(1024, 1024),
            WorkingFormat = GraphicsFormat.R16G16B16A16_UNorm,
            OutputGraphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
            ExportFormat = ExportFileFormat.PNG
        };
    }
}
