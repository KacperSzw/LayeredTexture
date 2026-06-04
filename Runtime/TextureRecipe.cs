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
        public OutputProfile Output = new();
    }

    [Serializable]
    public sealed class LayerStack
    {
        [SerializeReference]
        public List<TextureLayerBase> Layers = new();
        public StackEvalPolicy EvalPolicy;
    }

    [Serializable]
    public sealed class OutputProfile
    {
        public Vector2Int Resolution = new(1024, 1024);
        public GraphicsFormat WorkingFormat = GraphicsFormat.R16G16B16A16_UNorm;
        public GraphicsFormat OutputGraphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        public ExportFileFormat ExportFormat = ExportFileFormat.PNG;
        public string OutputPath;
        public bool GenerateMips;
        public bool SRGB;
    }
}
