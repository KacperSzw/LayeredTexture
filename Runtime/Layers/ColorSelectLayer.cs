using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class ColorSelectLayer : TextureLayerBase
    {
        public Color TargetColor = Color.white;
        public ColorSelectionMode Mode = ColorSelectionMode.RGB;
        public float Tolerance = 0.1f;
        public float Softness = 0.05f;
        public float MinimumSaturation = 0.05f;

        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.ColorSelectKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.ColorSelectTargetId, TargetColor);
            shader.SetVector(LayerCompute.ColorSelectSettingsId, new Vector4(
                (int)Mode,
                Tolerance,
                Softness,
                MinimumSaturation));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }

    public enum ColorSelectionMode
    {
        RGB,
        Hue,
        HueSaturation,
        HSL
    }
}
