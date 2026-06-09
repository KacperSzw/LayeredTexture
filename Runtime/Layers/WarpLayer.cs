using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Processor that distorts the current stack result with tileable procedural noise.
    /// </summary>
    [Serializable]
    public sealed class WarpLayer : TextureLayerBase
    {
        public int Seed = 1;
        public float Strength = 0.05f;
        public float Scale = 4f;
        public int Octaves = 2;

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.WarpKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.WarpSettingsId, new Vector4(
                Strength,
                Scale,
                Octaves,
                Seed));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }
}
