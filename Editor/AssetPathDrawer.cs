using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    [CustomPropertyDrawer(typeof(AssetPath))]
    sealed class AssetPathDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            AssetPathEditorGUI.Draw(position, property, PickerKind(), label);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        AssetPathPickerKind PickerKind()
        {
            var attribute = fieldInfo
                ?.GetCustomAttributes(typeof(AssetPathPickerAttribute), true)
                .OfType<AssetPathPickerAttribute>()
                .FirstOrDefault();

            return attribute?.Kind ?? AssetPathPickerKind.File;
        }
    }
}
