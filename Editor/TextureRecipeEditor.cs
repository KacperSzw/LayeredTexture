using System.Collections.Generic;
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
        readonly Dictionary<int, RenderTexture> layerPreviews = new();
        bool previewDirty = true;
        TexturePreviewDisplayMode previewDisplayMode;
        int previewSize;
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
        static float PreviewRailWidth => 154f;
        static float LayerPreviewHeight => 86f;
        static float HeaderPreviewWidth => 56f;
        const string PreviewDisplayModeKey = "Unmanaged.LayeredTexture.PreviewDisplayMode";
        const string PreviewSizeKey = "Unmanaged.LayeredTexture.PreviewSize";
        const int MinPreviewSize = 80;
        const int MaxPreviewSize = 512;

        void OnEnable()
        {
            output = serializedObject.FindProperty("Output");
            layers = serializedObject.FindProperty("RootStack").FindPropertyRelative("Layers");
            previewDisplayMode = (TexturePreviewDisplayMode)EditorPrefs.GetInt(
                PreviewDisplayModeKey,
                (int)TexturePreviewDisplayMode.RgbAlpha);
            previewSize = EditorPrefs.GetInt(PreviewSizeKey, 220);
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
            ReleaseLayerPreviews();
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
            var textureLayer = (TextureLayerBase)layer.managedReferenceValue;
            var headerPreviewRect = textureLayer.SupportsRawPreview
                ? new Rect(contentRect.xMax - HeaderPreviewWidth, contentRect.y, HeaderPreviewWidth, HeaderHeight)
                : default;
            var headerRect = textureLayer.SupportsRawPreview
                ? new Rect(contentRect.x, contentRect.y, headerPreviewRect.x - contentRect.x - SectionGap, HeaderHeight)
                : new Rect(contentRect.x, contentRect.y, contentRect.width, HeaderHeight);

            DrawLayerHeaderRow(headerRect, layer);

            if (!layer.isExpanded && textureLayer.SupportsRawPreview)
                DrawLayerPreviewColumn(headerPreviewRect, index, textureLayer, true);

            if (!layer.isExpanded)
                return;

            var y = headerRect.yMax + SectionGap;
            var bodyHeight = GetLayerBodyHeight(layer);
            var fieldsWidth = contentRect.width;

            if (textureLayer.SupportsRawPreview)
            {
                var previewRailRect = new Rect(
                    contentRect.xMax - PreviewRailWidth,
                    y,
                    PreviewRailWidth,
                    bodyHeight);
                DrawLayerPreviewColumn(previewRailRect, index, textureLayer, false);
                fieldsWidth = Mathf.Max(1f, previewRailRect.x - contentRect.x - SectionGap);
            }

            var specificRect = new Rect(contentRect.x, y, fieldsWidth, GetLayerSpecificHeight(layer));
            DrawLayerSpecificBox(specificRect, layer);
        }

        void DrawLayerHeaderRow(Rect rect, SerializedProperty layer)
        {
            var textureLayer = (TextureLayerBase)layer.managedReferenceValue;
            var enabled = layer.FindPropertyRelative("Enabled");
            var writeMask = layer.FindPropertyRelative("WriteMask");
            var opacity = layer.FindPropertyRelative("Opacity");
            var blendMode = layer.FindPropertyRelative("BlendMode");
            var rowY = rect.y + 2f;
            var enabledRect = new Rect(rect.x + 3f, rowY + 1f, 18f, LineHeight);
            var foldoutArrowRect = new Rect(enabledRect.xMax + 14f, rowY, 14f, LineHeight);
            var roleRect = new Rect(foldoutArrowRect.xMax + 4f, rowY + 2f, 18f, LineHeight - 4f);
            var blendRect = new Rect(rect.xMax - 96f, rowY, 96f, LineHeight);
            var opacityRect = new Rect(blendRect.x - 116f, rowY, 112f, LineHeight);
            var channelRect = new Rect(opacityRect.x - 98f, rowY + 1f, 94f, LineHeight - 2f);
            var titleRect = new Rect(
                roleRect.xMax + 6f,
                rowY,
                Mathf.Max(80f, channelRect.x - roleRect.xMax - 8f),
                LineHeight);

            EditorGUI.PropertyField(enabledRect, enabled, GUIContent.none);
            layer.isExpanded = EditorGUI.Foldout(foldoutArrowRect, layer.isExpanded, GUIContent.none, true);
            DrawRoleBadge(roleRect, textureLayer.Role);
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
                case WaterWavesLayer:
                    DrawWaterWavesFields(ref rect, layer);
                    break;
                case NormalFromHeightLayer:
                    DrawNoiseRow(
                        NextLine(ref rect),
                        layer.FindPropertyRelative("HeightUsage"),
                        "Height",
                        layer.FindPropertyRelative("Strength"),
                        "Strength");
                    break;
                case RecipeReferenceLayer:
                    EditorGUI.PropertyField(NextLine(ref rect), layer.FindPropertyRelative("Recipe"));
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
                + GetLayerBodyHeight(layer)
                + 4f;
        }

        static float GetLayerSpecificHeight(SerializedProperty layer)
        {
            var lines = layer.managedReferenceValue switch
            {
                SolidColorLayer => 1,
                TextureFileLayer => 1,
                NoiseLayer => 6,
                WaterWavesLayer => IsWaterWavesFoam(layer) ? 6 : 5,
                NormalFromHeightLayer => 1,
                RecipeReferenceLayer => 1,
                _ => 2
            };

            lines++;
            return lines * LineHeight + (lines - 1) * Spacing + SectionPadding * 2f;
        }

        static float GetLayerBodyHeight(SerializedProperty layer)
        {
            var height = GetLayerSpecificHeight(layer);

            return layer.managedReferenceValue is TextureLayerBase { SupportsRawPreview: true }
                ? Mathf.Max(height, LayerPreviewHeight)
                : height;
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

        static void DrawRoleBadge(Rect rect, TextureLayerRole role)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = role == TextureLayerRole.Source
                ? new Color(0.34f, 0.56f, 0.78f)
                : new Color(0.74f, 0.48f, 0.26f);

            GUI.Box(rect, role == TextureLayerRole.Source ? "S" : "P", EditorStyles.miniButton);
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
            menu.AddItem(new GUIContent("Water Waves"), false, () => AddLayer(new WaterWavesLayer()));
            menu.AddItem(new GUIContent("Normal From Height"), false, () => AddLayer(new NormalFromHeightLayer()));
            menu.AddItem(new GUIContent("Recipe Reference"), false, () => AddLayer(new RecipeReferenceLayer()));
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
            EditorGUI.BeginChangeCheck();
            previewDisplayMode = (TexturePreviewDisplayMode)EditorGUILayout.EnumPopup(
                previewDisplayMode,
                GUILayout.Width(96f));
            previewSize = EditorGUILayout.IntSlider(
                previewSize,
                MinPreviewSize,
                MaxPreviewSize,
                GUILayout.Width(180f));

            if (!EditorGUI.EndChangeCheck())
                return;

            EditorPrefs.SetInt(PreviewDisplayModeKey, (int)previewDisplayMode);
            EditorPrefs.SetInt(PreviewSizeKey, previewSize);
            Repaint();
        }

        void DrawLayerPreviewColumn(Rect rect, int index, TextureLayerBase layer, bool compact)
        {
            var preview = GetLayerPreview(index, layer);

            if (compact)
                TexturePreviewGUI.DrawCompact(rect, preview);
            else
                TexturePreviewGUI.Draw(rect, preview);
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
            ReleaseLayerPreviews();
            Repaint();
        }

        void HandleUndoRedo() => MarkPreviewDirty();

        void ReleasePreview()
        {
            ReleaseRenderTexture(previewTexture);
            previewTexture = null;
        }

        void ReleaseLayerPreviews()
        {
            foreach (var preview in layerPreviews.Values)
                ReleaseRenderTexture(preview);

            layerPreviews.Clear();
        }

        RenderTexture GetLayerPreview(int index, TextureLayerBase layer)
        {
            if (layerPreviews.TryGetValue(index, out var preview))
                return preview;

            preview = TextureLayerPreviewEvaluator.EvaluateRaw((TextureRecipe)target, layer);
            layerPreviews[index] = preview;
            return preview;
        }

        static void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            DestroyImmediate(texture);
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
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("NoiseType"),
                "Type",
                layer.FindPropertyRelative("Fractal"),
                "Fractal",
                layer.FindPropertyRelative("Seed"),
                "Seed");
            DrawNoiseScaleRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Scale"),
                layer.FindPropertyRelative("Rotation"));
            DrawNoiseOffsetRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Offset"));

            var fractal = layer.FindPropertyRelative("Fractal");

            using (new EditorGUI.DisabledScope((NoiseFractal)fractal.enumValueIndex == NoiseFractal.None))
            {
                DrawNoiseRow(
                    NextLine(ref rect),
                    layer.FindPropertyRelative("Octaves"),
                    "Octaves",
                    layer.FindPropertyRelative("Lacunarity"),
                    "Lacunarity",
                    layer.FindPropertyRelative("Gain"),
                    "Gain");
            }

            var warpStrength = layer.FindPropertyRelative("WarpStrength");
            DrawNoiseWarpRow(
                NextLine(ref rect),
                warpStrength,
                layer.FindPropertyRelative("WarpScale"),
                layer.FindPropertyRelative("WarpOctaves"));
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Contrast"),
                "Contrast",
                layer.FindPropertyRelative("Brightness"),
                "Brightness",
                layer.FindPropertyRelative("Invert"),
                "Invert");
        }

        static void DrawWaterWavesFields(ref Rect rect, SerializedProperty layer)
        {
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("OutputMode"),
                "Output",
                layer.FindPropertyRelative("Seed"),
                "Seed",
                layer.FindPropertyRelative("WaveCount"),
                "Waves");
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("WindDirection"),
                "Direction",
                layer.FindPropertyRelative("DirectionSpread"),
                "Spread",
                layer.FindPropertyRelative("Phase"),
                "Phase");
            DrawVector2Row(
                NextLine(ref rect),
                layer.FindPropertyRelative("CycleRange"),
                "Min Cycles",
                "Max Cycles");
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Amplitude"),
                "Amplitude",
                layer.FindPropertyRelative("AmplitudeFalloff"),
                "Falloff",
                layer.FindPropertyRelative("Choppiness"),
                "Chop");

            if (IsWaterWavesFoam(layer))
                DrawNoiseRow(
                    NextLine(ref rect),
                    layer.FindPropertyRelative("FoamThreshold"),
                    "Foam Threshold",
                    layer.FindPropertyRelative("FoamSoftness"),
                    "Foam Softness");

            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Contrast"),
                "Contrast",
                layer.FindPropertyRelative("Brightness"),
                "Brightness",
                layer.FindPropertyRelative("Invert"),
                "Invert");
        }

        static bool IsWaterWavesFoam(SerializedProperty layer) =>
            (WaterWavesOutputMode)layer.FindPropertyRelative("OutputMode").enumValueIndex == WaterWavesOutputMode.Foam;

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

        static void DrawNoiseWarpRow(
            Rect rect,
            SerializedProperty strength,
            SerializedProperty scale,
            SerializedProperty octaves)
        {
            const float Gap = 8f;
            var width = (rect.width - Gap * 2f) / 3f;
            DrawLabeledField(new Rect(rect.x, rect.y, width, rect.height), strength, "Warp Strength");

            using (new EditorGUI.DisabledScope(Mathf.Abs(strength.floatValue) <= 0.0001f))
            {
                DrawLabeledField(new Rect(rect.x + width + Gap, rect.y, width, rect.height), scale, "Warp Scale");
                DrawLabeledField(new Rect(rect.x + (width + Gap) * 2f, rect.y, width, rect.height), octaves, "Warp Octaves");
            }
        }

        static void DrawNoiseScaleRow(Rect rect, SerializedProperty scale, SerializedProperty rotation)
        {
            const float Gap = 8f;
            var width = (rect.width - Gap * 2f) / 3f;
            var value = scale.vector2Value;

            EditorGUI.BeginChangeCheck();
            value.x = DrawFloatField(new Rect(rect.x, rect.y, width, rect.height), "Scale X", value.x);
            value.y = DrawFloatField(new Rect(rect.x + width + Gap, rect.y, width, rect.height), "Scale Y", value.y);

            if (EditorGUI.EndChangeCheck())
                scale.vector2Value = value;

            DrawLabeledField(new Rect(rect.x + (width + Gap) * 2f, rect.y, width, rect.height), rotation, "Rotation");
        }

        static void DrawNoiseOffsetRow(Rect rect, SerializedProperty offset)
        {
            const float Gap = 8f;
            var width = (rect.width - Gap) * 0.5f;
            var value = offset.vector2Value;

            EditorGUI.BeginChangeCheck();
            value.x = DrawFloatField(new Rect(rect.x, rect.y, width, rect.height), "Offset X", value.x);
            value.y = DrawFloatField(
                new Rect(rect.x + width + Gap, rect.y, width, rect.height),
                "Offset Y",
                value.y);

            if (EditorGUI.EndChangeCheck())
                offset.vector2Value = value;
        }

        static void DrawVector2Row(Rect rect, SerializedProperty property, string xLabel, string yLabel)
        {
            const float Gap = 8f;
            var width = (rect.width - Gap) * 0.5f;
            var value = property.vector2Value;

            EditorGUI.BeginChangeCheck();
            value.x = DrawFloatField(new Rect(rect.x, rect.y, width, rect.height), xLabel, value.x);
            value.y = DrawFloatField(
                new Rect(rect.x + width + Gap, rect.y, width, rect.height),
                yLabel,
                value.y);

            if (EditorGUI.EndChangeCheck())
                property.vector2Value = value;
        }

        static void DrawNoiseRow(
            Rect rect,
            SerializedProperty first,
            string firstLabel)
        {
            DrawLabeledField(rect, first, firstLabel);
        }

        static void DrawNoiseRow(
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

        static void DrawNoiseRow(
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

        static float DrawFloatField(Rect rect, string label, float value)
        {
            var previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(LabelWidth, rect.width * 0.45f);
            var result = EditorGUI.FloatField(rect, label, value);
            EditorGUIUtility.labelWidth = previousWidth;
            return result;
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

        static void DrawLabeledField(Rect rect, SerializedProperty property, string label)
        {
            var previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(LabelWidth, rect.width * 0.45f);
            EditorGUI.PropertyField(rect, property, new GUIContent(label));
            EditorGUIUtility.labelWidth = previousWidth;
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
                WaterWavesLayer => "Water Waves",
                NormalFromHeightLayer => "Normal From Height",
                RecipeReferenceLayer => "Recipe Reference",
                _ => layer.managedReferenceValue.GetType().Name
            };
        }
    }

    enum TexturePreviewDisplayMode
    {
        RgbAlpha,
        RGBAChannels
    }
}
