using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class SignedDistanceFieldLayer : TextureLayerBase
    {
        public const float MaxSpreadPixels = 32f;

        public MaskUsage InputUsage = MaskUsage.Grayscale;
        public float Threshold = 0.5f;
        public float SpreadPixels = 16f;
        public float EdgeValue = 0.5f;
        public bool InvertSign;

        public override void Process(BakeContext ctx)
        {
            SignedDistanceFieldCompute.GetKernelOrThrow(out var shader, out var kernel);
            LayerCompute.SetCommon(shader, kernel, ctx, Opacity, BlendMode, InputSwizzle, WriteMask);
            shader.SetInt(SignedDistanceFieldCompute.InputUsageId, (int)InputUsage);
            var spreadPixels = Mathf.Clamp(SpreadPixels, 0.0001f, MaxSpreadPixels);
            shader.SetVector(SignedDistanceFieldCompute.SettingsId, new Vector4(
                Threshold,
                spreadPixels,
                EdgeValue,
                InvertSign ? 1f : 0f));
            LayerCompute.Dispatch(shader, kernel, ctx);
        }
    }

    static class SignedDistanceFieldCompute
    {
        const string ShaderPath = "LayeredTexture/SignedDistanceField";
        const string Kernel = "SignedDistanceField";
        static ComputeShader shader;

        internal static readonly int SettingsId = Shader.PropertyToID("_SdfSettings");
        internal static readonly int InputUsageId = Shader.PropertyToID("_InputUsage");

        internal static bool TryGetKernel(out ComputeShader shader, out int kernel, out string error)
        {
            shader = SignedDistanceFieldCompute.shader != null
                ? SignedDistanceFieldCompute.shader
                : Resources.Load<ComputeShader>(ShaderPath);
            SignedDistanceFieldCompute.shader = shader;
            kernel = -1;

            if (shader == null)
            {
                error = $"{ShaderPath} compute shader is missing.";
                return false;
            }

            try
            {
                kernel = shader.FindKernel(Kernel);
            }
            catch (Exception exception)
            {
                error = $"{ShaderPath} compute shader is missing kernel {Kernel}: {exception.Message}";
                return false;
            }

            error = null;
            return true;
        }

        internal static void GetKernelOrThrow(out ComputeShader shader, out int kernel)
        {
            if (!TryGetKernel(out shader, out kernel, out var error))
                throw new InvalidOperationException(error);
        }
    }
}
