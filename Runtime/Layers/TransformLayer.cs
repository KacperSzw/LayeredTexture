using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Processor that offsets, scales, and rotates the current stack result with repeat wrapping.
    /// </summary>
    [Serializable]
    public sealed class TransformLayer : TextureLayerBase
    {
        public Vector2 Offset;
        public Vector2 Scale = Vector2.one;
        public float Rotation;
        public Vector2 Pivot = new(0.5f, 0.5f);

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.TransformKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.TransformSettingsId, new Vector4(
                Offset.x,
                Offset.y,
                Scale.x,
                Scale.y));
            shader.SetVector(LayerCompute.TransformPivotRotationId, new Vector4(
                Pivot.x,
                Pivot.y,
                Rotation * Mathf.Deg2Rad,
                0f));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }
}
