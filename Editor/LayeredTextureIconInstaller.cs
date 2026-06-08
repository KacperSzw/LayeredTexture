using UnityEditor;
using UnityEngine;

namespace Unmanaged.LayeredTexture.Editor
{
    [InitializeOnLoad]
    static class LayeredTextureIconInstaller
    {
        const string PackageRoot = "Packages/com.unmanaged.layered-texture";
        const string RecipeScriptPath = PackageRoot + "/Runtime/TextureRecipe.cs";
        const string ArrayScriptPath = PackageRoot + "/Runtime/LayeredTextureArray.cs";
        const string RecipeIconPath = PackageRoot + "/Editor/Resources/LayeredTexture/Icons/TextureRecipeIcon.png";
        const string ArrayIconPath = PackageRoot + "/Editor/Resources/LayeredTexture/Icons/LayeredTextureArrayIcon.png";

        static LayeredTextureIconInstaller() => EditorApplication.delayCall += ApplyIcons;

        static void ApplyIcons()
        {
            SetScriptIcon(RecipeScriptPath, RecipeIconPath);
            SetScriptIcon(ArrayScriptPath, ArrayIconPath);
        }

        static void SetScriptIcon(string scriptPath, string iconPath)
        {
            if (AssetImporter.GetAtPath(scriptPath) is not MonoImporter importer)
                return;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

            if (icon == null || importer.GetIcon() == icon)
                return;

            importer.SetIcon(icon);
            importer.SaveAndReimport();
        }
    }
}
