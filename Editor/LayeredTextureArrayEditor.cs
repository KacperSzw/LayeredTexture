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

            DrawOutput();
            EditorGUILayout.Space(6f);
            pageList.DoLayoutList();
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
                DrawOutputPath();
                EditorGUILayout.PropertyField(output.FindPropertyRelative("GenerateMips"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("SRGB"));
            }
        }

        void DrawOutputPath()
        {
            var path = output.FindPropertyRelative("OutputPath");
            var rect = EditorGUILayout.GetControlRect();
            var buttonRect = new Rect(rect.xMax - 62f, rect.y, 62f, rect.height);
            var pathRect = new Rect(rect.x, rect.y, rect.width - 66f, rect.height);

            EditorGUI.PropertyField(pathRect, path);

            if (!GUI.Button(buttonRect, "Browse"))
                return;

            var selectedPath = EditorUtility.SaveFilePanelInProject(
                "Bake LayeredTextureArray",
                target.name,
                "asset",
                "Choose texture array output asset path.");

            if (string.IsNullOrEmpty(selectedPath))
                return;

            path.stringValue = selectedPath.Replace('\\', '/');
            bakeStatus = null;
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
                bakeStatus = $"Baked {output.FindPropertyRelative("OutputPath").stringValue}";
                bakeStatusType = MessageType.Info;
                return;
            }

            bakeStatus = error;
            bakeStatusType = MessageType.Error;
        }
    }
}
