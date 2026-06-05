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
        public NoiseType NoiseType = NoiseType.Gradient;
        public NoiseFractal Fractal = NoiseFractal.FBM;
        public int Seed = 1;
        public float Scale = 8f;
        public Vector2 Offset;
        public float Rotation;
        public int Octaves = 4;
        public float Lacunarity = 2f;
        public float Gain = 0.5f;
        public float WarpStrength;
        public float WarpScale = 4f;
        public int WarpOctaves = 2;
        public bool Invert;
        public float Contrast = 1f;
        public float Brightness;

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            if (!TryGetShaderKernel(out var shader, out var kernel, out var error))
                throw new InvalidOperationException(error);

            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetVector(LayerCompute.NoiseSettingsId, new Vector4(
                (int)NoiseType,
                (int)Fractal,
                Seed,
                Invert ? 1f : 0f));
            shader.SetVector(LayerCompute.NoiseTransformId, new Vector4(
                Scale,
                Rotation * Mathf.Deg2Rad,
                Offset.x,
                Offset.y));
            shader.SetVector(LayerCompute.NoiseFractalSettingsId, new Vector4(
                Octaves,
                Lacunarity,
                Gain,
                0f));
            shader.SetVector(LayerCompute.NoiseWarpSettingsId, new Vector4(
                WarpStrength,
                WarpScale,
                WarpOctaves,
                0f));
            shader.SetVector(LayerCompute.NoiseOutputSettingsId, new Vector4(
                Contrast,
                Brightness,
                0f,
                0f));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }

        internal static bool TryGetShaderKernel(out ComputeShader shader, out int kernel, out string error) =>
            LayerCompute.TryGetKernel(LayerCompute.NoiseKernel, out shader, out kernel, out error);
    }

    public enum NoiseType
    {
        Value,
        Gradient,
        Simplex,
        WorleyF1,
        WorleyF2,
        WorleyEdge
    }

    public enum NoiseFractal
    {
        None,
        FBM,
        Ridged,
        Turbulence,
        Billow
    }
}
