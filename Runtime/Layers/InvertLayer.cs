using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class InvertLayer : TextureLayerBase
    {
        public override void Process(BakeContext ctx)
        {
            if (!TryGetShaderKernel(out var shader, out var kernel, out var error))
                throw new InvalidOperationException(error);

            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            LayerCompute.Dispatch(shader, kernel, ctx);
        }

        internal static bool TryGetShaderKernel(out ComputeShader shader, out int kernel, out string error) =>
            LayerCompute.TryGetKernel(LayerCompute.InvertKernel, out shader, out kernel, out error);
    }
}
