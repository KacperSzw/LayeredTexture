using System;
using System.IO;

namespace Unmanaged.LayeredTexture.Editor
{
    static class TextureSetPbrTextureGatherer
    {
        internal static bool TryGather(AssetPath sourceFolder, out Maps maps, out string error)
        {
            maps = default;
            error = null;

            if (!LayeredTexturePreferences.TryGetRelativeRoot(out var root))
            {
                error = "Layered Texture relative root is missing.";
                return false;
            }

            if (sourceFolder.Mode != AssetPathMode.Relative
                || !sourceFolder.TryGetAbsolutePath(root, out var folder)
                || !Directory.Exists(folder))
            {
                error = "PBR source folder must exist under the Layered Texture relative root.";
                return false;
            }

            maps = new Maps();
            var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                if (!TextureFileLoader.IsSupportedPath(file)
                    || !AssetPath.TryMake(file, root, AssetPathMode.Relative, out var path))
                {
                    continue;
                }

                maps.Assign(Classify(file), path);
            }

            return true;
        }

        static MapKind Classify(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            var compact = name
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);

            if (ContainsAny(compact, "basecolor", "albedo", "diffuse", "diff", "color"))
                return MapKind.Color;

            if (ContainsAny(compact, "alpha", "opacity", "transparency", "cutout"))
                return MapKind.Alpha;

            if (ContainsAny(compact, "normal", "nrm", "nor"))
                return MapKind.Normal;

            if (ContainsAny(compact, "roughness", "rough"))
                return MapKind.Roughness;

            if (ContainsAny(compact, "smoothness", "smooth", "glossiness", "gloss"))
                return MapKind.Smoothness;

            if (ContainsAny(compact, "metallic", "metalness", "metal"))
                return MapKind.Metallic;

            if (ContainsAny(compact, "ambientocclusion", "occlusion", "ao"))
                return MapKind.AO;

            if (ContainsAny(compact, "displacement", "height", "disp"))
                return MapKind.Height;

            return MapKind.None;
        }

        static bool ContainsAny(string value, params string[] terms)
        {
            for (var i = 0; i < terms.Length; i++)
            {
                if (value.Contains(terms[i]))
                    return true;
            }

            return false;
        }

        internal enum MapKind
        {
            None,
            Color,
            Alpha,
            Normal,
            AO,
            Roughness,
            Smoothness,
            Metallic,
            Height
        }

        internal struct Maps
        {
            public AssetPath? Color;
            public AssetPath? Alpha;
            public AssetPath? Normal;
            public AssetPath? AO;
            public AssetPath? Roughness;
            public AssetPath? Smoothness;
            public AssetPath? Metallic;
            public AssetPath? Height;

            internal void Assign(MapKind kind, AssetPath path)
            {
                switch (kind)
                {
                    case MapKind.Color:
                        Color ??= path;
                        break;
                    case MapKind.Alpha:
                        Alpha ??= path;
                        break;
                    case MapKind.Normal:
                        Normal ??= path;
                        break;
                    case MapKind.AO:
                        AO ??= path;
                        break;
                    case MapKind.Roughness:
                        Roughness ??= path;
                        break;
                    case MapKind.Smoothness:
                        Smoothness ??= path;
                        break;
                    case MapKind.Metallic:
                        Metallic ??= path;
                        break;
                    case MapKind.Height:
                        Height ??= path;
                        break;
                }
            }
        }
    }
}
