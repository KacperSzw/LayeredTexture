using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    /// <summary>
    /// Editor context used when drawing one serialized LayerStack.
    /// </summary>
    public sealed class TextureLayerStackEditorContext
    {
        /// <summary>
        /// Object used for undo and dirty state. Defaults to the serialized object's target.
        /// </summary>
        public UnityEngine.Object UndoTarget;

        /// <summary>
        /// Recipe used for source resolution and raw layer previews.
        /// </summary>
        public TextureRecipe PreviewRecipe;

        /// <summary>
        /// Output profile provider used for raw layer previews.
        /// </summary>
        public Func<OutputProfile> GetOutput;

        /// <summary>
        /// Preview color mode provider used for raw layer previews.
        /// </summary>
        public Func<TexturePreviewColorMode> GetPreviewColorMode;

        /// <summary>
        /// Texture resolver used for file-backed texture sources.
        /// </summary>
        public ITextureSourceResolver SourceResolver;

        /// <summary>
        /// Callback invoked when stack content changes.
        /// </summary>
        public Action Changed;

        /// <summary>
        /// Reorderable list header label.
        /// </summary>
        public string HeaderLabel = "Layers";
    }

    /// <summary>
    /// Reusable editor drawer for one serialized LayerStack or Layers list.
    /// </summary>
    public sealed class TextureLayerStackEditor : IDisposable
    {
        readonly SerializedObject serializedObject;
        readonly TextureLayerStackEditorContext context;
        readonly Action repaint;
        readonly Dictionary<int, RenderTexture> layerPreviews = new();
        SerializedProperty layers;
        ReorderableList layerList;
        bool clipLayerPreviews;
        Rect layerPreviewScreenClipRect;
        int removeLayerIndex = -1;

        static float LineHeight => EditorGUIUtility.singleLineHeight;
        static float Spacing => EditorGUIUtility.standardVerticalSpacing;
        static float LayerPadding => 6f;
        static float SectionPadding => 5f;
        static float SectionGap => 4f;
        static float HeaderHeight => 28f;
        static float LabelWidth => 82f;
        static float PreviewRailWidth => 154f;
        static float LayerPreviewHeight => 86f;
        static float HeaderPreviewWidth => 74f;
        static float RemoveButtonWidth => 22f;

        /// <summary>
        /// Creates a layer stack editor for a LayerStack property or its Layers child property.
        /// </summary>
        public TextureLayerStackEditor(
            SerializedObject serializedObject,
            SerializedProperty stackOrLayers,
            TextureLayerStackEditorContext context,
            Action repaint = null)
        {
            this.serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            this.context = context ?? new TextureLayerStackEditorContext();
            this.repaint = repaint;
            layers = ResolveLayersProperty(stackOrLayers);
            Initialize();
        }

        /// <summary>
        /// Current number of serialized layers.
        /// </summary>
        public int LayerCount => layers.arraySize;

        /// <inheritdoc />
        public void Dispose()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            ReleaseLayerPreviews();
        }

        /// <summary>
        /// Draws the stack list using GUILayout.
        /// </summary>
        public void DrawLayout()
        {
            EditorGUI.BeginChangeCheck();
            layerList.DoLayoutList();
            RemovePendingLayer();

            if (EditorGUI.EndChangeCheck())
                MarkPreviewDirty();
        }

        /// <summary>
        /// Clips raw layer preview textures to a screen-space rectangle.
        /// </summary>
        public void SetPreviewClip(Rect screenRect)
        {
            layerPreviewScreenClipRect = screenRect;
            clipLayerPreviews = true;
        }

        /// <summary>
        /// Removes the active preview clipping rectangle.
        /// </summary>
        public void ClearPreviewClip() => clipLayerPreviews = false;

        /// <summary>
        /// Releases cached raw layer previews and requests repaint.
        /// </summary>
        public void MarkPreviewDirty()
        {
            ReleaseLayerPreviews();
            context.Changed?.Invoke();
            repaint?.Invoke();
        }

        /// <summary>
        /// Appends a layer to the stack.
        /// </summary>
        public void AddLayer(TextureLayerBase layer)
        {
            serializedObject.Update();
            Undo.RegisterCompleteObjectUndo(UndoTarget, "Add Texture Layer");

            var index = layers.arraySize;
            layers.arraySize++;
            layers.GetArrayElementAtIndex(index).managedReferenceValue = layer;
            layers.GetArrayElementAtIndex(index).isExpanded = true;
            layerList.index = index;

            serializedObject.ApplyModifiedProperties();
            MarkPreviewDirty();
        }

        /// <summary>
        /// Removes the layer at the given index.
        /// </summary>
        public void RemoveLayerAt(int index)
        {
            if (index < 0 || index >= layers.arraySize)
                return;

            serializedObject.Update();
            Undo.RegisterCompleteObjectUndo(UndoTarget, "Remove Texture Layer");
            layers.DeleteArrayElementAtIndex(index);
            layerList.index = Mathf.Min(index, layers.arraySize - 1);
            serializedObject.ApplyModifiedProperties();
            MarkPreviewDirty();
        }

        /// <summary>
        /// Duplicates the layer at the given index.
        /// </summary>
        public bool DuplicateLayerAt(int index)
        {
            if (index < 0 || index >= layers.arraySize)
                return false;

            var layer = layers.GetArrayElementAtIndex(index).managedReferenceValue as TextureLayerBase;

            if (!TextureLayerClipboard.TryClone(layer, out var clone))
                return false;

            InsertLayerAfter(index, clone, "Duplicate Texture Layer");
            return true;
        }

        static SerializedProperty ResolveLayersProperty(SerializedProperty stackOrLayers)
        {
            if (stackOrLayers == null)
                throw new ArgumentNullException(nameof(stackOrLayers));

            if (IsLayerList(stackOrLayers))
                return stackOrLayers;

            var layers = stackOrLayers.FindPropertyRelative("Layers");

            if (IsLayerList(layers))
                return layers;

            throw new ArgumentException(
                "TextureLayerStackEditor requires a LayerStack property or its Layers list.",
                nameof(stackOrLayers));
        }

        static bool IsLayerList(SerializedProperty property) =>
            property != null
            && property.isArray
            && property.propertyType == SerializedPropertyType.Generic;

        void Initialize()
        {
            layerList = new ReorderableList(serializedObject, layers, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, HeaderLabel),
                drawElementCallback = DrawLayer,
                elementHeightCallback = GetLayerHeight,
                onAddDropdownCallback = ShowLayerMenu,
                onRemoveCallback = RemoveLayer,
                onReorderCallback = _ => MarkPreviewDirty()
            };

            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        void DrawLayer(Rect rect, int index, bool active, bool focused)
        {
            var layer = layers.GetArrayElementAtIndex(index);
            var layerRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);

            if (layer.managedReferenceValue == null)
            {
                DrawLayerFrame(layerRect);
                EditorGUI.LabelField(Inset(layerRect, LayerPadding, LayerPadding), "Missing Layer");
                HandleMissingLayerContextMenu(layerRect, index);
                return;
            }

            DrawLayerFrame(layerRect);
            HandleLayerContextMenu(layerRect, index, (TextureLayerBase)layer.managedReferenceValue);

            var contentRect = Inset(layerRect, LayerPadding, LayerPadding);
            var textureLayer = (TextureLayerBase)layer.managedReferenceValue;
            var removeRect = new Rect(
                contentRect.xMax - RemoveButtonWidth,
                contentRect.y + (HeaderHeight - LineHeight) * 0.5f,
                RemoveButtonWidth,
                LineHeight);
            var headerRight = removeRect.x - SectionGap;
            var headerPreviewRect = new Rect(
                headerRight - HeaderPreviewWidth,
                contentRect.y,
                HeaderPreviewWidth,
                HeaderHeight);
            var showCollapsedPreview = !layer.isExpanded && textureLayer.SupportsRawPreview;
            var headerRect = new Rect(
                contentRect.x,
                contentRect.y,
                headerPreviewRect.x - contentRect.x - SectionGap,
                HeaderHeight);

            DrawLayerHeaderRow(headerRect, layer);
            DrawRemoveLayerButton(removeRect, index);

            if (showCollapsedPreview)
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
            var rowY = rect.y + (HeaderHeight - LineHeight) * 0.5f;
            var enabledRect = new Rect(rect.x + 3f, rowY + 1f, 18f, LineHeight);
            var foldoutArrowRect = new Rect(enabledRect.xMax + 14f, rowY, 14f, LineHeight);
            var roleRect = new Rect(foldoutArrowRect.xMax + 4f, rowY + 2f, 30f, LineHeight - 4f);
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
            HandleFoldoutClick(new Rect(foldoutArrowRect.xMax, rowY, titleRect.xMax - foldoutArrowRect.xMax, LineHeight), layer);
            DrawWriteMaskButtons(channelRect, writeMask);
            opacity.floatValue = EditorGUI.Slider(opacityRect, GUIContent.none, opacity.floatValue, 0f, 1f);
            EditorGUI.PropertyField(blendRect, blendMode, GUIContent.none);
        }

        void DrawLayerSpecificBox(Rect rect, SerializedProperty layer)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
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
                    DrawTextureSource(NextLine(ref rect), layer.FindPropertyRelative("Source"));
                    break;
                case NoiseLayer:
                    DrawNoiseFields(ref rect, layer);
                    break;
                case WarpLayer:
                    DrawWarpFields(ref rect, layer);
                    break;
                case BlurLayer:
                    DrawBlurFields(ref rect, layer);
                    break;
                case TransformLayer:
                    DrawVector2Row(
                        NextLine(ref rect),
                        layer.FindPropertyRelative("Offset"),
                        "Offset X",
                        "Offset Y");
                    DrawVector2Row(
                        NextLine(ref rect),
                        layer.FindPropertyRelative("Scale"),
                        "Scale X",
                        "Scale Y");
                    DrawNoiseRow(
                        NextLine(ref rect),
                        layer.FindPropertyRelative("Rotation"),
                        "Rotation",
                        layer.FindPropertyRelative("Pivot"),
                        "Pivot");
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
                case InvertLayer:
                    EditorGUI.LabelField(NextLine(ref rect), "Inverts selected write channels.");
                    break;
                case HistogramSelectLayer:
                    DrawHistogramSelectFields(ref rect, layer);
                    break;
                case SaturationLayer:
                    DrawSaturationFields(ref rect, layer);
                    break;
                case SignedDistanceFieldLayer:
                    DrawSignedDistanceFieldFields(ref rect, layer);
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
                NoiseLayer => 5,
                WarpLayer => 2,
                BlurLayer => 1,
                TransformLayer => 3,
                WaterWavesLayer => IsWaterWavesFoam(layer) ? 6 : 5,
                NormalFromHeightLayer => 1,
                InvertLayer => 1,
                HistogramSelectLayer => 2,
                SaturationLayer => 2,
                SignedDistanceFieldLayer => 3,
                RecipeReferenceLayer => 1,
                _ => 2
            };

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

        static void HandleFoldoutClick(Rect rect, SerializedProperty layer)
        {
            var current = Event.current;

            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition))
                return;

            layer.isExpanded = !layer.isExpanded;
            GUI.changed = true;
            current.Use();
        }

        static void DrawRoleBadge(Rect rect, TextureLayerRole role)
        {
            var fill = role == TextureLayerRole.Source
                ? new Color(0.16f, 0.32f, 0.48f, 0.9f)
                : new Color(0.42f, 0.25f, 0.12f, 0.9f);

            EditorGUI.DrawRect(rect, fill);
            DrawBorder(rect, new Color(0f, 0f, 0f, 0.45f));

            var style = EditorStyles.centeredGreyMiniLabel;
            var previousColor = style.normal.textColor;
            style.normal.textColor = new Color(0.86f, 0.9f, 0.94f);
            GUI.Label(rect, role == TextureLayerRole.Source ? "SRC" : "FX", style);
            style.normal.textColor = previousColor;
        }

        static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        static void DrawLayerFrame(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var tint = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.025f)
                : new Color(0f, 0f, 0f, 0.025f);
            EditorGUI.DrawRect(Inset(rect, 1f, 1f), tint);
        }

        void ShowLayerMenu(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("SRC/Solid Color"), false, () => AddLayer(new SolidColorLayer()));
            menu.AddItem(new GUIContent("SRC/Texture File"), false, () => AddLayer(new TextureFileLayer()));
            menu.AddItem(new GUIContent("SRC/Noise"), false, () => AddLayer(new NoiseLayer()));
            menu.AddItem(new GUIContent("SRC/Water Waves"), false, () => AddLayer(new WaterWavesLayer()));
            menu.AddItem(new GUIContent("SRC/Recipe Reference"), false, () => AddLayer(new RecipeReferenceLayer()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("FX/Blur"), false, () => AddLayer(new BlurLayer()));
            menu.AddItem(new GUIContent("FX/Transform"), false, () => AddLayer(new TransformLayer()));
            menu.AddItem(new GUIContent("FX/Warp"), false, () => AddLayer(new WarpLayer()));
            menu.AddItem(new GUIContent("FX/Normal From Height"), false, () => AddLayer(new NormalFromHeightLayer()));
            menu.AddItem(new GUIContent("FX/Invert"), false, () => AddLayer(new InvertLayer()));
            menu.AddItem(new GUIContent("FX/Histogram Select"), false, () => AddLayer(new HistogramSelectLayer()));
            menu.AddItem(new GUIContent("FX/Hue/Saturation"), false, () => AddLayer(new SaturationLayer()));
            menu.AddItem(new GUIContent("FX/SDF"), false, () => AddLayer(new SignedDistanceFieldLayer()));
            menu.DropDown(buttonRect);
        }

        void RemoveLayer(ReorderableList list) => RemoveLayerAt(list.index);

        void RemovePendingLayer()
        {
            if (removeLayerIndex < 0)
                return;

            RemoveLayerAt(removeLayerIndex);
            removeLayerIndex = -1;
        }

        void DrawRemoveLayerButton(Rect rect, int index)
        {
            if (!GUI.Button(rect, new GUIContent("x", "Remove layer"), EditorStyles.miniButton))
                return;

            removeLayerIndex = index;
            GUI.changed = true;
        }

        void HandleLayerContextMenu(Rect rect, int index, TextureLayerBase layer)
        {
            var current = Event.current;

            if (current.type != EventType.ContextClick || !rect.Contains(current.mousePosition))
                return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Layer"), false, () => TextureLayerClipboard.Copy(layer));

            if (TextureLayerClipboard.CanPasteValues(layer))
                menu.AddItem(new GUIContent("Paste Values"), false, () => PasteLayerValues(index));
            else
                menu.AddDisabledItem(new GUIContent("Paste Values"));

            if (TextureLayerClipboard.HasLayer)
                menu.AddItem(new GUIContent("Paste As New"), false, () => PasteLayerAsNew(index));
            else
                menu.AddDisabledItem(new GUIContent("Paste As New"));

            menu.AddItem(new GUIContent("Duplicate Layer"), false, () => DuplicateLayerAt(index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove Layer"), false, () => RemoveLayerAt(index));
            menu.ShowAsContext();
            current.Use();
        }

        void HandleMissingLayerContextMenu(Rect rect, int index)
        {
            var current = Event.current;

            if (current.type != EventType.ContextClick || !rect.Contains(current.mousePosition))
                return;

            var menu = new GenericMenu();

            if (TextureLayerClipboard.HasLayer)
                menu.AddItem(new GUIContent("Paste As New"), false, () => PasteLayerAsNew(index));
            else
                menu.AddDisabledItem(new GUIContent("Paste As New"));

            menu.AddItem(new GUIContent("Remove Layer"), false, () => RemoveLayerAt(index));
            menu.ShowAsContext();
            current.Use();
        }

        void PasteLayerValues(int index)
        {
            serializedObject.Update();

            if (index < 0 || index >= layers.arraySize)
                return;

            var layer = layers.GetArrayElementAtIndex(index).managedReferenceValue as TextureLayerBase;

            if (!TextureLayerClipboard.CanPasteValues(layer))
                return;

            Undo.RegisterCompleteObjectUndo(UndoTarget, "Paste Texture Layer Values");
            TextureLayerClipboard.TryPasteValues(layer);
            EditorUtility.SetDirty(UndoTarget);
            serializedObject.Update();
            MarkPreviewDirty();
        }

        void PasteLayerAsNew(int index)
        {
            if (!TextureLayerClipboard.TryCloneCopiedLayer(out var layer))
                return;

            InsertLayerAfter(index, layer, "Paste Texture Layer");
        }

        void InsertLayerAfter(int index, TextureLayerBase layer, string undoName)
        {
            serializedObject.Update();
            Undo.RegisterCompleteObjectUndo(UndoTarget, undoName);

            var insertIndex = Mathf.Clamp(index + 1, 0, layers.arraySize);
            if (insertIndex == layers.arraySize)
                layers.arraySize++;
            else
                layers.InsertArrayElementAtIndex(insertIndex);

            var property = layers.GetArrayElementAtIndex(insertIndex);
            property.managedReferenceValue = layer;
            property.isExpanded = true;
            layerList.index = insertIndex;

            serializedObject.ApplyModifiedProperties();
            MarkPreviewDirty();
        }

        void DrawLayerPreviewColumn(Rect rect, int index, TextureLayerBase layer, bool compact)
        {
            var preview = GetLayerPreview(index, layer);

            if (clipLayerPreviews)
            {
                DrawClippedLayerPreview(rect, preview, compact);
                return;
            }

            var output = Output;
            var colorMode = PreviewColorMode;

            if (compact)
                TexturePreviewGUI.DrawCompact(rect, preview, colorMode, output.SRGB, false);
            else
                TexturePreviewGUI.Draw(rect, preview, TexturePreviewDisplayMode.RGBAlpha, colorMode, output.SRGB, false);
        }

        void DrawClippedLayerPreview(Rect rect, Texture preview, bool compact)
        {
            var screenRect = ToScreenRect(rect);

            if (!ContainsVertically(layerPreviewScreenClipRect, screenRect))
                return;

            var output = Output;
            var colorMode = PreviewColorMode;

            if (compact)
                TexturePreviewGUI.DrawCompact(rect, preview, colorMode, output.SRGB, false);
            else
                TexturePreviewGUI.Draw(rect, preview, TexturePreviewDisplayMode.RGBAlpha, colorMode, output.SRGB, false);
        }

        RenderTexture GetLayerPreview(int index, TextureLayerBase layer)
        {
            if (layerPreviews.TryGetValue(index, out var preview))
                return preview;

            preview = TextureLayerPreviewEvaluator.EvaluateRaw(
                context.PreviewRecipe,
                layer,
                Output,
                SourceResolver);
            layerPreviews[index] = preview;
            return preview;
        }

        void DrawTextureSource(Rect rect, SerializedProperty source)
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
            AssetPathEditorGUI.Draw(
                rect,
                source.FindPropertyRelative("Path"),
                AssetPathPickerKind.TextureFile,
                GUIContent.none,
                MarkPreviewDirty);
        }

        void AssignTextureSource(SerializedProperty source, Texture texture)
        {
            var kind = source.FindPropertyRelative("Kind");
            var path = source.FindPropertyRelative("Path");
            var runtimeTexture = source.FindPropertyRelative("RuntimeTexture");

            AssetPathEditorGUI.Write(path, default);
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
                        AssetPathMode.Project,
                        out sourcePath)
                    || TextureRecipeEditorSourceResolver.TryMakeSourcePath(
                        assetPath,
                        AssetPathMode.Absolute,
                        out sourcePath)))
            {
                kind.enumValueIndex = (int)TextureSourceKind.File;
                AssetPathEditorGUI.Write(path, sourcePath);
                return;
            }

            kind.enumValueIndex = (int)TextureSourceKind.RuntimeTextureReference;
            runtimeTexture.objectReferenceValue = texture;
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
            DrawVector2Row(
                NextLine(ref rect),
                layer.FindPropertyRelative("Offset"),
                "Offset X",
                "Offset Y");

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

            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Contrast"),
                "Contrast",
                layer.FindPropertyRelative("Brightness"),
                "Brightness",
                layer.FindPropertyRelative("Invert"),
                "Invert");
        }

        static void DrawWarpFields(ref Rect rect, SerializedProperty layer)
        {
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Strength"),
                "Strength",
                layer.FindPropertyRelative("Scale"),
                "Scale",
                layer.FindPropertyRelative("Seed"),
                "Seed");
            DrawLabeledField(NextLine(ref rect), layer.FindPropertyRelative("Octaves"), "Octaves");
        }

        static void DrawBlurFields(ref Rect rect, SerializedProperty layer)
        {
            const float Gap = 8f;
            var line = NextLine(ref rect);
            var width = (line.width - Gap) * 0.5f;
            var radius = layer.FindPropertyRelative("Radius");
            var radiusMode = layer.FindPropertyRelative("RadiusMode");
            var radiusRect = new Rect(line.x, line.y, width, line.height);

            if ((BlurRadiusMode)radiusMode.enumValueIndex == BlurRadiusMode.UV)
                radius.floatValue = EditorGUI.Slider(radiusRect, "Radius", radius.floatValue, 0f, 1f);
            else
                DrawLabeledField(radiusRect, radius, "Radius Px");

            DrawLabeledField(
                new Rect(line.x + width + Gap, line.y, width, line.height),
                radiusMode,
                "Mode");
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

        static void DrawHistogramSelectFields(ref Rect rect, SerializedProperty layer)
        {
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Mode"),
                "Mode",
                layer.FindPropertyRelative("Position"),
                "Position");
            DrawNoiseRow(
                NextLine(ref rect),
                layer.FindPropertyRelative("Range"),
                "Range",
                layer.FindPropertyRelative("Gradient"),
                "Gradient");
        }

        static void DrawSaturationFields(ref Rect rect, SerializedProperty layer)
        {
            DrawLabeledField(NextLine(ref rect), layer.FindPropertyRelative("HueOffset"), "Hue");
            var line = NextLine(ref rect);
            var saturation = layer.FindPropertyRelative("Saturation");
            var luminance = layer.FindPropertyRelative("Luminance");
            const float Gap = 8f;
            var width = (line.width - Gap) * 0.5f;
            saturation.floatValue = EditorGUI.Slider(
                new Rect(line.x, line.y, width, line.height),
                "Saturation",
                saturation.floatValue,
                -1f,
                1f);
            luminance.floatValue = EditorGUI.Slider(
                new Rect(line.x + width + Gap, line.y, width, line.height),
                "Luminance",
                luminance.floatValue,
                -1f,
                1f);
        }

        static void DrawSignedDistanceFieldFields(ref Rect rect, SerializedProperty layer)
        {
            var line = NextLine(ref rect);
            const float Gap = 8f;
            var width = (line.width - Gap) * 0.5f;
            DrawLabeledField(
                new Rect(line.x, line.y, width, line.height),
                layer.FindPropertyRelative("InputUsage"),
                "Input");
            var threshold = layer.FindPropertyRelative("Threshold");
            threshold.floatValue = EditorGUI.Slider(
                new Rect(line.x + width + Gap, line.y, width, line.height),
                new GUIContent("Threshold", "Input mask value used as the SDF contour."),
                threshold.floatValue,
                0f,
                1f);

            line = NextLine(ref rect);
            var spreadPixels = layer.FindPropertyRelative("SpreadPixels");
            var edgeValue = layer.FindPropertyRelative("EdgeValue");
            spreadPixels.floatValue = EditorGUI.Slider(
                new Rect(line.x, line.y, width, line.height),
                "Spread Px",
                spreadPixels.floatValue,
                0f,
                SignedDistanceFieldLayer.MaxSpreadPixels);
            edgeValue.floatValue = EditorGUI.Slider(
                new Rect(line.x + width + Gap, line.y, width, line.height),
                new GUIContent("Edge", "Encoded SDF output value written exactly at the contour."),
                edgeValue.floatValue,
                0f,
                1f);
            DrawLabeledField(NextLine(ref rect), layer.FindPropertyRelative("InvertSign"), "Invert Sign");
        }

        static bool IsWaterWavesFoam(SerializedProperty layer) =>
            (WaterWavesOutputMode)layer.FindPropertyRelative("OutputMode").enumValueIndex == WaterWavesOutputMode.Foam;

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

        TextureSource ReadTextureSource(SerializedProperty source) => new()
        {
            Kind = (TextureSourceKind)source.FindPropertyRelative("Kind").enumValueIndex,
            Path = AssetPathEditorGUI.Read(source.FindPropertyRelative("Path")),
            RuntimeTexture = source.FindPropertyRelative("RuntimeTexture").objectReferenceValue as Texture
        };

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

        static bool ContainsVertically(Rect outer, Rect inner) =>
            inner.yMin >= outer.yMin
            && inner.yMax <= outer.yMax;

        static Rect ToScreenRect(Rect rect)
        {
            var min = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin, rect.yMin));
            var max = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static string LayerName(SerializedProperty layer)
        {
            return layer.managedReferenceValue switch
            {
                SolidColorLayer => "Solid Color",
                TextureFileLayer => "Texture File",
                NoiseLayer => "Noise",
                WarpLayer => "Warp",
                BlurLayer => "Blur",
                TransformLayer => "Transform",
                WaterWavesLayer => "Water Waves",
                NormalFromHeightLayer => "Normal From Height",
                InvertLayer => "Invert",
                HistogramSelectLayer => "Histogram Select",
                SaturationLayer => "Hue/Saturation",
                SignedDistanceFieldLayer => "Signed Distance Field",
                RecipeReferenceLayer => "Recipe Reference",
                _ => layer.managedReferenceValue.GetType().Name
            };
        }

        ITextureSourceResolver SourceResolver =>
            context.SourceResolver ?? TextureRecipeEditorSourceResolver.Instance;

        OutputProfile Output =>
            context.GetOutput != null
                ? context.GetOutput()
                : context.PreviewRecipe != null
                    ? context.PreviewRecipe.Output
                    : default;

        TexturePreviewColorMode PreviewColorMode =>
            context.GetPreviewColorMode != null
                ? context.GetPreviewColorMode()
                : TexturePreviewColorMode.Auto;

        string HeaderLabel =>
            string.IsNullOrEmpty(context.HeaderLabel) ? "Layers" : context.HeaderLabel;

        UnityEngine.Object UndoTarget =>
            context.UndoTarget != null ? context.UndoTarget : serializedObject.targetObject;

        void HandleUndoRedo() => MarkPreviewDirty();

        void ReleaseLayerPreviews()
        {
            foreach (var preview in layerPreviews.Values)
                LayeredTextureBakeUtility.Release(preview);

            layerPreviews.Clear();
        }
    }
}
