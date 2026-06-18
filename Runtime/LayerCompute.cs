using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Shared compute shader access and parameter binding for all layer kernels.
    /// </summary>
    static class LayerCompute
    {
        internal const string SolidColorKernel = "SolidColor";
        internal const string TextureFileKernel = "TextureFile";
        internal const string NoiseKernel = "Noise";
        internal const string WarpKernel = "Warp";
        internal const string NormalFromHeightKernel = "NormalFromHeight";
        internal const string WaterWavesKernel = "WaterWaves";
        internal const string BlurHorizontalKernel = "BlurHorizontal";
        internal const string BlurVerticalKernel = "BlurVertical";
        internal const string TransformKernel = "Transform";
        internal const string InvertKernel = "Invert";
        internal const string HistogramSelectKernel = "HistogramSelect";
        internal const string SaturationKernel = "Saturation";

        const string ShaderPath = "LayeredTexture/SolidColor";

        static ComputeShader shader;

        internal static readonly int SourceId = Shader.PropertyToID("_Source");
        internal static readonly int ColorId = Shader.PropertyToID("_Color");
        internal static readonly int NoiseSettingsId = Shader.PropertyToID("_NoiseSettings");
        internal static readonly int NoiseTransformId = Shader.PropertyToID("_NoiseTransform");
        internal static readonly int NoiseRotationId = Shader.PropertyToID("_NoiseRotation");
        internal static readonly int NoiseFractalSettingsId = Shader.PropertyToID("_NoiseFractalSettings");
        internal static readonly int NoiseOutputSettingsId = Shader.PropertyToID("_NoiseOutputSettings");
        internal static readonly int WarpSettingsId = Shader.PropertyToID("_WarpSettings");
        internal static readonly int HeightUsageId = Shader.PropertyToID("_HeightUsage");
        internal static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
        internal static readonly int WaterWaveSettingsId = Shader.PropertyToID("_WaterWaveSettings");
        internal static readonly int WaterWaveDirectionId = Shader.PropertyToID("_WaterWaveDirection");
        internal static readonly int WaterWaveCyclesId = Shader.PropertyToID("_WaterWaveCycles");
        internal static readonly int WaterWaveOutputId = Shader.PropertyToID("_WaterWaveOutput");
        internal static readonly int WaterWaveFoamId = Shader.PropertyToID("_WaterWaveFoam");
        internal static readonly int BlurCenterWeightId = Shader.PropertyToID("_BlurCenterWeight");
        internal static readonly int BlurPairCountId = Shader.PropertyToID("_BlurPairCount");
        internal static readonly int BlurPairsId = Shader.PropertyToID("_BlurPairs");
        internal static readonly int TransformSettingsId = Shader.PropertyToID("_TransformSettings");
        internal static readonly int TransformPivotRotationId = Shader.PropertyToID("_TransformPivotRotation");
        internal static readonly int HistogramSelectSettingsId = Shader.PropertyToID("_HistogramSelectSettings");
        internal static readonly int SaturationSettingsId = Shader.PropertyToID("_SaturationSettings");

        internal static readonly int ResultId = Shader.PropertyToID("_Result");
        internal static readonly int PreviousId = Shader.PropertyToID("_Previous");
        internal static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
        static readonly int InputSwizzleId = Shader.PropertyToID("_InputSwizzle");
        static readonly int WriteMaskId = Shader.PropertyToID("_WriteMask");
        static readonly int MaskId = Shader.PropertyToID("_Mask");
        static readonly int UseMaskId = Shader.PropertyToID("_UseMask");
        static readonly int MaskUsageId = Shader.PropertyToID("_MaskUsage");
        static readonly int MaskInvertId = Shader.PropertyToID("_MaskInvert");
        static readonly int MaskOpacityId = Shader.PropertyToID("_MaskOpacity");
        static readonly int RawPreviewId = Shader.PropertyToID("_RawPreview");

        /// <summary>
        /// Resolves a compute kernel and returns a validation-style error instead of throwing.
        /// </summary>
        /// <param name="kernelName">Kernel name inside the shared LayeredTexture compute shader.</param>
        /// <param name="shader">Loaded compute shader when available.</param>
        /// <param name="kernel">Resolved kernel index when available.</param>
        /// <param name="error">Failure reason when resolution fails.</param>
        /// <returns>True when the shader and kernel are available.</returns>
        internal static bool TryGetKernel(string kernelName, out ComputeShader shader, out int kernel, out string error)
        {
            shader = LayerCompute.shader != null
                ? LayerCompute.shader
                : Resources.Load<ComputeShader>(ShaderPath);
            LayerCompute.shader = shader;
            kernel = -1;

            if (shader == null)
            {
                error = $"{ShaderPath} compute shader is missing.";
                return false;
            }

            try
            {
                kernel = shader.FindKernel(kernelName);
            }
            catch (Exception exception)
            {
                error = $"{ShaderPath} compute shader is missing kernel {kernelName}: {exception.Message}";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Resolves a compute kernel for layer execution.
        /// </summary>
        /// <param name="kernelName">Kernel name inside the shared LayeredTexture compute shader.</param>
        /// <param name="shader">Loaded compute shader.</param>
        /// <param name="kernel">Resolved kernel index.</param>
        internal static void GetKernelOrThrow(string kernelName, out ComputeShader shader, out int kernel)
        {
            if (!TryGetKernel(kernelName, out shader, out kernel, out var error))
                throw new InvalidOperationException(error);
        }

        /// <summary>
        /// Binds compositing, mask, swizzle, and output state shared by layer kernels.
        /// </summary>
        /// <param name="shader">Compute shader that owns the kernel.</param>
        /// <param name="kernel">Kernel receiving common LayeredTexture parameters.</param>
        /// <param name="ctx">Bake context that owns working textures and mask state.</param>
        /// <param name="opacity">Layer opacity applied during common compositing.</param>
        /// <param name="blendMode">Blend mode applied during common compositing.</param>
        /// <param name="inputSwizzle">Input channel swizzle applied to candidate pixels.</param>
        /// <param name="writeMask">Target channel mask applied to the final result.</param>
        internal static void SetCommon(
            ComputeShader shader,
            int kernel,
            BakeContext ctx,
            float opacity,
            BlendMode blendMode,
            ChannelSwizzle inputSwizzle,
            ChannelWriteMask writeMask)
        {
            shader.SetTexture(kernel, ResultId, ctx.current);
            shader.SetTexture(kernel, PreviousId, ctx.previous);
            shader.SetVector(ResolutionId, new Vector4(ctx.resolution.x, ctx.resolution.y, 0f, 0f));
            shader.SetFloat(OpacityId, opacity);
            shader.SetInt(BlendModeId, (int)blendMode);
            shader.SetVector(InputSwizzleId, new Vector4(
                (int)inputSwizzle.R,
                (int)inputSwizzle.G,
                (int)inputSwizzle.B,
                (int)inputSwizzle.A));
            shader.SetInt(WriteMaskId, (int)writeMask);

            var useMask = ctx.mask != null && ctx.activeMask != null;
            shader.SetTexture(kernel, MaskId, useMask ? ctx.mask : Texture2D.whiteTexture);
            shader.SetInt(UseMaskId, useMask ? 1 : 0);
            shader.SetInt(MaskUsageId, useMask ? (int)ctx.activeMask.Usage : (int)MaskUsage.Grayscale);
            shader.SetInt(MaskInvertId, useMask && ctx.activeMask.Invert ? 1 : 0);
            shader.SetFloat(MaskOpacityId, useMask ? ctx.activeMask.Opacity : 1f);
            shader.SetInt(RawPreviewId, ctx.rawPreview ? 1 : 0);
        }

        internal static void Dispatch(ComputeShader shader, int kernel, BakeContext ctx) =>
            shader.Dispatch(kernel, (ctx.resolution.x + 7) / 8, (ctx.resolution.y + 7) / 8, 1);
    }
}
