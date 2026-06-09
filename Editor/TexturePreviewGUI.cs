using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Draws RGB, alpha, and individual-channel texture previews in editor UI.
    /// </summary>
    static class TexturePreviewGUI
    {
        const float Padding = 5f;
        const float CompactPadding = 2f;
        const float Gap = 4f;

        static Material previewMaterial;

        static TexturePreviewGUI() => AssemblyReloadEvents.beforeAssemblyReload += ReleaseMaterial;

        internal static void Draw(Rect rect, Texture texture) => DrawRgbAlpha(rect, texture, true);

        internal static void Draw(Rect rect, Texture texture, TexturePreviewDisplayMode mode)
        {
            switch (mode)
            {
                case TexturePreviewDisplayMode.RGB:
                    DrawChannels(rect, texture, true, 0);
                    break;
                case TexturePreviewDisplayMode.RGBAChannels:
                    DrawChannels(rect, texture, true, 1, 2, 3, 4);
                    break;
                case TexturePreviewDisplayMode.R:
                    DrawChannels(rect, texture, true, 1);
                    break;
                case TexturePreviewDisplayMode.G:
                    DrawChannels(rect, texture, true, 2);
                    break;
                case TexturePreviewDisplayMode.B:
                    DrawChannels(rect, texture, true, 3);
                    break;
                case TexturePreviewDisplayMode.A:
                    DrawChannels(rect, texture, true, 4);
                    break;
                default:
                    DrawRgbAlpha(rect, texture, true);
                    break;
            }
        }

        internal static void DrawCompact(Rect rect, Texture texture) => DrawRgbAlpha(rect, texture, false);

        static void DrawRgbAlpha(Rect rect, Texture texture, bool frame) =>
            DrawChannels(rect, texture, frame, 0, 4);

        static void DrawChannels(Rect rect, Texture texture, bool frame, params int[] channels)
        {
            if (frame)
                GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            if (texture == null)
            {
                EditorGUI.LabelField(rect, "No Preview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var padding = frame ? Padding : CompactPadding;
            var innerRect = Inset(rect, padding, padding);
            var count = Mathf.Max(1, channels.Length);
            var size = Mathf.Min(
                (innerRect.width - Gap * (count - 1)) / count,
                innerRect.height);
            size = Mathf.Max(1f, size);
            var totalWidth = size * count + Gap * (count - 1);
            var x = innerRect.x + (innerRect.width - totalWidth) * 0.5f;
            var y = innerRect.y + (innerRect.height - size) * 0.5f;

            for (var i = 0; i < count; i++)
                DrawTexture(new Rect(x + (size + Gap) * i, y, size, size), texture, channels[i]);
        }

        static void DrawTexture(Rect rect, Texture texture, int channel)
        {
            var material = GetMaterial();

            if (material != null)
                material.SetInt("_Channel", channel);

            EditorGUI.DrawPreviewTexture(rect, texture, material, ScaleMode.ScaleToFit);
        }

        static Material GetMaterial()
        {
            if (previewMaterial != null)
                return previewMaterial;

            var shader = Shader.Find("Hidden/LayeredTexture/PreviewChannel");

            if (shader == null)
                return null;

            previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return previewMaterial;
        }

        static void ReleaseMaterial()
        {
            if (previewMaterial == null)
                return;

            Object.DestroyImmediate(previewMaterial);
            previewMaterial = null;
        }

        static Rect Inset(Rect rect, float x, float y) =>
            new(rect.x + x, rect.y + y, rect.width - x * 2f, rect.height - y * 2f);
    }
}
