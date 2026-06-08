using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    [CustomEditor(typeof(LayeredTextureArray))]
    sealed class LayeredTextureArrayEditor : UnityEditor.Editor
    {
        SerializedProperty output;
        SerializedProperty pages;
        ReorderableList pageList;
        string bakeStatus;
        MessageType bakeStatusType;

        void OnEnable()
        {
            output = serializedObject.FindProperty("Output");
            pages = serializedObject.FindProperty("Pages");
            pageList = new ReorderableList(serializedObject, pages, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Pages"),
                drawElementCallback = DrawPage
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            pageList.DoLayoutList();
            EditorGUILayout.Space(6f);
            DrawOutput();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            DrawBakeControls();
        }

        void DrawOutput()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(output.FindPropertyRelative("Resolution"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("WorkingFormat"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("GenerateMips"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("SRGB"));
            }
        }

        void DrawPage(Rect rect, int index, bool active, bool focused)
        {
            var page = pages.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, page, GUIContent.none);
        }

        void DrawBakeControls()
        {
            if (GUILayout.Button("Bake", GUILayout.Height(24f)))
                Bake();

            if (!string.IsNullOrEmpty(bakeStatus))
                EditorGUILayout.HelpBox(bakeStatus, bakeStatusType);
        }

        void Bake()
        {
            serializedObject.ApplyModifiedProperties();

            if (LayeredTextureArrayBaker.Bake((LayeredTextureArray)target, out var error))
            {
                bakeStatus = LayeredTextureArrayBaker.TryGetOutputPath(
                    (LayeredTextureArray)target,
                    out var assetPath,
                    out _,
                    out _)
                    ? $"Baked {assetPath}"
                    : "Baked.";
                bakeStatusType = MessageType.Info;
                return;
            }

            bakeStatus = error;
            bakeStatusType = MessageType.Error;
        }
    }
}
