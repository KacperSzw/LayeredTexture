using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unmanaged.LayeredTexture
{
    public sealed class BakeContext : IDisposable
    {
        public GraphicsFormat workingFormat;
        public Vector2Int resolution;
        public RenderTexture current;
        public RenderTexture previous;
        public CommandBuffer cmd;
        public RenderTexture mask;

        public void Dispose()
        {
            Release(current);
            Release(previous);
            Release(mask);
            cmd?.Release();
        }

        static void Release(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
