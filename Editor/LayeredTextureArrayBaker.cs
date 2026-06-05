using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

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
                || !TryGetOutputPath(array.Output, out var assetPath, out var fullPath, out error)
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
                        textures[i] = LayeredTextureBakeUtility.ReadBack(renderTexture, TextureFormat.RGBA32);
                    }
                    finally
                    {
                        LayeredTextureBakeUtility.Release(renderTexture);
                    }
                }

                textureArray = CreateTextureArray(array, textures);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

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

        static bool TryGetOutputPath(
            TextureArrayOutputProfile output,
            out string assetPath,
            out string fullPath,
            out string error) =>
            LayeredTextureBakeUtility.TryGetOutputPath(
                output.OutputPath,
                "LayeredTextureArray.Output.OutputPath",
                ".asset",
                null,
                null,
                out assetPath,
                out fullPath,
                out error);

        static Texture2DArray CreateTextureArray(LayeredTextureArray array, Texture2D[] textures)
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
                name = Path.GetFileNameWithoutExtension(output.OutputPath),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (var i = 0; i < textures.Length; i++)
                textureArray.SetPixels(textures[i].GetPixels(), i);

            textureArray.Apply(output.GenerateMips, false);
            return textureArray;
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
                error = "LayeredTextureArray.Output.OutputPath already contains a non-Texture2DArray asset.";
                return false;
            }

            EditorUtility.CopySerialized(textureArray, existingArray);
            EditorUtility.SetDirty(existingArray);
            return true;
        }
    }
}
