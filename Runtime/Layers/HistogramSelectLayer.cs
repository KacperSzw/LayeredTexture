using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class HistogramSelectLayer : TextureLayerBase
    {
        public HistogramSelectionMode Mode = HistogramSelectionMode.Luminance;
        public float Position = 0.5f;
        public float Range = 0.25f;
        public float Gradient = 0.1f;

        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.HistogramSelectKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.HistogramSelectSettingsId, new Vector4(
                (int)Mode,
                Position,
                Range,
                Gradient));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }

    public enum HistogramSelectionMode
    {
        Luminance,
        Value
    }
}
