using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Source layer that generates tileable ocean-swell height waves.
    /// </summary>
    [Serializable]
    public sealed class WaterWavesLayer : TextureLayerBase
    {
        public WaterWavesOutputMode OutputMode = WaterWavesOutputMode.Height;
        public int Seed = 1;
        public int WaveCount = 16;
        public float WindDirection;
        public float DirectionSpread = 35f;
        public Vector2 CycleRange = new(2f, 14f);
        public float Amplitude = 1f;
        public float AmplitudeFalloff = 0.75f;
        public float Choppiness = 0.25f;
        public float Phase;
        public float FoamThreshold = 0.72f;
        public float FoamSoftness = 0.12f;
        public float Contrast = 1f;
        public float Brightness;
        public bool Invert;

        /// <inheritdoc />
        public override TextureLayerRole Role => TextureLayerRole.Source;

        /// <inheritdoc />
        public override bool SupportsRawPreview => true;

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.WaterWavesKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.WaterWaveSettingsId, new Vector4(
                Seed,
                WaveCount,
                Amplitude,
                Phase));
            shader.SetVector(LayerCompute.WaterWaveDirectionId, new Vector4(
                WindDirection * Mathf.Deg2Rad,
                DirectionSpread * Mathf.Deg2Rad,
                AmplitudeFalloff,
                Choppiness));
            shader.SetVector(LayerCompute.WaterWaveCyclesId, new Vector4(
                CycleRange.x,
                CycleRange.y,
                0f,
                0f));
            shader.SetVector(LayerCompute.WaterWaveOutputId, new Vector4(
                Contrast,
                Brightness,
                Invert ? 1f : 0f,
                (int)OutputMode));
            shader.SetVector(LayerCompute.WaterWaveFoamId, new Vector4(
                FoamThreshold,
                FoamSoftness,
                0f,
                0f));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }

    public enum WaterWavesOutputMode
    {
        Height,
        Foam
    }
}
