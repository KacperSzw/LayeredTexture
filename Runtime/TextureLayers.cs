using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class TextureFileLayer : TextureLayerBase
    {
        public TextureSource Source;

        public override void Process(BakeContext ctx) => throw new NotImplementedException();
    }

    [Serializable]
    public sealed class SolidColorLayer : TextureLayerBase
    {
        public Color Color = Color.white;

        const string ShaderPath = "LayeredTexture/SolidColor";
        const string KernelName = "SolidColor";

        static ComputeShader shader;
        static readonly int ResultId = Shader.PropertyToID("_Result");
        static readonly int PreviousId = Shader.PropertyToID("_Previous");
        static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
        static readonly int InputSwizzleId = Shader.PropertyToID("_InputSwizzle");
        static readonly int WriteMaskId = Shader.PropertyToID("_WriteMask");
        static readonly int FormatPolicyId = Shader.PropertyToID("_FormatPolicy");

        public override void Process(BakeContext ctx)
        {
            if (!TryGetShaderKernel(out var shader, out var kernel, out var error))
                throw new InvalidOperationException(error);

            shader.SetTexture(kernel, ResultId, ctx.current);
            shader.SetTexture(kernel, PreviousId, ctx.previous);
            shader.SetVector(ResolutionId, new Vector4(ctx.resolution.x, ctx.resolution.y, 0f, 0f));
            shader.SetVector(ColorId, Color);
            shader.SetFloat(OpacityId, Opacity);
            shader.SetInt(BlendModeId, (int)BlendMode);
            shader.SetVector(InputSwizzleId, new Vector4(
                (int)InputSwizzle.R,
                (int)InputSwizzle.G,
                (int)InputSwizzle.B,
                (int)InputSwizzle.A));
            shader.SetInt(WriteMaskId, (int)WriteMask);
            shader.SetInt(FormatPolicyId, (int)FormatPolicy);
            shader.Dispatch(kernel, (ctx.resolution.x + 7) / 8, (ctx.resolution.y + 7) / 8, 1);
        }

        internal static bool TryGetShaderKernel(out ComputeShader shader, out int kernel, out string error)
        {
            shader = SolidColorLayer.shader != null
                ? SolidColorLayer.shader
                : Resources.Load<ComputeShader>(ShaderPath);
            SolidColorLayer.shader = shader;
            kernel = -1;

            if (shader == null)
            {
                error = $"{ShaderPath} compute shader is missing.";
                return false;
            }

            try
            {
                kernel = shader.FindKernel(KernelName);
            }
            catch (Exception exception)
            {
                error = $"{ShaderPath} compute shader is missing kernel {KernelName}: {exception.Message}";
                return false;
            }

            error = null;
            return true;
        }
    }

    [Serializable]
    public sealed class ChannelPackLayer : TextureLayerBase
    {
        public ChannelPackSource R;
        public ChannelPackSource G;
        public ChannelPackSource B;
        public ChannelPackSource A;

        public override void Process(BakeContext ctx) => throw new NotImplementedException();
    }

    [Serializable]
    public sealed class ChannelFillLayer : TextureLayerBase
    {
        public ChannelWriteMask Channels = ChannelWriteMask.RGBA;
        public float Value = 1f;

        public override void Process(BakeContext ctx) => throw new NotImplementedException();
    }

    [Serializable]
    public sealed class RecipeReferenceLayer : TextureLayerBase
    {
        public TextureRecipe Recipe;

        public override void Process(BakeContext ctx) => throw new NotImplementedException();
    }

    [Serializable]
    public struct ChannelPackSource
    {
        public TextureSource Texture;
        public TextureChannel Channel;
        public float DefaultValue;
    }
}
