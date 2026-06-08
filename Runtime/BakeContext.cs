using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unmanaged.LayeredTexture
{
    /// <summary>
    /// Mutable execution state used while evaluating a texture recipe.
    /// </summary>
    public sealed class BakeContext : IDisposable
    {
        /// <summary>
        /// Graphics format used by the working render textures.
        /// </summary>
        public GraphicsFormat workingFormat;

        /// <summary>
        /// Current evaluation resolution.
        /// </summary>
        public Vector2Int resolution;

        /// <summary>
        /// Render texture that receives the current layer result.
        /// </summary>
        public RenderTexture current;

        /// <summary>
        /// Render texture containing the previous stack result.
        /// </summary>
        public RenderTexture previous;

        /// <summary>
        /// Reusable temporary texture for multi-pass layers.
        /// </summary>
        public RenderTexture scratch;

        /// <summary>
        /// Command buffer used for simple render-target operations.
        /// </summary>
        public CommandBuffer cmd;

        /// <summary>
        /// Optional mask texture for the layer currently being processed.
        /// </summary>
        public RenderTexture mask;

        /// <summary>
        /// Mask settings associated with the active mask texture.
        /// </summary>
        public StackMask activeMask;

        /// <summary>
        /// Recipe currently being evaluated.
        /// </summary>
        public TextureRecipe recipe;

        /// <summary>
        /// Optional texture source resolver used during this evaluation.
        /// </summary>
        public ITextureSourceResolver sourceResolver;

        /// <summary>
        /// Whether layer kernels should output their raw candidate instead of compositing.
        /// </summary>
        public bool rawPreview;

        /// <summary>
        /// Creates working render textures for one recipe evaluation.
        /// </summary>
        /// <param name="output">Output profile that defines resolution and working format.</param>
        /// <param name="recipe">Recipe being evaluated.</param>
        /// <param name="sourceResolver">Optional resolver for non-runtime texture sources.</param>
        public BakeContext(OutputProfile output, TextureRecipe recipe = null, ITextureSourceResolver sourceResolver = null)
        {
            workingFormat = output.WorkingFormat;
            resolution = output.Resolution;
            this.recipe = recipe;
            this.sourceResolver = sourceResolver;

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

        /// <summary>
        /// Clears the current working texture to a solid color.
        /// </summary>
        /// <param name="color">Color to write into the current texture.</param>
        public void ClearCurrent(Color color)
        {
            cmd.Clear();
            cmd.SetRenderTarget(current);
            cmd.ClearRenderTarget(false, true, color);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        /// <summary>
        /// Assigns a mask texture and mask settings for the next layer dispatch.
        /// </summary>
        /// <param name="texture">Evaluated mask render texture.</param>
        /// <param name="stackMask">Mask settings used to sample the texture.</param>
        public void SetMask(RenderTexture texture, StackMask stackMask)
        {
            Release(mask);
            mask = texture;
            activeMask = stackMask;
        }

        /// <summary>
        /// Releases and clears the active mask texture.
        /// </summary>
        public void ClearMask()
        {
            Release(mask);
            mask = null;
            activeMask = null;
        }

        /// <summary>
        /// Moves the current stack result into the previous slot before processing the next layer.
        /// </summary>
        public void SwapCurrentToPrevious()
        {
            var temp = previous;
            previous = current;
            current = temp;
        }

        /// <summary>
        /// Returns a reusable full-resolution scratch texture.
        /// </summary>
        public RenderTexture GetScratch()
        {
            if (scratch != null)
                return scratch;

            scratch = CreateTexture("LayeredTexture Scratch", resolution, workingFormat, false);
            scratch.wrapMode = TextureWrapMode.Repeat;
            scratch.filterMode = FilterMode.Bilinear;
            return scratch;
        }

        /// <summary>
        /// Transfers ownership of the current render texture to the caller.
        /// </summary>
        /// <returns>The evaluated render texture.</returns>
        public RenderTexture DetachCurrent()
        {
            var result = current;
            current = null;
            return result;
        }

        /// <summary>
        /// Releases all working GPU resources still owned by this context.
        /// </summary>
        public void Dispose()
        {
            Release(current);
            Release(previous);
            Release(mask);
            Release(scratch);
            current = null;
            previous = null;
            mask = null;
            scratch = null;
            activeMask = null;
            cmd?.Release();
            cmd = null;
        }

        static RenderTexture CreateTexture(string name, OutputProfile output) =>
            CreateTexture(name, output.Resolution, output.WorkingFormat, output.GenerateMips);

        static RenderTexture CreateTexture(string name, Vector2Int resolution, GraphicsFormat format, bool useMipMap)
        {
            var descriptor = new RenderTextureDescriptor(resolution.x, resolution.y)
            {
                depthBufferBits = 0,
                enableRandomWrite = true,
                graphicsFormat = format,
                msaaSamples = 1,
                autoGenerateMips = false,
                useMipMap = useMipMap
            };

            var texture = new RenderTexture(descriptor)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            if (!texture.Create())
            {
                Release(texture);
                throw new InvalidOperationException(
                    $"Failed to create {name} render texture ({resolution.x}x{resolution.y}, {format}).");
            }

            return texture;
        }

        internal static void Release(RenderTexture texture)
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
