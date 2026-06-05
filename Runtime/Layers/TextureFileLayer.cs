using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Layer that samples one texture source and composites it into the stack.
    /// </summary>
    [Serializable]
    public sealed class TextureFileLayer : TextureLayerBase
    {
        /// <summary>
        /// Texture source sampled by this layer.
        /// </summary>
        public TextureSource Source;

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            if (!TryGetShaderKernel(out var shader, out var kernel, out var error))
                throw new InvalidOperationException(error);

            if (!TextureSourceUtility.TryResolve(ctx.recipe, Source, ctx.sourceResolver, out var texture))
                throw new InvalidOperationException("TextureFileLayer.Source is unresolved.");

            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetTexture(kernel, LayerCompute.SourceId, texture);
            LayerCompute.Dispatch(shader, kernel, ctx);
        }

        internal static bool TryGetShaderKernel(out ComputeShader shader, out int kernel, out string error) =>
            LayerCompute.TryGetKernel(LayerCompute.TextureFileKernel, out shader, out kernel, out error);
    }
}
