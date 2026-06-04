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

        public override void Process(BakeContext ctx) => throw new NotImplementedException();
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
