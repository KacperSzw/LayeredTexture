using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Editor bake pipeline for writing LayeredTextureArray output as a Texture2DArray asset.
    /// </summary>
    public static class LayeredTextureArrayBaker
    {
        /// <summary>
        /// Evaluates every page recipe, writes the configured Texture2DArray asset, and saves the AssetDatabase.
        /// </summary>
        /// <param name="array">Array recipe to bake.</param>
        /// <param name="error">Failure reason when baking fails.</param>
        /// <returns>True when the bake succeeds.</returns>
        public static bool Bake(LayeredTextureArray array, out string error)
        {
            error = null;

            if (array == null)
            {
                error = "LayeredTextureArray is missing.";
                return false;
            }

            if (!ValidateOutput(array.Output, out error)
                || !TryGetOutputPath(array, out var assetPath, out _, out error)
                || !ValidatePages(array, out error))
                return false;

            var textures = new Texture2D[array.Pages.Count];
            var output = array.Output.ToRecipeOutput();
            Texture2DArray textureArray = null;

            try
            {
                for (var i = 0; i < array.Pages.Count; i++)
                {
                    var renderTexture = TextureRecipeEvaluator.Evaluate(
                        array.Pages[i],
                        output,
                        TextureRecipeEditorSourceResolver.Instance);

                    if (renderTexture == null)
                    {
                        error = $"LayeredTextureArray.Pages[{i}] evaluation failed.";
                        return false;
                    }

                    try
                    {
                        textures[i] = LayeredTextureBakeUtility.ReadBack(
                            renderTexture,
                            TextureFormat.RGBA32,
                            array.Output.GenerateMips);
                    }
                    finally
                    {
                        LayeredTextureBakeUtility.Release(renderTexture);
                    }
                }

                textureArray = CreateTextureArray(array, textures);

                if (!SaveTextureArray(textureArray, assetPath, out error))
                    return false;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                textureArray = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                for (var i = 0; i < textures.Length; i++)
                {
                    if (textures[i] != null)
                        UnityEngine.Object.DestroyImmediate(textures[i]);
                }

                if (textureArray != null)
                    UnityEngine.Object.DestroyImmediate(textureArray);
            }
        }

        static bool ValidateOutput(TextureArrayOutputProfile output, out string error)
        {
            error = null;

            if (output.Resolution.x <= 0 || output.Resolution.y <= 0)
            {
                error = "LayeredTextureArray.Output.Resolution must be positive.";
                return false;
            }

            if (output.WorkingFormat == GraphicsFormat.None)
            {
                error = "LayeredTextureArray.Output.WorkingFormat is invalid.";
                return false;
            }

            if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.Render))
            {
                error = $"LayeredTextureArray.Output.WorkingFormat is not renderable: {output.WorkingFormat}.";
                return false;
            }

            if (!SystemInfo.IsFormatSupported(output.WorkingFormat, GraphicsFormatUsage.LoadStore))
            {
                error = $"LayeredTextureArray.Output.WorkingFormat does not support compute writes: {output.WorkingFormat}.";
                return false;
            }

            if (!TryGetTextureFormat(output.OutputFormat, out var textureFormat))
            {
                error = $"LayeredTextureArray.Output.OutputFormat is unsupported: {output.OutputFormat}.";
                return false;
            }

            if (!SystemInfo.SupportsTextureFormat(textureFormat))
            {
                error = $"LayeredTextureArray.Output.OutputFormat is not supported on this platform: {output.OutputFormat}.";
                return false;
            }

            if (!IsCompressed(output.OutputFormat))
                return true;

            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
            {
                error = "LayeredTextureArray compressed output requires Graphics.CopyTexture support.";
                return false;
            }

            if (output.Resolution.x % 4 != 0 || output.Resolution.y % 4 != 0)
            {
                error = $"LayeredTextureArray.Output.Resolution must be a multiple of 4 for {output.OutputFormat}.";
                return false;
            }

            return true;
        }

        static bool ValidatePages(LayeredTextureArray array, out string error)
        {
            error = null;

            if (array.Pages == null || array.Pages.Count == 0)
            {
                error = "LayeredTextureArray.Pages is empty.";
                return false;
            }

            for (var i = 0; i < array.Pages.Count; i++)
            {
                if (array.Pages[i] != null)
                    continue;

                error = $"LayeredTextureArray.Pages[{i}] is missing.";
                return false;
            }

            return true;
        }

        internal static bool TryGetOutputPath(
            LayeredTextureArray array,
            out string assetPath,
            out string fullPath,
            out string error)
        {
            assetPath = null;
            fullPath = null;
            error = null;

            var arrayPath = AssetDatabase.GetAssetPath(array);

            if (string.IsNullOrEmpty(arrayPath))
            {
                error = "LayeredTextureArray asset must be saved before baking.";
                return false;
            }

            arrayPath = arrayPath.Replace('\\', '/');
            var directory = Path.GetDirectoryName(arrayPath)?.Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(arrayPath);
            assetPath = string.IsNullOrEmpty(directory)
                ? $"{fileName}_Texture2DArray.asset"
                : $"{directory}/{fileName}_Texture2DArray.asset";
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            return true;
        }

        static Texture2DArray CreateTextureArray(LayeredTextureArray array, Texture2D[] textures)
        {
            var output = array.Output;

            return IsCompressed(output.OutputFormat)
                ? CreateCompressedTextureArray(array, textures)
                : CreateUncompressedTextureArray(array, textures);
        }

        static Texture2DArray CreateUncompressedTextureArray(LayeredTextureArray array, Texture2D[] textures)
        {
            var output = array.Output;
            var textureArray = new Texture2DArray(
                output.Resolution.x,
                output.Resolution.y,
                textures.Length,
                TextureFormat.RGBA32,
                output.GenerateMips,
                !output.SRGB)
            {
                name = array.name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (var i = 0; i < textures.Length; i++)
                textureArray.SetPixels(textures[i].GetPixels(), i);

            textureArray.Apply(output.GenerateMips, false);
            return textureArray;
        }

        static Texture2DArray CreateCompressedTextureArray(LayeredTextureArray array, Texture2D[] textures)
        {
            var output = array.Output;
            TryGetTextureFormat(output.OutputFormat, out var textureFormat);
            var textureArray = new Texture2DArray(
                output.Resolution.x,
                output.Resolution.y,
                textures.Length,
                textureFormat,
                output.GenerateMips,
                !output.SRGB)
            {
                name = array.name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (var i = 0; i < textures.Length; i++)
            {
                EditorUtility.CompressTexture(
                    textures[i],
                    textureFormat,
                    CompressionQualityFor(output.CompressionQuality));
                CopyTextureMipChain(textures[i], textureArray, i);
            }

            return textureArray;
        }

        static void CopyTextureMipChain(Texture source, Texture2DArray target, int slice)
        {
            var count = Mathf.Min(source.mipmapCount, target.mipmapCount);

            for (var mip = 0; mip < count; mip++)
                Graphics.CopyTexture(source, 0, mip, target, slice, mip);
        }

        static bool SaveTextureArray(Texture2DArray textureArray, string assetPath, out string error)
        {
            error = null;
            var existingAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (existingAsset == null)
            {
                AssetDatabase.CreateAsset(textureArray, assetPath);
                return true;
            }

            if (existingAsset is not Texture2DArray existingArray)
            {
                error = "LayeredTextureArray output path already contains a non-Texture2DArray asset.";
                return false;
            }

            EditorUtility.CopySerialized(textureArray, existingArray);
            EditorUtility.SetDirty(existingArray);
            return true;
        }

        static bool TryGetTextureFormat(TextureArrayOutputFormat outputFormat, out TextureFormat textureFormat)
        {
            textureFormat = outputFormat switch
            {
                TextureArrayOutputFormat.RGBA32 => TextureFormat.RGBA32,
                TextureArrayOutputFormat.BC7 => TextureFormat.BC7,
                TextureArrayOutputFormat.BC3 => TextureFormat.DXT5,
                TextureArrayOutputFormat.BC1 => TextureFormat.DXT1,
                _ => default
            };

            return outputFormat is TextureArrayOutputFormat.RGBA32
                or TextureArrayOutputFormat.BC7
                or TextureArrayOutputFormat.BC3
                or TextureArrayOutputFormat.BC1;
        }

        static bool IsCompressed(TextureArrayOutputFormat outputFormat) =>
            outputFormat != TextureArrayOutputFormat.RGBA32;

        static TextureCompressionQuality CompressionQualityFor(TextureArrayCompressionQuality quality) =>
            quality switch
            {
                TextureArrayCompressionQuality.Fast => TextureCompressionQuality.Fast,
                TextureArrayCompressionQuality.Best => TextureCompressionQuality.Best,
                _ => TextureCompressionQuality.Normal
            };
    }
}
