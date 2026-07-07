using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    public static class TextureSetPbrSetupUtility
    {
        public static bool Setup(TextureSetRecipe set, out string message)
        {
            message = null;

            if (set == null)
            {
                message = "TextureSetRecipe is missing.";
                return false;
            }

            if (!TextureSetPbrTextureGatherer.TryGather(set.PbrSetup.SourceFolder, out var maps, out message))
                return false;

            set.Recipes ??= new List<TextureSetRecipeSlot>();

            if (set.PbrSetup.Color)
                FillColor(GetOrCreateSlot(set, "Color"), maps, set.PbrSetup.PackAlpha);

            if (set.PbrSetup.ARM)
                FillArm(GetOrCreateSlot(set, "ARM"), maps);

            if (set.PbrSetup.Mask)
                FillMask(GetOrCreateSlot(set, "Mask"), maps);

            if (set.PbrSetup.Normal)
                FillNormal(GetOrCreateSlot(set, "Normal"), maps);

            message = "PBR setup finished.";
            return true;
        }

        static TextureSetRecipeSlot GetOrCreateSlot(TextureSetRecipe set, string name)
        {
            for (var i = 0; i < set.Recipes.Count; i++)
            {
                var slot = set.Recipes[i];

                if (slot != null && string.Equals(slot.Name, name, StringComparison.OrdinalIgnoreCase))
                    return slot;
            }

            var created = new TextureSetRecipeSlot
            {
                Name = name
            };
            set.Recipes.Add(created);
            return created;
        }

        static void FillColor(TextureSetRecipeSlot slot, TextureSetPbrTextureGatherer.Maps maps, bool packAlpha)
        {
            if (!CanFill(slot))
                return;

            var layers = slot.RootStack.Layers;
            layers.Add(Solid(new Color(1f, 1f, 1f, 1f)));

            if (maps.Color.HasValue)
                layers.Add(TextureLayer(maps.Color.Value, ChannelWriteMask.RGB, ChannelSwizzle.Identity));

            if (!packAlpha)
                return;

            if (maps.Alpha.HasValue)
                layers.Add(TextureLayer(maps.Alpha.Value, ChannelWriteMask.A, ScalarSwizzle(SwizzleChannelSource.R)));
            else if (maps.Color.HasValue)
                layers.Add(TextureLayer(maps.Color.Value, ChannelWriteMask.A, ScalarSwizzle(SwizzleChannelSource.A)));
        }

        static void FillArm(TextureSetRecipeSlot slot, TextureSetPbrTextureGatherer.Maps maps)
        {
            if (!CanFill(slot))
                return;

            var layers = slot.RootStack.Layers;
            layers.Add(Solid(new Color(1f, 1f, 0f, 1f)));

            if (maps.AO.HasValue)
                layers.Add(TextureLayer(maps.AO.Value, ChannelWriteMask.R, ScalarSwizzle(SwizzleChannelSource.R)));

            if (maps.Roughness.HasValue)
                layers.Add(TextureLayer(maps.Roughness.Value, ChannelWriteMask.G, ScalarSwizzle(SwizzleChannelSource.R)));
            else if (maps.Smoothness.HasValue)
            {
                layers.Add(TextureLayer(maps.Smoothness.Value, ChannelWriteMask.G, ScalarSwizzle(SwizzleChannelSource.R)));
                layers.Add(new InvertLayer
                {
                    WriteMask = ChannelWriteMask.G
                });
            }

            if (maps.Metallic.HasValue)
                layers.Add(TextureLayer(maps.Metallic.Value, ChannelWriteMask.B, ScalarSwizzle(SwizzleChannelSource.R)));

            if (maps.Height.HasValue)
                layers.Add(TextureLayer(maps.Height.Value, ChannelWriteMask.A, ScalarSwizzle(SwizzleChannelSource.R)));
        }

        static void FillMask(TextureSetRecipeSlot slot, TextureSetPbrTextureGatherer.Maps maps)
        {
            if (!CanFill(slot))
                return;

            var layers = slot.RootStack.Layers;
            layers.Add(Solid(new Color(0f, 1f, 0f, 1f)));

            if (maps.Metallic.HasValue)
                layers.Add(TextureLayer(maps.Metallic.Value, ChannelWriteMask.R, ScalarSwizzle(SwizzleChannelSource.R)));

            if (maps.AO.HasValue)
                layers.Add(TextureLayer(maps.AO.Value, ChannelWriteMask.G, ScalarSwizzle(SwizzleChannelSource.R)));

            if (maps.Height.HasValue)
                layers.Add(TextureLayer(maps.Height.Value, ChannelWriteMask.B, ScalarSwizzle(SwizzleChannelSource.R)));

            if (maps.Smoothness.HasValue)
                layers.Add(TextureLayer(maps.Smoothness.Value, ChannelWriteMask.A, ScalarSwizzle(SwizzleChannelSource.R)));
            else if (maps.Roughness.HasValue)
            {
                layers.Add(TextureLayer(maps.Roughness.Value, ChannelWriteMask.A, ScalarSwizzle(SwizzleChannelSource.R)));
                layers.Add(new InvertLayer
                {
                    WriteMask = ChannelWriteMask.A
                });
            }
        }

        static void FillNormal(TextureSetRecipeSlot slot, TextureSetPbrTextureGatherer.Maps maps)
        {
            if (!CanFill(slot))
                return;

            var layers = slot.RootStack.Layers;

            if (maps.Normal.HasValue)
            {
                layers.Add(Solid(new Color(0.5f, 0.5f, 1f, 1f)));
                layers.Add(TextureLayer(maps.Normal.Value, ChannelWriteMask.RGB, ChannelSwizzle.Identity));
                return;
            }

            if (maps.Height.HasValue)
            {
                layers.Add(Solid(new Color(0f, 0f, 0f, 1f)));
                layers.Add(TextureLayer(maps.Height.Value, ChannelWriteMask.R, ScalarSwizzle(SwizzleChannelSource.R)));
                layers.Add(new NormalFromHeightLayer());
                return;
            }

            layers.Add(Solid(new Color(0.5f, 0.5f, 1f, 1f)));
        }

        static bool CanFill(TextureSetRecipeSlot slot)
        {
            slot.RootStack ??= new LayerStack();
            slot.RootStack.Layers ??= new List<TextureLayerBase>();
            return slot.RootStack.Layers.Count == 0;
        }

        static SolidColorLayer Solid(Color color) => new()
        {
            Color = color
        };

        static TextureFileLayer TextureLayer(AssetPath path, ChannelWriteMask writeMask, ChannelSwizzle swizzle) => new()
        {
            Source = new TextureSource
            {
                Kind = TextureSourceKind.File,
                Path = path
            },
            WriteMask = writeMask,
            InputSwizzle = swizzle
        };

        static ChannelSwizzle ScalarSwizzle(SwizzleChannelSource channel) => new()
        {
            R = channel,
            G = channel,
            B = channel,
            A = channel
        };

    }
}
