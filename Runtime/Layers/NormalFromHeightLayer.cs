using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Processor that derives an OpenGL +Y normal map from the current stack height.
    /// </summary>
    [Serializable]
    public sealed class NormalFromHeightLayer : TextureLayerBase
    {
        public MaskUsage HeightUsage = MaskUsage.R;
        public float Strength = 1f;

        public NormalFromHeightLayer()
        {
            WriteMask = ChannelWriteMask.RGB;
        }

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            if (!TryGetShaderKernel(out var shader, out var kernel, out var error))
                throw new InvalidOperationException(error);

            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetInt(LayerCompute.HeightUsageId, (int)HeightUsage);
            shader.SetFloat(LayerCompute.NormalStrengthId, Strength);
            LayerCompute.Dispatch(shader, kernel, ctx);
        }

        internal static bool TryGetShaderKernel(out ComputeShader shader, out int kernel, out string error) =>
            LayerCompute.TryGetKernel(LayerCompute.NormalFromHeightKernel, out shader, out kernel, out error);
    }
}
