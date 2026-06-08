using System;
using System.Reflection;
using UnityEditor;

namespace Unmanaged.LayeredTexture.Editor
{
    public static class TextureLayerClipboard
    {
        static string copiedJson;
        static TextureLayerBase copiedLayer;
        static Type copiedType;

        public static bool HasLayer => copiedType != null;

        public static Type CopiedType => copiedType;

        public static void Clear()
        {
            copiedJson = null;
            copiedLayer = null;
            copiedType = null;
        }

        public static void Copy(TextureLayerBase layer)
        {
            if (layer == null)
            {
                Clear();
                return;
            }

            copiedType = layer.GetType();
            copiedJson = EditorJsonUtility.ToJson(layer);
            TryClone(layer, out copiedLayer);
        }

        public static bool CanPasteValues(TextureLayerBase target) =>
            target != null && copiedType == target.GetType();

        public static bool TryPasteValues(TextureLayerBase target)
        {
            if (!CanPasteValues(target))
                return false;

            EditorJsonUtility.FromJsonOverwrite(copiedJson, target);
            CopyUnityObjectReferences(copiedLayer, target, copiedType);
            return true;
        }

        public static bool TryClone(TextureLayerBase source, out TextureLayerBase clone)
        {
            clone = null;

            if (source == null)
                return false;

            if (!TryClone(source.GetType(), EditorJsonUtility.ToJson(source), out clone))
                return false;

            CopyUnityObjectReferences(source, clone, source.GetType());
            return true;
        }

        public static bool TryCloneCopiedLayer(out TextureLayerBase clone) =>
            TryClone(copiedLayer, out clone);

        static bool TryClone(Type type, string json, out TextureLayerBase clone)
        {
            clone = null;

            if (type == null || string.IsNullOrEmpty(json) || !typeof(TextureLayerBase).IsAssignableFrom(type))
                return false;

            clone = (TextureLayerBase)Activator.CreateInstance(type);
            EditorJsonUtility.FromJsonOverwrite(json, clone);
            return true;
        }

        static void CopyUnityObjectReferences(object source, object target, Type type)
        {
            if (source == null || target == null || type == null)
                return;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null && type != typeof(object))
            {
                foreach (var field in type.GetFields(Flags))
                    CopyUnityObjectReferenceField(field, source, target);

                type = type.BaseType;
            }
        }

        static void CopyUnityObjectReferenceField(FieldInfo field, object source, object target)
        {
            if (field.IsStatic || field.IsNotSerialized || field.IsInitOnly)
                return;

            var fieldType = field.FieldType;

            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                field.SetValue(target, field.GetValue(source));
                return;
            }

            if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string))
                return;

            if (!fieldType.IsValueType && !fieldType.IsClass)
                return;

            var sourceValue = field.GetValue(source);

            if (sourceValue == null)
                return;

            var targetValue = field.GetValue(target);

            if (fieldType.IsValueType)
            {
                CopyUnityObjectReferences(sourceValue, targetValue, fieldType);
                field.SetValue(target, targetValue);
                return;
            }

            if (targetValue == null)
            {
                targetValue = Activator.CreateInstance(fieldType);
                field.SetValue(target, targetValue);
            }

            CopyUnityObjectReferences(sourceValue, targetValue, fieldType);
        }
    }
}
