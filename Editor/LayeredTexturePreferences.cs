using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// User-local editor preferences for LayeredTexture.
    /// </summary>
    public static class LayeredTexturePreferences
    {
        const string RelativeRootKey = "Unmanaged.LayeredTexture.RelativeRoot";

        /// <summary>
        /// Absolute root folder used by AssetPathMode.Relative file sources.
        /// </summary>
        public static string RelativeRoot => EditorPrefs.GetString(RelativeRootKey, null);

        /// <summary>
        /// Stores the absolute relative-root folder.
        /// </summary>
        /// <param name="path">Absolute folder path, or null/empty to clear the preference.</param>
        public static void SetRelativeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorPrefs.DeleteKey(RelativeRootKey);
                return;
            }

            EditorPrefs.SetString(RelativeRootKey, Path.GetFullPath(path).Replace('\\', '/'));
        }

        /// <summary>
        /// Gets the configured root when it points to an existing directory.
        /// </summary>
        /// <param name="absolutePath">Configured absolute root when valid.</param>
        /// <returns>True when a valid root is configured.</returns>
        public static bool TryGetRelativeRoot(out string absolutePath)
        {
            absolutePath = RelativeRoot;
            return !string.IsNullOrWhiteSpace(absolutePath) && Directory.Exists(absolutePath);
        }

        /// <summary>
        /// Registers the Unity Preferences provider.
        /// </summary>
        /// <returns>LayeredTexture preferences provider.</returns>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => new("Preferences/Layered Texture", SettingsScope.User)
        {
            label = "Layered Texture",
            guiHandler = _ => DrawSettings()
        };

        static void DrawSettings()
        {
            var root = RelativeRoot;

            EditorGUILayout.LabelField("Relative Root", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                root = EditorGUILayout.TextField(root ?? string.Empty);

                if (EditorGUI.EndChangeCheck())
                    SetRelativeRoot(root);

                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                    Browse();
            }

            if (!TryGetRelativeRoot(out _))
                EditorGUILayout.HelpBox("Relative TextureFile paths will not resolve until this points to an existing folder.", MessageType.Warning);
        }

        static void Browse()
        {
            var selectedPath = EditorUtility.OpenFolderPanel(
                "Layered Texture Relative Root",
                string.IsNullOrWhiteSpace(RelativeRoot) ? Application.dataPath : RelativeRoot,
                string.Empty);

            if (!string.IsNullOrEmpty(selectedPath))
                SetRelativeRoot(selectedPath);
        }
    }
}
