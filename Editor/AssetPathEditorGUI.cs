using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    static class AssetPathEditorGUI
    {
        const float ModeWidth = 82f;
        const float ButtonWidth = 48f;
        const float Gap = 4f;

        internal static void Draw(
            Rect rect,
            SerializedProperty property,
            AssetPathPickerKind kind,
            GUIContent label = null,
            Action changed = null)
        {
            if (label != null && label != GUIContent.none)
                rect = EditorGUI.PrefixLabel(rect, label);

            var mode = property.FindPropertyRelative("Mode");
            var path = property.FindPropertyRelative("Path");
            var modeRect = new Rect(rect.x, rect.y, ModeWidth, rect.height);
            var buttonRect = new Rect(rect.xMax - ButtonWidth, rect.y, ButtonWidth, rect.height);
            var pathRect = new Rect(
                modeRect.xMax + Gap,
                rect.y,
                Mathf.Max(1f, buttonRect.x - modeRect.xMax - Gap * 2f),
                rect.height);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(modeRect, mode, GUIContent.none);
            DrawPathText(pathRect, path, IsValid(Read(property), kind));

            if (EditorGUI.EndChangeCheck())
                changed?.Invoke();

            if (GUI.Button(buttonRect, "Pick"))
                Pick(property, kind, changed);
        }

        internal static AssetPath Read(SerializedProperty property) => new()
        {
            Mode = (AssetPathMode)property.FindPropertyRelative("Mode").enumValueIndex,
            Path = property.FindPropertyRelative("Path").stringValue
        };

        internal static void Write(SerializedProperty property, AssetPath assetPath)
        {
            property.FindPropertyRelative("Mode").enumValueIndex = (int)assetPath.Mode;
            property.FindPropertyRelative("Path").stringValue = assetPath.Path;
        }

        static void DrawPathText(Rect rect, SerializedProperty property, bool valid)
        {
            var tint = valid
                ? new Color(0.34f, 0.62f, 0.34f, EditorGUIUtility.isProSkin ? 0.22f : 0.14f)
                : new Color(0.72f, 0.32f, 0.32f, EditorGUIUtility.isProSkin ? 0.22f : 0.14f);

            EditorGUI.DrawRect(rect, tint);

            var value = EditorGUI.TextField(rect, property.stringValue ?? string.Empty);
            property.stringValue = Normalize(value);
        }

        static void Pick(SerializedProperty property, AssetPathPickerKind kind, Action changed)
        {
            var mode = (AssetPathMode)property.FindPropertyRelative("Mode").enumValueIndex;

            if (kind == AssetPathPickerKind.TextureFile && mode == AssetPathMode.Relative)
            {
                var serializedObject = property.serializedObject;
                var propertyPath = property.propertyPath;
                RelativeTexturePickerWindow.Show(relativePath =>
                {
                    serializedObject.Update();
                    var path = serializedObject.FindProperty(propertyPath);
                    Write(path, AssetPath.Relative(relativePath));
                    serializedObject.ApplyModifiedProperties();
                    changed?.Invoke();
                });
                return;
            }

            var current = Read(property);
            var selected = kind == AssetPathPickerKind.Folder
                ? EditorUtility.OpenFolderPanel("Select Folder", StartDirectory(current, kind), string.Empty)
                : EditorUtility.OpenFilePanel("Select File", StartDirectory(current, kind), Extensions(kind));

            if (string.IsNullOrEmpty(selected))
                return;

            if (!TryMake(selected, mode, out var assetPath))
                return;

            Write(property, assetPath);
            property.serializedObject.ApplyModifiedProperties();
            changed?.Invoke();
        }

        static bool TryMake(string selected, AssetPathMode mode, out AssetPath assetPath)
        {
            if (mode == AssetPathMode.Absolute)
                return AssetPath.TryMake(selected, null, mode, out assetPath);

            if (!LayeredTexturePreferences.TryGetRelativeRoot(out var root))
            {
                assetPath = default;
                Debug.LogWarning("Layered Texture relative root is missing.");
                return false;
            }

            if (AssetPath.TryMake(selected, root, mode, out assetPath))
                return true;

            Debug.LogWarning("Selected path is outside the Layered Texture relative root.");
            return false;
        }

        static string StartDirectory(AssetPath path, AssetPathPickerKind kind)
        {
            if (TryGetAbsolutePath(path, out var absolutePath))
            {
                if (kind == AssetPathPickerKind.Folder && Directory.Exists(absolutePath))
                    return absolutePath;

                var directory = Path.GetDirectoryName(absolutePath);

                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    return directory;
            }

            return LayeredTexturePreferences.TryGetRelativeRoot(out var root)
                ? root
                : Application.dataPath;
        }

        static bool IsValid(AssetPath path, AssetPathPickerKind kind)
        {
            if (!TryGetAbsolutePath(path, out var absolutePath))
                return false;

            return kind switch
            {
                AssetPathPickerKind.Folder => Directory.Exists(absolutePath),
                AssetPathPickerKind.TextureFile => IsValidTexturePath(path, absolutePath),
                _ => File.Exists(absolutePath)
            };
        }

        static bool IsValidTexturePath(AssetPath path, string absolutePath)
        {
            var relativeRoot = path.Mode == AssetPathMode.Relative
                && LayeredTexturePreferences.TryGetRelativeRoot(out var root)
                    ? root
                    : null;

            if (path.TryGetUnityAssetPath(relativeRoot, out var assetPath)
                && AssetDatabase.LoadAssetAtPath<Texture>(assetPath) != null)
            {
                return true;
            }

            return File.Exists(absolutePath) && TextureFileLoader.IsSupportedPath(absolutePath);
        }

        static bool TryGetAbsolutePath(AssetPath path, out string absolutePath)
        {
            var relativeRoot = path.Mode == AssetPathMode.Relative
                && LayeredTexturePreferences.TryGetRelativeRoot(out var root)
                    ? root
                    : null;

            return path.TryGetAbsolutePath(relativeRoot, out absolutePath);
        }

        static string Extensions(AssetPathPickerKind kind) =>
            kind == AssetPathPickerKind.TextureFile ? "png,jpg,jpeg,tga,asset" : string.Empty;

        static string Normalize(string path) =>
            string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/');
    }
}
