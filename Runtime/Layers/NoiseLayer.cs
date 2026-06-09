using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Layer that generates procedural grayscale noise.
    /// </summary>
    [Serializable]
    public sealed class NoiseLayer : TextureLayerBase
    {
        /// <summary>
        /// Procedural noise algorithm used to generate the base signal.
        /// </summary>
        public NoiseType NoiseType = NoiseType.Gradient;

        /// <summary>
        /// Fractal layering mode applied over the base noise.
        /// </summary>
        public NoiseFractal Fractal = NoiseFractal.FBM;

        /// <summary>
        /// Deterministic random seed for procedural variation.
        /// </summary>
        public int Seed = 1;

        /// <summary>
        /// Tile period count on the X and Y axes.
        /// </summary>
        public Vector2 Scale = new(8f, 8f);

        /// <summary>
        /// Noise-space offset applied after UV scaling.
        /// </summary>
        public Vector2 Offset;

        /// <summary>
        /// Gradient direction rotation in degrees.
        /// </summary>
        public float Rotation;

        /// <summary>
        /// Number of fractal octaves when a fractal mode is active.
        /// </summary>
        public int Octaves = 4;

        /// <summary>
        /// Frequency multiplier between fractal octaves.
        /// </summary>
        public float Lacunarity = 2f;

        /// <summary>
        /// Amplitude multiplier between fractal octaves.
        /// </summary>
        public float Gain = 0.5f;

        /// <summary>
        /// Whether to invert the generated grayscale output.
        /// </summary>
        public bool Invert;

        /// <summary>
        /// Contrast applied around the 0.5 midpoint.
        /// </summary>
        public float Contrast = 1f;

        /// <summary>
        /// Additive brightness offset after contrast.
        /// </summary>
        public float Brightness;

        /// <inheritdoc />
        public override TextureLayerRole Role => TextureLayerRole.Source;

        /// <inheritdoc />
        public override bool SupportsRawPreview => true;

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.NoiseKernel, out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.NoiseSettingsId, new Vector4(
                (int)NoiseType,
                (int)Fractal,
                Seed,
                Invert ? 1f : 0f));
            shader.SetVector(LayerCompute.NoiseTransformId, new Vector4(
                Scale.x,
                Scale.y,
                Offset.x,
                Offset.y));
            shader.SetFloat(LayerCompute.NoiseRotationId, Rotation * Mathf.Deg2Rad);
            shader.SetVector(LayerCompute.NoiseFractalSettingsId, new Vector4(
                Octaves,
                Lacunarity,
                Gain,
                0f));
            shader.SetVector(LayerCompute.NoiseOutputSettingsId, new Vector4(
                Contrast,
                Brightness,
                0f,
                0f));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }

    /// <summary>
    /// Procedural noise algorithm used by NoiseLayer.
    /// </summary>
    public enum NoiseType
    {
        Value,
        Gradient,
        Simplex,
        WorleyF1,
        WorleyF2,
        WorleyEdge
    }

    /// <summary>
    /// Fractal layering mode used by NoiseLayer.
    /// </summary>
    public enum NoiseFractal
    {
        None,
        FBM,
        Ridged,
        Turbulence,
        Billow
    }
}
