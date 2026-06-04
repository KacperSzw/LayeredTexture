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

        public BakeContext(OutputProfile output)
        {
            workingFormat = output.WorkingFormat;
            resolution = output.Resolution;
            try
            {
                current = CreateTexture("LayeredTexture Current", output);
                previous = CreateTexture("LayeredTexture Previous", output);
                cmd = new CommandBuffer
                {
                    name = "LayeredTexture"
                };
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ClearCurrent(Color color)
        {
            cmd.Clear();
            cmd.SetRenderTarget(current);
            cmd.ClearRenderTarget(false, true, color);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public void SwapCurrentToPrevious()
        {
            var temp = previous;
            previous = current;
            current = temp;
        }

        public RenderTexture DetachCurrent()
        {
            var result = current;
            current = null;
            return result;
        }

        public void Dispose()
        {
            Release(current);
            Release(previous);
            Release(mask);
            current = null;
            previous = null;
            mask = null;
            cmd?.Release();
            cmd = null;
        }

        static RenderTexture CreateTexture(string name, OutputProfile output)
        {
            var descriptor = new RenderTextureDescriptor(output.Resolution.x, output.Resolution.y)
            {
                depthBufferBits = 0,
                enableRandomWrite = true,
                graphicsFormat = output.WorkingFormat,
                msaaSamples = 1,
                autoGenerateMips = false,
                useMipMap = output.GenerateMips
            };

            var texture = new RenderTexture(descriptor)
            {
                name = name
            };

            if (!texture.Create())
            {
                Release(texture);
                throw new InvalidOperationException(
                    $"Failed to create {name} render texture ({output.Resolution.x}x{output.Resolution.y}, {output.WorkingFormat}).");
            }

            return texture;
        }

        static void Release(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
