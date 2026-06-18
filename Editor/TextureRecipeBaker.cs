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

            if (!TryGetOutputPath(recipe, out var assetPath, out var fullPath, out error))
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

                texture = LayeredTextureBakeUtility.ReadBack(renderTexture, TextureFormatFor(recipe.Output.ExportFormat));
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
                LayeredTextureBakeUtility.Release(renderTexture);

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

        internal static bool TryGetOutputPath(
            TextureRecipe recipe,
            out string assetPath,
            out string fullPath,
            out string error)
        {
            assetPath = null;
            fullPath = null;
            error = null;

            var recipePath = AssetDatabase.GetAssetPath(recipe);

            if (string.IsNullOrEmpty(recipePath))
            {
                error = "TextureRecipe asset must be saved before baking.";
                return false;
            }

            recipePath = recipePath.Replace('\\', '/');

            var extension = ExtensionFor(recipe.Output.ExportFormat);

            if (extension == null)
            {
                error = $"TextureRecipe.Output.ExportFormat is unsupported: {recipe.Output.ExportFormat}.";
                return false;
            }

            assetPath = Path.ChangeExtension(recipePath, extension).Replace('\\', '/');
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            return true;
        }

        internal static TextureFormat TextureFormatFor(ExportFileFormat format) =>
            format == ExportFileFormat.EXR ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;

        internal static byte[] Encode(Texture2D texture, ExportFileFormat format) =>
            format switch
            {
                ExportFileFormat.PNG => ImageConversion.EncodeToPNG(texture),
                ExportFileFormat.TGA => ImageConversion.EncodeToTGA(texture),
                ExportFileFormat.EXR => ImageConversion.EncodeToEXR(texture, Texture2D.EXRFlags.OutputAsFloat),
                _ => null
            };

        internal static void ApplyImporter(string assetPath, OutputProfile output)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterTypeFor(output.TextureType);
            importer.sRGBTexture = output.SRGB;
            importer.mipmapEnabled = output.GenerateMips;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        static TextureImporterType TextureImporterTypeFor(OutputTextureType textureType) =>
            textureType switch
            {
                OutputTextureType.NormalMap => TextureImporterType.NormalMap,
                OutputTextureType.SingleChannel => TextureImporterType.SingleChannel,
                OutputTextureType.Sprite => TextureImporterType.Sprite,
                OutputTextureType.GUI => TextureImporterType.GUI,
                OutputTextureType.Cursor => TextureImporterType.Cursor,
                OutputTextureType.Cookie => TextureImporterType.Cookie,
                OutputTextureType.Lightmap => TextureImporterType.Lightmap,
                OutputTextureType.Shadowmask => TextureImporterType.Shadowmask,
                _ => TextureImporterType.Default
            };
    }
}
