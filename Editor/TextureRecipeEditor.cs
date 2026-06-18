using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Shared TextureRecipe editor view used by the inspector and standalone editor window.
    /// </summary>
    sealed class TextureRecipeEditorView : System.IDisposable
    {
        readonly TextureRecipe recipe;
        readonly SerializedObject serializedObject;
        readonly System.Action repaint;
        SerializedProperty output;
        TextureLayerStackEditor layerStackEditor;
        RenderTexture previewTexture;
        bool previewDirty = true;
        TexturePreviewDisplayMode previewDisplayMode;
        Vector2 editScroll;
        int previewSize;
        string previewError;
        string bakeStatus;
        MessageType bakeStatusType;

        const string PreviewDisplayModeKey = "Unmanaged.LayeredTexture.PreviewDisplayMode";
        const string PreviewSizeKey = "Unmanaged.LayeredTexture.PreviewSize";
        const int MinPreviewSize = 80;
        const int MaxPreviewSize = 512;

        internal TextureRecipeEditorView(TextureRecipe recipe, System.Action repaint)
        {
            this.recipe = recipe;
            this.repaint = repaint;
            serializedObject = new SerializedObject(recipe);
            Initialize();
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            layerStackEditor?.Dispose();
            ReleasePreview();
        }

        void Initialize()
        {
            output = serializedObject.FindProperty("Output");
            previewDisplayMode = LoadPreviewDisplayMode();
            previewSize = EditorPrefs.GetInt(PreviewSizeKey, 220);
            layerStackEditor = new TextureLayerStackEditor(
                serializedObject,
                serializedObject.FindProperty("RootStack"),
                new TextureLayerStackEditorContext
                {
                    UndoTarget = recipe,
                    PreviewRecipe = recipe,
                    GetOutput = () => recipe.Output,
                    SourceResolver = TextureRecipeEditorSourceResolver.Instance,
                    Changed = MarkPreviewDirty
                },
                repaint);

            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        internal void DrawInspector()
        {
            serializedObject.Update();

            DrawPreview();
            EditorGUILayout.Space(6f);
            DrawEditControls();
        }

        internal void DrawWindow(float windowHeight)
        {
            serializedObject.Update();

            DrawPreview();
            EditorGUILayout.Space(6f);
            var scrollTop = GUILayoutUtility.GetLastRect().yMax;
            var scrollHeight = Mathf.Max(0f, windowHeight - scrollTop);
            var screenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            var clipRect = new Rect(
                screenOrigin.x,
                screenOrigin.y + scrollTop,
                EditorGUIUtility.currentViewWidth,
                scrollHeight);

            layerStackEditor.SetPreviewClip(clipRect);

            try
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(editScroll, GUILayout.ExpandHeight(true)))
                {
                    editScroll = scroll.scrollPosition;
                    DrawEditControls();
                }
            }
            finally
            {
                layerStackEditor.ClearPreviewClip();
            }
        }

        void DrawEditControls()
        {
            EditorGUI.BeginChangeCheck();
            layerStackEditor.DrawLayout();
            EditorGUILayout.Space(6f);
            DrawOutput();

            var changed = EditorGUI.EndChangeCheck();
            var applied = serializedObject.ApplyModifiedProperties();

            if (changed || applied)
                layerStackEditor.MarkPreviewDirty();

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
                EditorGUILayout.PropertyField(output.FindPropertyRelative("OutputGraphicsFormat"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("ExportFormat"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("TextureType"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("GenerateMips"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("SRGB"));
            }
        }

        void DrawBakeControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake", GUILayout.Height(24f)))
                    Bake();
            }

            if (!string.IsNullOrEmpty(bakeStatus))
                EditorGUILayout.HelpBox(bakeStatus, bakeStatusType);
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
            repaint?.Invoke();
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
            repaint?.Invoke();
        }

        void Bake()
        {
            serializedObject.ApplyModifiedProperties();

            if (TextureRecipeBaker.Bake(recipe, out var error))
            {
                bakeStatus = TextureRecipeBaker.TryGetOutputPath(recipe, out var assetPath, out _, out _)
                    ? $"Baked {assetPath}"
                    : "Baked.";
                bakeStatusType = MessageType.Info;
                return;
            }

            bakeStatus = error;
            bakeStatusType = MessageType.Error;
        }

        void RebuildPreview()
        {
            previewDirty = false;
            previewError = null;
            ReleasePreview();

            try
            {
                previewTexture = TextureRecipeEvaluator.Evaluate(
                    recipe,
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
            repaint?.Invoke();
        }

        void HandleUndoRedo() => MarkPreviewDirty();

        void ReleasePreview()
        {
            LayeredTextureBakeUtility.Release(previewTexture);
            previewTexture = null;
        }

        static TexturePreviewDisplayMode LoadPreviewDisplayMode()
        {
            var value = EditorPrefs.GetInt(PreviewDisplayModeKey, (int)TexturePreviewDisplayMode.RGBAlpha);

            return System.Enum.IsDefined(typeof(TexturePreviewDisplayMode), value)
                ? (TexturePreviewDisplayMode)value
                : TexturePreviewDisplayMode.RGBAlpha;
        }
    }

    /// <summary>
    /// Inspector for TextureRecipe assets.
    /// </summary>
    [CustomEditor(typeof(TextureRecipe))]
    sealed class TextureRecipeEditor : UnityEditor.Editor
    {
        TextureRecipeEditorView view;

        void OnEnable()
        {
            if (target is TextureRecipe recipe)
                view = new TextureRecipeEditorView(recipe, Repaint);
        }

        void OnDisable()
        {
            view?.Dispose();
            view = null;
        }

        public override void OnInspectorGUI()
        {
            if (target is not TextureRecipe recipe)
                return;

            if (GUILayout.Button("Open Texture Recipe Editor", GUILayout.Height(22f)))
                TextureRecipeEditorWindow.Open(recipe);

            EditorGUILayout.Space(4f);
            view?.DrawInspector();
        }
    }

    /// <summary>
    /// Standalone editor window for editing TextureRecipe assets.
    /// </summary>
    sealed class TextureRecipeEditorWindow : EditorWindow
    {
        [SerializeField] TextureRecipe recipe;
        TextureRecipeEditorView view;

        [MenuItem("Window/Layered Texture/Texture Recipe Editor")]
        static void OpenWindow()
        {
            var window = GetWindow<TextureRecipeEditorWindow>("Texture Recipe");

            if (Selection.activeObject is TextureRecipe selectedRecipe)
                window.SetRecipe(selectedRecipe);

            window.Show();
        }

        internal static void Open(TextureRecipe recipe)
        {
            var window = GetWindow<TextureRecipeEditorWindow>("Texture Recipe");
            window.SetRecipe(recipe);
            window.Show();
        }

        void OnEnable()
        {
            if (recipe != null)
                CreateView();
        }

        void OnDisable() => DisposeView();

        void OnGUI()
        {
            DrawRecipeSelector();

            if (recipe == null)
            {
                EditorGUILayout.HelpBox("Select a TextureRecipe asset.", MessageType.Info);
                return;
            }

            view ??= new TextureRecipeEditorView(recipe, Repaint);
            view.DrawWindow(position.height);
        }

        void DrawRecipeSelector()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var selectedRecipe = (TextureRecipe)EditorGUILayout.ObjectField(
                    recipe,
                    typeof(TextureRecipe),
                    false,
                    GUILayout.MinWidth(120f));

                if (EditorGUI.EndChangeCheck())
                    SetRecipe(selectedRecipe);

                using (new EditorGUI.DisabledScope(Selection.activeObject is not TextureRecipe))
                {
                    if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                        SetRecipe((TextureRecipe)Selection.activeObject);
                }
            }
        }

        void SetRecipe(TextureRecipe selectedRecipe)
        {
            if (recipe == selectedRecipe)
                return;

            DisposeView();
            recipe = selectedRecipe;

            if (recipe != null)
                CreateView();

            Repaint();
        }

        void CreateView() => view = new TextureRecipeEditorView(recipe, Repaint);

        void DisposeView()
        {
            view?.Dispose();
            view = null;
        }
    }

    enum TexturePreviewDisplayMode
    {
        RGBAlpha = 0,
        RGBAChannels = 1,
        RGB = 2,
        R = 3,
        G = 4,
        B = 5,
        A = 6
    }
}
