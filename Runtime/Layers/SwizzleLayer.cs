using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class SwizzleLayer : TextureLayerBase
    {
        public SwizzleChannelSource R = SwizzleChannelSource.R;
        public SwizzleChannelSource G = SwizzleChannelSource.G;
        public SwizzleChannelSource B = SwizzleChannelSource.B;
        public SwizzleChannelSource A = SwizzleChannelSource.A;

        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.SwizzleKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.SwizzleSettingsId, new Vector4(
                (int)R,
                (int)G,
                (int)B,
                (int)A));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }

    public enum SwizzleChannelSource
    {
        R,
        G,
        B,
        A,
        OneMinusR,
        OneMinusG,
        OneMinusB,
        OneMinusA,
        Zero,
        One
    }
}
