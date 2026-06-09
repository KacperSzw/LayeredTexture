using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Processor that applies a tileable separable Gaussian blur to the current stack result.
    /// </summary>
    [Serializable]
    public sealed class BlurLayer : TextureLayerBase
    {
        /// <summary>
        /// Unit used when interpreting Radius.
        /// </summary>
        public BlurRadiusMode RadiusMode = BlurRadiusMode.UV;

        /// <summary>
        /// Blur radius in normalized UV units or pixels, depending on RadiusMode.
        /// </summary>
        public float Radius = 0.02f;

        const int MaxRadius = 128;
        const int MaxPairs = MaxRadius / 2;
        static readonly double[] Weights = new double[MaxRadius + 1];
        static readonly Vector4[] Pairs = new Vector4[MaxPairs];

        /// <inheritdoc />
        public override void Process(BakeContext ctx)
        {
            LayerCompute.GetKernelOrThrow(LayerCompute.BlurHorizontalKernel, out var shader, out var horizontal);
            LayerCompute.GetKernelOrThrow(LayerCompute.BlurVerticalKernel, out _, out var vertical);
            var scratch = ctx.GetScratch();
            BuildGaussianPairs(Radius, RadiusMode, ctx.resolution.x, out var centerWeight, out var pairCount);
            LayerCompute.SetCommon(shader, horizontal, ctx, 1f, BlendMode.Replace, ChannelSwizzle.Identity, ChannelWriteMask.RGBA);
            shader.SetTexture(horizontal, LayerCompute.ResultId, scratch);
            SetBlurSettings(shader, centerWeight, pairCount);
            LayerCompute.Dispatch(shader, horizontal, ctx);

            BuildGaussianPairs(Radius, RadiusMode, ctx.resolution.y, out centerWeight, out pairCount);
            LayerCompute.SetCommon(shader, vertical, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetTexture(vertical, LayerCompute.SourceId, scratch);
            SetBlurSettings(shader, centerWeight, pairCount);
            LayerCompute.Dispatch(shader, vertical, ctx);
        }

        static void BuildGaussianPairs(
            float radius,
            BlurRadiusMode radiusMode,
            int dimension,
            out float centerWeight,
            out int pairCount)
        {
            var radiusPx = radiusMode == BlurRadiusMode.UV
                ? Mathf.Clamp01(radius) * dimension
                : radius;
            var pixelRadius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, radiusPx)), 0, MaxRadius);
            pixelRadius = Mathf.Min(pixelRadius, Mathf.Max(0, dimension / 2));

            if (pixelRadius == 0)
            {
                centerWeight = 1f;
                pairCount = 0;
                return;
            }

            var sigma = Math.Max(pixelRadius / 3.0, 0.0001);
            var total = 0.0;

            for (var i = 0; i <= pixelRadius; i++)
            {
                var weight = Math.Exp(-0.5 * (i / sigma) * (i / sigma));
                Weights[i] = weight;
                total += i == 0 ? weight : weight * 2.0;
            }

            centerWeight = (float)(Weights[0] / total);
            pairCount = 0;

            for (var i = 1; i <= pixelRadius; i += 2)
            {
                var first = Weights[i] / total;
                var second = i + 1 <= pixelRadius ? Weights[i + 1] / total : 0.0;
                var pairWeight = first + second;
                Pairs[pairCount] = new Vector4(
                    (float)((i * first + (i + 1) * second) / pairWeight),
                    (float)pairWeight,
                    0f,
                    0f);
                pairCount++;
            }
        }

        static void SetBlurSettings(ComputeShader shader, float centerWeight, int pairCount)
        {
            shader.SetFloat(LayerCompute.BlurCenterWeightId, centerWeight);
            shader.SetInt(LayerCompute.BlurPairCountId, pairCount);
            shader.SetVectorArray(LayerCompute.BlurPairsId, Pairs);
        }
    }

    /// <summary>
    /// Units used by BlurLayer.Radius.
    /// </summary>
    public enum BlurRadiusMode
    {
        UV,
        PerPixel
    }
}
