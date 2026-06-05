using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Project-level LayeredTexture editor preferences stored under ProjectSettings.
    /// </summary>
    [FilePath("ProjectSettings/LayeredTextureSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class LayeredTextureProjectPreferences : ScriptableSingleton<LayeredTextureProjectPreferences>
    {
        [SerializeField]
        RelativePath sourceRoot = RelativePath.FromAssetPath("Assets");

        /// <summary>
        /// Root used by RelativePathMode.ProjectPreferences.
        /// </summary>
        public static RelativePath SourceRoot => instance.sourceRoot;

        /// <summary>
        /// Stores the project preference source root.
        /// </summary>
        /// <param name="path">New source root. ProjectPreferences mode is ignored by the settings UI.</param>
        public static void SetSourceRoot(RelativePath path)
        {
            instance.sourceRoot = path;
            instance.Save(true);
        }

        /// <summary>
        /// Resolves the configured source root to an absolute filesystem path.
        /// </summary>
        /// <param name="absolutePath">Resolved source root when successful.</param>
        /// <returns>True when the configured source root is valid.</returns>
        public static bool TryGetSourceRootAbsolutePath(out string absolutePath) =>
            instance.sourceRoot.TryGetAbsolutePath(out absolutePath);

        /// <summary>
        /// Registers the Project Settings provider.
        /// </summary>
        /// <returns>LayeredTexture project settings provider.</returns>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => new("Project/Layered Texture", SettingsScope.Project)
        {
            label = "Layered Texture",
            guiHandler = _ => DrawSettings()
        };

        static void DrawSettings()
        {
            var sourceRoot = SourceRoot;
            var changed = false;
            var modeIndex = sourceRoot.Mode == RelativePathMode.ProjectRoot ? 1 : 0;

            EditorGUILayout.LabelField("Source Root", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                modeIndex = EditorGUILayout.Popup(modeIndex, new[] { "Project Assets", "Project Root" }, GUILayout.Width(130f));
                sourceRoot.Path = EditorGUILayout.TextField(sourceRoot.Path);
                changed |= EditorGUI.EndChangeCheck();

                var mode = modeIndex == 1 ? RelativePathMode.ProjectRoot : RelativePathMode.ProjectAssets;

                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                    changed |= Browse(ref sourceRoot, mode);

                sourceRoot.Mode = mode;
            }

            if (changed)
                SetSourceRoot(sourceRoot);
        }

        static bool Browse(ref RelativePath sourceRoot, RelativePathMode mode)
        {
            var selectedPath = EditorUtility.OpenFolderPanel(
                "Layered Texture Source Root",
                Application.dataPath,
                string.Empty);

            if (string.IsNullOrEmpty(selectedPath))
                return false;

            if (!RelativePath.TryMake(selectedPath, mode, null, out sourceRoot))
            {
                EditorUtility.DisplayDialog("Invalid Source Root", $"Choose a folder under {mode}.", "OK");
                return false;
            }

            return true;
        }
    }
}
