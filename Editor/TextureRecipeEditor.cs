using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    [CustomEditor(typeof(TextureRecipe))]
    sealed class TextureRecipeEditor : UnityEditor.Editor
    {
        SerializedProperty output;
        SerializedProperty layers;
        ReorderableList layerList;
        RenderTexture previewTexture;
        bool previewDirty = true;
        string previewError;
        string bakeStatus;
        MessageType bakeStatusType;

        static float LineHeight => EditorGUIUtility.singleLineHeight;
        static float Spacing => EditorGUIUtility.standardVerticalSpacing;
        static float LayerPadding => 6f;
        static float SectionPadding => 5f;
        static float SectionGap => 4f;
        static float HeaderHeight => 20f;
        static float LabelWidth => 82f;

        void OnEnable()
        {
            output = serializedObject.FindProperty("Output");
            layers = serializedObject.FindProperty("RootStack").FindPropertyRelative("Layers");
            layerList = new ReorderableList(serializedObject, layers, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Layers"),
                drawElementCallback = DrawLayer,
                elementHeightCallback = GetLayerHeight,
                onAddDropdownCallback = ShowLayerMenu,
                onRemoveCallback = RemoveLayer,
                onReorderCallback = _ => MarkPreviewDirty()
            };

            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            ReleasePreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            DrawOutput();
            EditorGUILayout.Space(6f);
            layerList.DoLayoutList();

            var changed = EditorGUI.EndChangeCheck();
            var applied = serializedObject.ApplyModifiedProperties();

            if (changed || applied)
                MarkPreviewDirty();

            EditorGUILayout.Space(6f);
            DrawBakeControls();
            DrawPreview();
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
                DrawOutputPath();
                EditorGUILayout.PropertyField(output.FindPropertyRelative("GenerateMips"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("SRGB"));
            }
        }

        void DrawOutputPath()
        {
            var path = output.FindPropertyRelative("OutputPath");
            var exportFormat = output.FindPropertyRelative("ExportFormat");
            var rect = EditorGUILayout.GetControlRect();
            var buttonRect = new Rect(rect.xMax - 62f, rect.y, 62f, rect.height);
            var pathRect = new Rect(rect.x, rect.y, rect.width - 66f, rect.height);

            EditorGUI.PropertyField(pathRect, path);

            if (!GUI.Button(buttonRect, "Browse"))
                return;

            var format = (ExportFileFormat)exportFormat.enumValueIndex;
            var extension = TextureRecipeBaker.ExtensionFor(format)?.TrimStart('.') ?? "png";
            var selectedPath = EditorUtility.SaveFilePanelInProject(
                "Bake TextureRecipe",
                target.name,
                extension,
                "Choose texture output path.");

            if (string.IsNullOrEmpty(selectedPath))
                return;

            path.stringValue = selectedPath.Replace('\\', '/');
            bakeStatus = null;
        }

        void DrawLayer(Rect rect, int index, bool active, bool focused)
        {
            var layer = layers.GetArrayElementAtIndex(index);
            var layerRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);

            if (layer.managedReferenceValue == null)
            {
                DrawLayerFrame(layerRect);
                EditorGUI.LabelField(Inset(layerRect, LayerPadding, LayerPadding), "Missing Layer");
                return;
            }

            DrawLayerFrame(layerRect);

            var contentRect = Inset(layerRect, LayerPadding, LayerPadding);
            var headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, HeaderHeight);
            DrawLayerHeaderRow(headerRect, layer);

            if (!layer.isExpanded)
                return;

            var y = headerRect.yMax + SectionGap;
            var specificRect = new Rect(contentRect.x, y, contentRect.width, GetLayerSpecificHeight(layer));
            DrawLayerSpecificBox(specificRect, layer);
        }

        void DrawLayerHeaderRow(Rect rect, SerializedProperty layer)
        {
            var enabled = layer.FindPropertyRelative("Enabled");
            var writeMask = layer.FindPropertyRelative("WriteMask");
            var opacity = layer.FindPropertyRelative("Opacity");
            var blendMode = layer.FindPropertyRelative("BlendMode");
            var enabledRect = new Rect(rect.x + 3f, rect.y + 1f, 18f, rect.height);
            var foldoutArrowRect = new Rect(enabledRect.xMax + 14f, rect.y, 14f, rect.height);
            var blendRect = new Rect(rect.xMax - 96f, rect.y, 96f, rect.height);
            var opacityRect = new Rect(blendRect.x - 116f, rect.y, 112f, rect.height);
            var channelRect = new Rect(opacityRect.x - 98f, rect.y + 1f, 94f, rect.height - 2f);
            var titleRect = new Rect(
                foldoutArrowRect.xMax + 6f,
                rect.y,
                Mathf.Max(80f, channelRect.x - foldoutArrowRect.xMax - 8f),
                rect.height);

            EditorGUI.PropertyField(enabledRect, enabled, GUIContent.none);
            layer.isExpanded = EditorGUI.Foldout(foldoutArrowRect, layer.isExpanded, GUIContent.none, true);
            EditorGUI.LabelField(titleRect, LayerName(layer), EditorStyles.boldLabel);
            DrawWriteMaskButtons(channelRect, writeMask);
            opacity.floatValue = EditorGUI.Slider(opacityRect, GUIContent.none, opacity.floatValue, 0f, 1f);
            EditorGUI.PropertyField(blendRect, blendMode, GUIContent.none);
        }

        void DrawLayerSpecificBox(Rect rect, SerializedProperty layer)
        {
            DrawSectionFrame(rect);
            DrawLayerFields(Inset(rect, SectionPadding, SectionPadding), layer);
        }

        void DrawLayerFields(Rect rect, SerializedProperty layer)
        {
            switch (layer.managedReferenceValue)
            {
                case SolidColorLayer:
                    EditorGUI.PropertyField(NextLine(ref rect), layer.FindPropertyRelative("Color"));
                    break;
                case TextureFileLayer:
                    DrawTextureSource(NextLine(ref rect), layer.FindPropertyRelative("Source"), "Texture");
                    break;
                case NoiseLayer:
                    DrawNoiseFields(ref rect, layer);
                    break;
                default:
                    var helpRect = NextLine(ref rect);
                    helpRect.height = LineHeight * 2f;
                    rect.y += LineHeight + Spacing;
                    EditorGUI.HelpBox(helpRect, "This layer type is not editable in the minimal inspector.", MessageType.Info);
                    break;
            }

            DrawMaskRow(NextLine(ref rect), layer.FindPropertyRelative("Mask"));
        }

        float GetLayerHeight(int index)
        {
            var layer = layers.GetArrayElementAtIndex(index);

            if (layer.managedReferenceValue == null || !layer.isExpanded)
                return HeaderHeight + LayerPadding * 2f + 4f;

            return HeaderHeight
                + LayerPadding * 2f
                + SectionGap
                + GetLayerSpecificHeight(layer)
                + 4f;
        }

        static float GetLayerSpecificHeight(SerializedProperty layer)
        {
            var lines = layer.managedReferenceValue switch
            {
                SolidColorLayer => 1,
                TextureFileLayer => 1,
                NoiseLayer => 6,
                _ => 2
            };

            lines++;
            return lines * LineHeight + (lines - 1) * Spacing + SectionPadding * 2f;
        }

        static void DrawWriteMaskButtons(Rect rect, SerializedProperty writeMask)
        {
            var buttonWidth = 22f;
            var gap = 2f;
            var value = writeMask.intValue;

            DrawWriteMaskButton(new Rect(rect.x, rect.y, buttonWidth, rect.height), "R", ChannelWriteMask.R, new Color(0.72f, 0.32f, 0.32f), ref value);
            DrawWriteMaskButton(new Rect(rect.x + (buttonWidth + gap), rect.y, buttonWidth, rect.height), "G", ChannelWriteMask.G, new Color(0.34f, 0.62f, 0.34f), ref value);
            DrawWriteMaskButton(new Rect(rect.x + (buttonWidth + gap) * 2f, rect.y, buttonWidth, rect.height), "B", ChannelWriteMask.B, new Color(0.32f, 0.42f, 0.74f), ref value);
            DrawWriteMaskButton(new Rect(rect.x + (buttonWidth + gap) * 3f, rect.y, buttonWidth, rect.height), "A", ChannelWriteMask.A, new Color(0.65f, 0.65f, 0.65f), ref value);

            writeMask.intValue = value;
        }

        static void DrawWriteMaskButton(Rect rect, string label, ChannelWriteMask channel, Color tint, ref int value)
        {
            var mask = (int)channel;
            var active = (value & mask) != 0;
            var previousColor = GUI.backgroundColor;

            if (active)
                GUI.backgroundColor = Color.Lerp(previousColor, tint, EditorGUIUtility.isProSkin ? 0.65f : 0.45f);

            if (GUI.Button(rect, label, EditorStyles.miniButton))
                value = active ? value & ~mask : value | mask;

            GUI.backgroundColor = previousColor;
        }

        static void DrawLayerFrame(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var tint = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.025f)
                : new Color(0f, 0f, 0f, 0.025f);
            EditorGUI.DrawRect(Inset(rect, 1f, 1f), tint);
        }

        static void DrawSectionFrame(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        }

        void ShowLayerMenu(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Solid Color"), false, () => AddLayer(new SolidColorLayer()));
            menu.AddItem(new GUIContent("Texture File"), false, () => AddLayer(new TextureFileLayer()));
            menu.AddItem(new GUIContent("Noise"), false, () => AddLayer(new NoiseLayer()));
            menu.DropDown(buttonRect);
        }

        void AddLayer(TextureLayerBase layer)
        {
            serializedObject.Update();

            var index = layers.arraySize;
            layers.arraySize++;
            layers.GetArrayElementAtIndex(index).managedReferenceValue = layer;
            layers.GetArrayElementAtIndex(index).isExpanded = true;
            layerList.index = index;

            serializedObject.ApplyModifiedProperties();
            MarkPreviewDirty();
        }

        void RemoveLayer(ReorderableList list)
        {
            if (list.index < 0 || list.index >= layers.arraySize)
                return;

            layers.DeleteArrayElementAtIndex(list.index);
            list.index = Mathf.Min(list.index, layers.arraySize - 1);
            MarkPreviewDirty();
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
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

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

            var aspect = previewTexture.height > 0
                ? (float)previewTexture.height / previewTexture.width
                : 1f;
            var width = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 38f);
            var height = Mathf.Clamp(width * aspect, 80f, 220f);
            var rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
        }

        void Bake()
        {
            serializedObject.ApplyModifiedProperties();

            if (TextureRecipeBaker.Bake((TextureRecipe)target, out var error))
            {
                bakeStatus = $"Baked {output.FindPropertyRelative("OutputPath").stringValue}";
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
                    (TextureRecipe)target,
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

        void HandleUndoRedo() => MarkPreviewDirty();

        void ReleasePreview()
        {
            if (previewTexture == null)
                return;

            previewTexture.Release();
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        void DrawTextureSource(Rect rect, SerializedProperty source, string _)
        {
            var kind = source.FindPropertyRelative("Kind");
            var modeRect = new Rect(rect.x, rect.y, 84f, rect.height);
            var contentRect = new Rect(modeRect.xMax + 4f, rect.y, rect.width - modeRect.width - 4f, rect.height);

            EditorGUI.PropertyField(modeRect, kind, GUIContent.none);

            if ((TextureSourceKind)kind.enumValueIndex == TextureSourceKind.File)
            {
                DrawTexturePathSource(contentRect, source);
                return;
            }

            var currentTexture = source.FindPropertyRelative("RuntimeTexture").objectReferenceValue as Texture;

            EditorGUI.BeginChangeCheck();
            var texture = (Texture)EditorGUI.ObjectField(contentRect, GUIContent.none, currentTexture, typeof(Texture), false);

            if (EditorGUI.EndChangeCheck())
                AssignTextureSource(source, texture);
        }

        void DrawTexturePathSource(Rect rect, SerializedProperty source)
        {
            var path = ReadAssetPath(source.FindPropertyRelative("Path"));
            var valid = TextureRecipeEditorSourceResolver.Instance.TryResolve((TextureRecipe)target, ReadTextureSource(source), out _);
            var buttonRect = new Rect(rect.xMax - 48f, rect.y, 48f, rect.height);
            var pathRect = new Rect(rect.x, rect.y, rect.width - 52f, rect.height);
            var tint = valid
                ? new Color(0.34f, 0.62f, 0.34f, EditorGUIUtility.isProSkin ? 0.22f : 0.14f)
                : new Color(0.72f, 0.32f, 0.32f, EditorGUIUtility.isProSkin ? 0.22f : 0.14f);

            EditorGUI.DrawRect(pathRect, tint);

            using (new EditorGUI.DisabledScope(true))
                EditorGUI.TextField(pathRect, path.Path ?? string.Empty);

            if (GUI.Button(buttonRect, "Pick"))
                PickRelativeTexture(source.propertyPath);
        }

        void PickRelativeTexture(string sourcePropertyPath)
        {
            RelativeTexturePickerWindow.Show(relativePath =>
            {
                serializedObject.Update();
                var source = serializedObject.FindProperty(sourcePropertyPath);
                AssignTextureSource(source, AssetPath.Relative(relativePath));
                serializedObject.ApplyModifiedProperties();
                MarkPreviewDirty();
            });
        }

        static void DrawNoiseFields(ref Rect rect, SerializedProperty layer)
        {
            DrawLabeledPair(
                NextLine(ref rect),
                layer.FindPropertyRelative("NoiseType"),
                "Type",
                layer.FindPropertyRelative("Fractal"),
                "Fractal");
            DrawLabeledPair(
                NextLine(ref rect),
                layer.FindPropertyRelative("Seed"),
                "Seed",
                layer.FindPropertyRelative("Scale"),
                "Scale");
            DrawLabeledPair(
                NextLine(ref rect),
                layer.FindPropertyRelative("Offset"),
                "Offset",
                layer.FindPropertyRelative("Rotation"),
                "Rotation");
            DrawLabeledTriple(
                NextLine(ref rect),
                layer.FindPropertyRelative("Octaves"),
                "Oct",
                layer.FindPropertyRelative("Lacunarity"),
                "Lacunarity",
                layer.FindPropertyRelative("Gain"),
                "Gain");
            DrawLabeledTriple(
                NextLine(ref rect),
                layer.FindPropertyRelative("WarpStrength"),
                "Warp",
                layer.FindPropertyRelative("WarpScale"),
                "Scale",
                layer.FindPropertyRelative("WarpOctaves"),
                "Oct");
            DrawLabeledTriple(
                NextLine(ref rect),
                layer.FindPropertyRelative("Contrast"),
                "Contrast",
                layer.FindPropertyRelative("Brightness"),
                "Brightness",
                layer.FindPropertyRelative("Invert"),
                "Invert");
        }

        void DrawMaskRow(Rect rect, SerializedProperty mask)
        {
            if (mask == null)
                return;

            var recipe = mask.FindPropertyRelative("RecipeReference");
            var usage = mask.FindPropertyRelative("Usage");
            var invert = mask.FindPropertyRelative("Invert");
            var opacity = mask.FindPropertyRelative("Opacity");
            var labelRect = new Rect(rect.x, rect.y, 34f, rect.height);
            var opacityRect = new Rect(rect.xMax - 88f, rect.y, 88f, rect.height);
            var invertRect = new Rect(opacityRect.x - 42f, rect.y, 38f, rect.height);
            var usageRect = new Rect(invertRect.x - 82f, rect.y, 78f, rect.height);
            var recipeRect = new Rect(labelRect.xMax + 4f, rect.y, usageRect.x - labelRect.xMax - 8f, rect.height);

            EditorGUI.LabelField(labelRect, "Mask");
            EditorGUI.PropertyField(recipeRect, recipe, GUIContent.none);
            EditorGUI.PropertyField(usageRect, usage, GUIContent.none);
            invert.boolValue = GUI.Toggle(invertRect, invert.boolValue, "Inv");
            opacity.floatValue = EditorGUI.Slider(opacityRect, GUIContent.none, opacity.floatValue, 0f, 1f);
        }

        Texture ResolveTexture(SerializedProperty source)
        {
            var kind = (TextureSourceKind)source.FindPropertyRelative("Kind").enumValueIndex;

            if (kind == TextureSourceKind.RuntimeTextureReference)
                return source.FindPropertyRelative("RuntimeTexture").objectReferenceValue as Texture;

            var textureSource = ReadTextureSource(source);
            return TextureRecipeEditorSourceResolver.Instance.TryResolve((TextureRecipe)target, textureSource, out var texture)
                ? texture
                : null;
        }

        void AssignTextureSource(SerializedProperty source, Texture texture)
        {
            var kind = source.FindPropertyRelative("Kind");
            var path = source.FindPropertyRelative("Path");
            var runtimeTexture = source.FindPropertyRelative("RuntimeTexture");

            WriteAssetPath(path, default);
            runtimeTexture.objectReferenceValue = null;

            if (texture == null)
            {
                kind.enumValueIndex = (int)TextureSourceKind.RuntimeTextureReference;
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(texture);

            if (!string.IsNullOrEmpty(assetPath)
                && (TextureRecipeEditorSourceResolver.TryMakeSourcePath(
                        assetPath,
                        AssetPathMode.Relative,
                        out var sourcePath)
                    || TextureRecipeEditorSourceResolver.TryMakeSourcePath(
                        assetPath,
                        AssetPathMode.Absolute,
                        out sourcePath)))
            {
                kind.enumValueIndex = (int)TextureSourceKind.File;
                WriteAssetPath(path, sourcePath);
                return;
            }

            kind.enumValueIndex = (int)TextureSourceKind.RuntimeTextureReference;
            runtimeTexture.objectReferenceValue = texture;
        }

        static void AssignTextureSource(SerializedProperty source, AssetPath assetPath)
        {
            source.FindPropertyRelative("Kind").enumValueIndex = (int)TextureSourceKind.File;
            WriteAssetPath(source.FindPropertyRelative("Path"), assetPath);
            source.FindPropertyRelative("RuntimeTexture").objectReferenceValue = null;
        }

        static TextureSource ReadTextureSource(SerializedProperty source) => new()
        {
            Kind = (TextureSourceKind)source.FindPropertyRelative("Kind").enumValueIndex,
            Path = ReadAssetPath(source.FindPropertyRelative("Path")),
            RuntimeTexture = source.FindPropertyRelative("RuntimeTexture").objectReferenceValue as Texture
        };

        static AssetPath ReadAssetPath(SerializedProperty path) => new()
        {
            Mode = (AssetPathMode)path.FindPropertyRelative("Mode").enumValueIndex,
            Path = path.FindPropertyRelative("Path").stringValue
        };

        static void WriteAssetPath(SerializedProperty path, AssetPath assetPath)
        {
            path.FindPropertyRelative("Mode").enumValueIndex = (int)assetPath.Mode;
            path.FindPropertyRelative("Path").stringValue = assetPath.Path;
        }

        static Rect NextLine(ref Rect rect)
        {
            var line = new Rect(rect.x, rect.y, rect.width, LineHeight);
            rect.y += LineHeight + Spacing;
            return line;
        }

        static void DrawLabeledPair(
            Rect rect,
            SerializedProperty first,
            string firstLabel,
            SerializedProperty second,
            string secondLabel)
        {
            const float Gap = 8f;
            var width = (rect.width - Gap) * 0.5f;
            DrawLabeledField(new Rect(rect.x, rect.y, width, rect.height), first, firstLabel);
            DrawLabeledField(new Rect(rect.x + width + Gap, rect.y, width, rect.height), second, secondLabel);
        }

        static void DrawLabeledTriple(
            Rect rect,
            SerializedProperty first,
            string firstLabel,
            SerializedProperty second,
            string secondLabel,
            SerializedProperty third,
            string thirdLabel)
        {
            const float Gap = 8f;
            var width = (rect.width - Gap * 2f) / 3f;
            DrawLabeledField(new Rect(rect.x, rect.y, width, rect.height), first, firstLabel);
            DrawLabeledField(new Rect(rect.x + width + Gap, rect.y, width, rect.height), second, secondLabel);
            DrawLabeledField(new Rect(rect.x + (width + Gap) * 2f, rect.y, width, rect.height), third, thirdLabel);
        }

        static void DrawLabeledField(Rect rect, SerializedProperty property, string label)
        {
            var labelWidth = Mathf.Min(LabelWidth, rect.width * 0.45f);
            var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var fieldRect = new Rect(labelRect.xMax + 3f, rect.y, rect.width - labelWidth - 3f, rect.height);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none);
        }

        static Rect Inset(Rect rect, float x, float y) =>
            new(rect.x + x, rect.y + y, rect.width - x * 2f, rect.height - y * 2f);

        static string LayerName(SerializedProperty layer)
        {
            return layer.managedReferenceValue switch
            {
                SolidColorLayer => "Solid Color",
                TextureFileLayer => "Texture File",
                NoiseLayer => "Noise",
                RecipeReferenceLayer => "Recipe Reference",
                _ => layer.managedReferenceValue.GetType().Name
            };
        }
    }
}
