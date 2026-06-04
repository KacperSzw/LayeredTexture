using System;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [Serializable]
    public sealed class TextureFileLayer : TextureLayerBase
    {
        public TextureSource Source = new();

        public override void Process(BakeContext ctx) => throw new NotImplementedException();
    }

    [Serializable]
    public sealed class SolidColorLayer : TextureLayerBase
    {
        public Color Color = Color.white;

        static ComputeShader shader;
        static readonly int ResultId = Shader.PropertyToID("_Result");
        static readonly int PreviousId = Shader.PropertyToID("_Previous");
        static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
        static readonly int WriteMaskId = Shader.PropertyToID("_WriteMask");

        public override void Process(BakeContext ctx)
        {
            shader ??= Resources.Load<ComputeShader>("LayeredTexture/SolidColor");

            if (shader == null)
                throw new InvalidOperationException("LayeredTexture/SolidColor compute shader is missing.");

            var kernel = shader.FindKernel("SolidColor");
            shader.SetTexture(kernel, ResultId, ctx.current);
            shader.SetTexture(kernel, PreviousId, ctx.previous);
            shader.SetVector(ResolutionId, new Vector4(ctx.resolution.x, ctx.resolution.y, 0f, 0f));
            shader.SetVector(ColorId, Color);
            shader.SetFloat(OpacityId, Opacity);
            shader.SetInt(BlendModeId, (int)BlendMode);
            shader.SetInt(WriteMaskId, (int)WriteMask);
            shader.Dispatch(kernel, Mathf.CeilToInt(ctx.resolution.x / 8f), Mathf.CeilToInt(ctx.resolution.y / 8f), 1);
        }
    }

    [Serializable]
    public sealed class ChannelPackLayer : TextureLayerBase
    {
        public ChannelPackSource R = new();
        public ChannelPackSource G = new();
        public ChannelPackSource B = new();
        public ChannelPackSource A = new();

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
    public sealed class ChannelPackSource
    {
        public TextureSource Texture = new();
        public TextureChannel Channel;
        public float DefaultValue;
    }
}
