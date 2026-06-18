using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class SaturationLayer : TextureLayerBase
    {
        public float HueOffset;
        public float Saturation;
        public float Luminance;

        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.SaturationKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.SaturationSettingsId, new Vector4(
                HueOffset * Mathf.Deg2Rad,
                Saturation,
                Luminance,
                0f));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }
}
