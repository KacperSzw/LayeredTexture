using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unmanaged.LayeredTexture
{
    [CreateAssetMenu(menuName = "LayeredTexture/Texture Set Recipe")]
    public sealed class TextureSetRecipe : ScriptableObject
    {
        public List<TextureSetRecipeSlot> Recipes = new();
        public TextureSetPbrSetupSettings PbrSetup = new();
    }

    [Serializable]
    public sealed class TextureSetRecipeSlot
    {
        public bool Enabled = true;
        public string Name;
        public LayerStack RootStack = new();
        public OutputProfile Output = OutputProfile.Default;
    }

    [Serializable]
    public sealed class TextureSetPbrSetupSettings
    {
        [AssetPathPicker(AssetPathPickerKind.Folder)]
        public AssetPath SourceFolder = AssetPath.Relative();
        public bool Color = true;
        public bool PackAlpha = true;
        public bool ARM = true;
        public bool Mask;
        public bool Normal = true;
    }
}
