using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Picker window for assigning texture files relative to the configured LayeredTexture root.
    /// </summary>
    public sealed class RelativeTexturePickerWindow : EditorWindow
    {
        readonly Dictionary<string, Texture2D> previews = new();
        Action<string> onSelect;
        List<string> paths = new();
        Vector2 scroll;
        string search;
        string root;

        internal static void Show(Action<string> onSelect)
        {
            var window = CreateInstance<RelativeTexturePickerWindow>();
            window.titleContent = new GUIContent("Texture File");
            window.minSize = new Vector2(420f, 320f);
            window.onSelect = onSelect;
            window.Refresh();
            window.ShowUtility();
        }

        /// <summary>
        /// Collects supported texture files below a root folder as sorted relative paths.
        /// </summary>
        /// <param name="root">Absolute root folder to search.</param>
        /// <param name="search">Optional case-insensitive path filter.</param>
        /// <returns>Sorted relative paths using forward slashes.</returns>
        public static List<string> CollectRelativePaths(string root, string search)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return result;

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!TextureFileLoader.IsSupportedPath(file))
                    continue;

                var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');

                if (!string.IsNullOrWhiteSpace(search)
                    && relativePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                result.Add(relativePath);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        void OnDisable()
        {
            foreach (var preview in previews.Values)
                DestroyImmediate(preview);

            previews.Clear();
        }

        void OnGUI()
        {
            if (!LayeredTexturePreferences.TryGetRelativeRoot(out root))
            {
                EditorGUILayout.HelpBox("Set a valid relative root in Layered Texture preferences.", MessageType.Warning);

                if (GUILayout.Button("Open Preferences"))
                    SettingsService.OpenUserPreferences("Preferences/Layered Texture");

                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                search = GUILayout.TextField(search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.ExpandWidth(true));

                if (EditorGUI.EndChangeCheck())
                    Refresh();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    Refresh();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var path in paths)
                DrawPath(path);

            EditorGUILayout.EndScrollView();
        }

        void DrawPath(string path)
        {
            var rect = EditorGUILayout.GetControlRect(false, 38f);
            var previewRect = new Rect(rect.x, rect.y + 3f, 32f, 32f);
            var labelRect = new Rect(previewRect.xMax + 6f, rect.y, rect.width - 112f, rect.height);
            var buttonRect = new Rect(rect.xMax - 72f, rect.y + 7f, 72f, 22f);
            var preview = GetPreview(path);

            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            else
                GUI.Box(previewRect, GUIContent.none);

            EditorGUI.LabelField(labelRect, path);

            if (GUI.Button(buttonRect, "Select"))
            {
                onSelect?.Invoke(path);
                Close();
            }
        }

        Texture2D GetPreview(string path)
        {
            if (previews.TryGetValue(path, out var preview))
                return preview;

            if (!TextureFileLoader.TryLoad(Path.Combine(root, path), out preview))
                return null;

            previews[path] = preview;
            return preview;
        }

        void Refresh()
        {
            paths = LayeredTexturePreferences.TryGetRelativeRoot(out root)
                ? CollectRelativePaths(root, search)
                : new List<string>();
        }
    }
}
