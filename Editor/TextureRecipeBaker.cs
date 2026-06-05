using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Editor bake pipeline for writing TextureRecipe output as a Unity texture asset.
    /// </summary>
    public static class TextureRecipeBaker
    {
        /// <summary>
        /// Evaluates a recipe, writes the configured output file, refreshes AssetDatabase, and applies importer settings.
        /// </summary>
        /// <param name="recipe">Recipe to bake.</param>
        /// <param name="error">Failure reason when baking fails.</param>
        /// <returns>True when the bake succeeds.</returns>
        public static bool Bake(TextureRecipe recipe, out string error)
        {
            error = null;

            if (recipe == null)
            {
                error = "TextureRecipe is missing.";
                return false;
            }

            if (!TryGetOutputPath(recipe.Output, out var assetPath, out var fullPath, out error))
                return false;

            RenderTexture renderTexture = null;
            Texture2D texture = null;

            try
            {
                renderTexture = TextureRecipeEvaluator.Evaluate(recipe, TextureRecipeEditorSourceResolver.Instance);

                if (renderTexture == null)
                {
                    error = "TextureRecipe evaluation failed.";
                    return false;
                }

                texture = ReadBack(renderTexture, recipe.Output.ExportFormat);
                var bytes = Encode(texture, recipe.Output.ExportFormat);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, bytes);
                AssetDatabase.Refresh();
                ApplyImporter(assetPath, recipe.Output);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                Release(renderTexture);

                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Gets the required file extension for an export format.
        /// </summary>
        /// <param name="format">Export format.</param>
        /// <returns>Extension including the leading dot, or null for unsupported values.</returns>
        public static string ExtensionFor(ExportFileFormat format) =>
            format switch
            {
                ExportFileFormat.PNG => ".png",
                ExportFileFormat.TGA => ".tga",
                ExportFileFormat.EXR => ".exr",
                _ => null
            };

        static bool TryGetOutputPath(OutputProfile output, out string assetPath, out string fullPath, out string error)
        {
            assetPath = null;
            fullPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(output.OutputPath))
            {
                error = "TextureRecipe.Output.OutputPath is missing.";
                return false;
            }

            assetPath = output.OutputPath.Replace('\\', '/');

            if (Path.IsPathRooted(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "TextureRecipe.Output.OutputPath must be under Assets/.";
                return false;
            }

            var expectedExtension = ExtensionFor(output.ExportFormat);

            if (expectedExtension == null)
            {
                error = $"TextureRecipe.Output.ExportFormat is unsupported: {output.ExportFormat}.";
                return false;
            }

            var actualExtension = Path.GetExtension(assetPath);

            if (!string.Equals(actualExtension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = $"TextureRecipe.Output.OutputPath extension must be {expectedExtension} for {output.ExportFormat}.";
                return false;
            }

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));

            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = "TextureRecipe.Output.OutputPath must be under Assets/.";
                return false;
            }

            return true;
        }

        static Texture2D ReadBack(RenderTexture renderTexture, ExportFileFormat format)
        {
            var textureFormat = format == ExportFileFormat.EXR
                ? TextureFormat.RGBAFloat
                : TextureFormat.RGBA32;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, textureFormat, false, true);
            var active = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = active;
            }

            return texture;
        }

        static byte[] Encode(Texture2D texture, ExportFileFormat format) =>
            format switch
            {
                ExportFileFormat.PNG => ImageConversion.EncodeToPNG(texture),
                ExportFileFormat.TGA => ImageConversion.EncodeToTGA(texture),
                ExportFileFormat.EXR => ImageConversion.EncodeToEXR(texture, Texture2D.EXRFlags.OutputAsFloat),
                _ => null
            };

        static void ApplyImporter(string assetPath, OutputProfile output)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.sRGBTexture = output.SRGB;
            importer.mipmapEnabled = output.GenerateMips;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
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
