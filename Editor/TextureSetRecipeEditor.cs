using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    [CustomEditor(typeof(TextureSetRecipe))]
    sealed class TextureSetRecipeEditor : UnityEditor.Editor
    {
        SerializedProperty recipes;
        SerializedProperty pbrSetup;
        ReorderableList recipeList;
        TextureLayerStackEditor stackEditor;
        RenderTexture previewTexture;
        int selectedIndex;
        bool previewDirty = true;
        TexturePreviewDisplayMode previewDisplayMode;
        int previewSize;
        string previewError;
        string bakeStatus;
        MessageType bakeStatusType;
        string setupStatus;
        MessageType setupStatusType;
        bool pbrFoldout = true;

        const string PreviewDisplayModeKey = "Unmanaged.LayeredTexture.PreviewDisplayMode";
        const string PreviewSizeKey = "Unmanaged.LayeredTexture.PreviewSize";
        const int MinPreviewSize = 80;
        const int MaxPreviewSize = 512;

        void OnEnable()
        {
            recipes = serializedObject.FindProperty("Recipes");
            pbrSetup = serializedObject.FindProperty("PbrSetup");
            previewDisplayMode = LoadPreviewDisplayMode();
            previewSize = EditorPrefs.GetInt(PreviewSizeKey, 220);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, recipes.arraySize - 1));
            recipeList = new ReorderableList(serializedObject, recipes, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Recipes"),
                drawElementCallback = DrawRecipeElement,
                onSelectCallback = SelectRecipe,
                onAddCallback = AddRecipe,
                onRemoveCallback = RemoveRecipe,
                onReorderCallback = list =>
                {
                    selectedIndex = list.index;
                    RebuildStackEditor();
                }
            };
        }

        void OnDisable()
        {
            stackEditor?.Dispose();
            stackEditor = null;
            ReleasePreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            recipeList.DoLayoutList();
            DrawSelectedRecipe();
            DrawPbrSetup();
            var changed = serializedObject.ApplyModifiedProperties();

            if (changed)
                MarkPreviewDirty();

            EditorGUILayout.Space(6f);
            DrawBakeControls();
        }

        void DrawRecipeElement(Rect rect, int index, bool active, bool focused)
        {
            var slot = recipes.GetArrayElementAtIndex(index);
            var enabled = slot.FindPropertyRelative("Enabled");
            var name = slot.FindPropertyRelative("Name");
            var enabledRect = new Rect(rect.x, rect.y + 1f, 18f, EditorGUIUtility.singleLineHeight);
            var nameRect = new Rect(enabledRect.xMax + 4f, rect.y, rect.width - enabledRect.width - 4f, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(enabledRect, enabled, GUIContent.none);
            EditorGUI.PropertyField(nameRect, name, GUIContent.none);
        }

        void SelectRecipe(ReorderableList list)
        {
            selectedIndex = list.index;
            RebuildStackEditor();
        }

        void AddRecipe(ReorderableList _)
        {
            serializedObject.ApplyModifiedProperties();
            var set = (TextureSetRecipe)target;
            Undo.RecordObject(set, "Add Texture Set Recipe");
            set.Recipes ??= new System.Collections.Generic.List<TextureSetRecipeSlot>();
            set.Recipes.Add(new TextureSetRecipeSlot
            {
                Name = UniqueRecipeName(set)
            });
            selectedIndex = set.Recipes.Count - 1;
            recipeList.index = selectedIndex;
            EditorUtility.SetDirty(set);
            serializedObject.Update();
            RebuildStackEditor();
        }

        void RemoveRecipe(ReorderableList _)
        {
            if (selectedIndex < 0 || selectedIndex >= recipes.arraySize)
                return;

            serializedObject.Update();
            Undo.RegisterCompleteObjectUndo(target, "Remove Texture Set Recipe");
            recipes.DeleteArrayElementAtIndex(selectedIndex);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, recipes.arraySize - 1));
            recipeList.index = selectedIndex;
            serializedObject.ApplyModifiedProperties();
            RebuildStackEditor();
        }

        void DrawSelectedRecipe()
        {
            if (recipes.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add a recipe slot or use PBR Auto Fill.", MessageType.Info);
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, recipes.arraySize - 1);
            recipeList.index = selectedIndex;
            var slot = recipes.GetArrayElementAtIndex(selectedIndex);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Selected Recipe", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
                DrawOutput(slot.FindPropertyRelative("Output"));

            DrawPreview();
            EnsureStackEditor();
            stackEditor?.DrawLayout();
        }

        static void DrawOutput(SerializedProperty output)
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(output.FindPropertyRelative("Resolution"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("WorkingFormat"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("OutputGraphicsFormat"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("ExportFormat"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("TextureType"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("GenerateMips"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("SRGB"));
            }
        }

        void DrawPreview()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Preview", EditorStyles.boldLabel, GUILayout.Width(58f));
                GUILayout.FlexibleSpace();
                DrawPreviewControls();

                if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                    MarkPreviewDirty();
            }

            if (previewDirty)
                RebuildPreview();

            if (previewTexture == null)
            {
                EditorGUILayout.HelpBox(previewError ?? "Preview unavailable.", MessageType.Info);
                return;
            }

            var height = Mathf.Clamp(previewSize, MinPreviewSize, MaxPreviewSize);
            var rect = EditorGUILayout.GetControlRect(false, height);
            TexturePreviewGUI.Draw(rect, previewTexture, previewDisplayMode);
        }

        void DrawPreviewControls()
        {
            DrawPreviewModeButtons();

            EditorGUI.BeginChangeCheck();
            previewSize = EditorGUILayout.IntSlider(
                previewSize,
                MinPreviewSize,
                MaxPreviewSize,
                GUILayout.Width(180f));

            if (!EditorGUI.EndChangeCheck())
                return;

            EditorPrefs.SetInt(PreviewSizeKey, previewSize);
            Repaint();
        }

        void DrawPreviewModeButtons()
        {
            const float WideButtonWidth = 48f;
            const float ButtonWidth = 28f;

            DrawPreviewModeButton(TexturePreviewDisplayMode.RGB, "RGB", WideButtonWidth, EditorStyles.miniButtonLeft);
            DrawPreviewModeButton(
                TexturePreviewDisplayMode.RGBAlpha,
                "RGB+A",
                WideButtonWidth,
                EditorStyles.miniButtonMid);
            DrawPreviewModeButton(
                TexturePreviewDisplayMode.RGBAChannels,
                "RGBA",
                WideButtonWidth,
                EditorStyles.miniButtonMid);
            DrawPreviewModeButton(TexturePreviewDisplayMode.R, "R", ButtonWidth, EditorStyles.miniButtonMid);
            DrawPreviewModeButton(TexturePreviewDisplayMode.G, "G", ButtonWidth, EditorStyles.miniButtonMid);
            DrawPreviewModeButton(TexturePreviewDisplayMode.B, "B", ButtonWidth, EditorStyles.miniButtonMid);
            DrawPreviewModeButton(TexturePreviewDisplayMode.A, "A", ButtonWidth, EditorStyles.miniButtonRight);
        }

        void DrawPreviewModeButton(TexturePreviewDisplayMode mode, string label, float width, GUIStyle style)
        {
            var active = previewDisplayMode == mode;
            var previousColor = GUI.backgroundColor;

            if (active)
                GUI.backgroundColor = Color.Lerp(
                    previousColor,
                    new Color(0.34f, 0.56f, 0.78f),
                    EditorGUIUtility.isProSkin ? 0.75f : 0.45f);

            if (GUILayout.Button(label, style, GUILayout.Width(width)))
                SetPreviewMode(mode);

            GUI.backgroundColor = previousColor;
        }

        void SetPreviewMode(TexturePreviewDisplayMode mode)
        {
            previewDisplayMode = mode;
            EditorPrefs.SetInt(PreviewDisplayModeKey, (int)mode);
            Repaint();
        }

        void DrawPbrSetup()
        {
            EditorGUILayout.Space(6f);
            pbrFoldout = EditorGUILayout.Foldout(pbrFoldout, "PBR Auto Fill", true);

            if (!pbrFoldout)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(pbrSetup.FindPropertyRelative("SourceFolder"));
                DrawPresetButtons();
                EditorGUILayout.PropertyField(pbrSetup.FindPropertyRelative("Color"));
                EditorGUILayout.PropertyField(pbrSetup.FindPropertyRelative("PackAlpha"));
                EditorGUILayout.PropertyField(pbrSetup.FindPropertyRelative("ARM"));
                EditorGUILayout.PropertyField(pbrSetup.FindPropertyRelative("Mask"));
                EditorGUILayout.PropertyField(pbrSetup.FindPropertyRelative("Normal"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Pick Source Folder", GUILayout.Height(22f)))
                        PickSourceFolder();

                    if (GUILayout.Button("Setup From Stored Folder", GUILayout.Height(22f)))
                        RunPbrSetup();
                }
            }

            if (!string.IsNullOrEmpty(setupStatus))
                EditorGUILayout.HelpBox(setupStatus, setupStatusType);
        }

        void DrawPresetButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Color+Mask+Normal"))
                    SetPreset(false, false, true);

                if (GUILayout.Button("ColorAlpha+Mask+Normal"))
                    SetPreset(true, false, true);

                if (GUILayout.Button("ColorAlpha+ARM+Normal"))
                    SetPreset(true, true, false);
            }
        }

        void SetPreset(bool packAlpha, bool arm, bool mask)
        {
            pbrSetup.FindPropertyRelative("Color").boolValue = true;
            pbrSetup.FindPropertyRelative("PackAlpha").boolValue = packAlpha;
            pbrSetup.FindPropertyRelative("ARM").boolValue = arm;
            pbrSetup.FindPropertyRelative("Mask").boolValue = mask;
            pbrSetup.FindPropertyRelative("Normal").boolValue = true;
        }

        void PickSourceFolder()
        {
            serializedObject.ApplyModifiedProperties();

            if (!LayeredTexturePreferences.TryGetRelativeRoot(out var root))
            {
                setupStatus = "Layered Texture relative root is missing.";
                setupStatusType = MessageType.Error;
                return;
            }

            var selected = EditorUtility.OpenFolderPanel("PBR Source Folder", root, string.Empty);

            if (string.IsNullOrEmpty(selected))
                return;

            if (!AssetPath.TryMake(selected, root, AssetPathMode.Relative, out var sourceFolder))
            {
                setupStatus = "PBR source folder must be under the Layered Texture relative root.";
                setupStatusType = MessageType.Error;
                return;
            }

            var set = (TextureSetRecipe)target;
            Undo.RecordObject(set, "Set PBR Source Folder");
            set.PbrSetup.SourceFolder = sourceFolder;
            EditorUtility.SetDirty(set);
            serializedObject.Update();
            RunPbrSetup();
        }

        void RunPbrSetup()
        {
            serializedObject.ApplyModifiedProperties();
            var set = (TextureSetRecipe)target;
            Undo.RegisterCompleteObjectUndo(set, "PBR Auto Fill Texture Set");

            if (TextureSetPbrSetupUtility.Setup(set, out setupStatus))
            {
                setupStatusType = MessageType.Info;
                EditorUtility.SetDirty(set);
                serializedObject.Update();
                selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, recipes.arraySize - 1));
                RebuildStackEditor();
                MarkPreviewDirty();
                return;
            }

            setupStatusType = MessageType.Error;
        }

        void DrawBakeControls()
        {
            if (GUILayout.Button("Bake All", GUILayout.Height(24f)))
                BakeAll();

            if (!string.IsNullOrEmpty(bakeStatus))
                EditorGUILayout.HelpBox(bakeStatus, bakeStatusType);
        }

        void BakeAll()
        {
            serializedObject.ApplyModifiedProperties();

            if (TextureSetRecipeBaker.Bake((TextureSetRecipe)target, out var error))
            {
                bakeStatus = "Baked texture set.";
                bakeStatusType = MessageType.Info;
                return;
            }

            bakeStatus = error;
            bakeStatusType = MessageType.Error;
        }

        void EnsureStackEditor()
        {
            if (stackEditor != null)
                return;

            if (selectedIndex < 0 || selectedIndex >= recipes.arraySize)
                return;

            var slot = recipes.GetArrayElementAtIndex(selectedIndex);
            var stack = slot.FindPropertyRelative("RootStack");
            stackEditor = new TextureLayerStackEditor(
                serializedObject,
                stack,
                new TextureLayerStackEditorContext
                {
                    UndoTarget = target,
                    GetOutput = SelectedOutput,
                    SourceResolver = TextureRecipeEditorSourceResolver.Instance,
                    HeaderLabel = "Layers",
                    Changed = MarkPreviewDirty
                },
                Repaint);
        }

        void RebuildStackEditor()
        {
            stackEditor?.Dispose();
            stackEditor = null;
            ReleasePreview();
            previewDirty = true;
            Repaint();
        }

        OutputProfile SelectedOutput()
        {
            var set = (TextureSetRecipe)target;
            return selectedIndex >= 0 && selectedIndex < set.Recipes.Count
                ? set.Recipes[selectedIndex].Output
                : OutputProfile.Default;
        }

        void RebuildPreview()
        {
            previewDirty = false;
            previewError = null;
            ReleasePreview();

            var set = (TextureSetRecipe)target;

            if (selectedIndex < 0 || selectedIndex >= set.Recipes.Count)
                return;

            var slot = set.Recipes[selectedIndex];

            try
            {
                previewTexture = TextureRecipeEvaluator.Evaluate(
                    slot.RootStack,
                    slot.Output,
                    TextureRecipeEditorSourceResolver.Instance);

                if (previewTexture == null)
                    previewError = "Preview unavailable. Check recipe settings.";
            }
            catch (System.Exception exception)
            {
                previewError = exception.Message;
            }
        }

        void MarkPreviewDirty()
        {
            previewDirty = true;
            Repaint();
        }

        void ReleasePreview()
        {
            LayeredTextureBakeUtility.Release(previewTexture);
            previewTexture = null;
        }

        static string UniqueRecipeName(TextureSetRecipe set)
        {
            const string BaseName = "Recipe";
            var index = set.Recipes.Count + 1;

            while (ContainsName(set, $"{BaseName}{index}"))
                index++;

            return $"{BaseName}{index}";
        }

        static bool ContainsName(TextureSetRecipe set, string name)
        {
            for (var i = 0; i < set.Recipes.Count; i++)
            {
                if (string.Equals(set.Recipes[i]?.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static TexturePreviewDisplayMode LoadPreviewDisplayMode()
        {
            var value = EditorPrefs.GetInt(PreviewDisplayModeKey, (int)TexturePreviewDisplayMode.RGBAlpha);

            return System.Enum.IsDefined(typeof(TexturePreviewDisplayMode), value)
                ? (TexturePreviewDisplayMode)value
                : TexturePreviewDisplayMode.RGBAlpha;
        }
    }
}
