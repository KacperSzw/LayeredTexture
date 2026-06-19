using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    public static class TextureSetRecipeBaker
    {
        public static bool Bake(TextureSetRecipe set, out string error)
        {
            error = null;

            if (!Validate(set, out var slots, out error))
                return false;

            RenderTexture renderTexture = null;
            Texture2D texture = null;

            try
            {
                foreach (var slot in slots)
                {
                    renderTexture = TextureRecipeEvaluator.Evaluate(
                        slot.Slot.RootStack,
                        slot.Slot.Output,
                        TextureRecipeEditorSourceResolver.Instance);

                    if (renderTexture == null)
                    {
                        error = $"{slot.Slot.Name} evaluation failed.";
                        return false;
                    }

                    texture = LayeredTextureBakeUtility.ReadBack(
                        renderTexture,
                        TextureRecipeBaker.TextureFormatFor(slot.Slot.Output.ExportFormat));
                    var bytes = TextureRecipeBaker.Encode(texture, slot.Slot.Output);

                    if (bytes == null)
                    {
                        error = $"{slot.Slot.Name} output encoding failed.";
                        return false;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(slot.FullPath));
                    File.WriteAllBytes(slot.FullPath, bytes);
                    AssetDatabase.Refresh();
                    TextureRecipeBaker.ApplyImporter(slot.AssetPath, slot.Slot.Output);

                    LayeredTextureBakeUtility.Release(renderTexture);
                    renderTexture = null;
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }

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

        static bool Validate(TextureSetRecipe set, out List<ResolvedSlot> slots, out string error)
        {
            slots = null;
            error = null;

            if (set == null)
            {
                error = "TextureSetRecipe is missing.";
                return false;
            }

            var setPath = AssetDatabase.GetAssetPath(set);

            if (string.IsNullOrEmpty(setPath))
            {
                error = "TextureSetRecipe asset must be saved before baking.";
                return false;
            }

            if (set.Recipes == null || set.Recipes.Count == 0)
            {
                error = "TextureSetRecipe.Recipes is empty.";
                return false;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            slots = new List<ResolvedSlot>();

            for (var i = 0; i < set.Recipes.Count; i++)
            {
                var slot = set.Recipes[i];

                if (slot == null || !slot.Enabled)
                    continue;

                var sanitizedName = SanitizeName(slot.Name);

                if (string.IsNullOrEmpty(sanitizedName))
                {
                    error = $"TextureSetRecipe.Recipes[{i}].Name is missing.";
                    return false;
                }

                if (!names.Add(sanitizedName))
                {
                    error = $"TextureSetRecipe contains duplicate output name: {sanitizedName}.";
                    return false;
                }

                if (!TextureRecipeValidator.ValidateRuntime(slot.RootStack, slot.Output, out var validationError))
                {
                    error = $"{slot.Name}: {validationError}";
                    return false;
                }

                if (!TryGetOutputPath(setPath, slot, sanitizedName, out var assetPath, out var fullPath, out error))
                    return false;

                slots.Add(new ResolvedSlot
                {
                    Slot = slot,
                    AssetPath = assetPath,
                    FullPath = fullPath
                });
            }

            if (slots.Count > 0)
                return true;

            error = "TextureSetRecipe has no enabled recipes.";
            return false;
        }

        static bool TryGetOutputPath(
            string setPath,
            TextureSetRecipeSlot slot,
            string sanitizedName,
            out string assetPath,
            out string fullPath,
            out string error)
        {
            assetPath = null;
            fullPath = null;
            error = null;
            var extension = TextureRecipeBaker.ExtensionFor(slot.Output.ExportFormat);

            if (extension == null)
            {
                error = $"{slot.Name}.Output.ExportFormat is unsupported: {slot.Output.ExportFormat}.";
                return false;
            }

            setPath = setPath.Replace('\\', '/');
            var directory = Path.GetDirectoryName(setPath)?.Replace('\\', '/');
            var setName = Path.GetFileNameWithoutExtension(setPath);
            assetPath = string.IsNullOrEmpty(directory)
                ? $"{setName}_{sanitizedName}{extension}"
                : $"{directory}/{setName}_{sanitizedName}{extension}";
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            return true;
        }

        static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Trim().ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
                    chars[i] = '_';
            }

            return new string(chars).Trim('_');
        }

        struct ResolvedSlot
        {
            public TextureSetRecipeSlot Slot;
            public string AssetPath;
            public string FullPath;
        }
    }
}
