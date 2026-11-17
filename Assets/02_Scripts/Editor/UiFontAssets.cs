using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class UiFontAssets
{
    public static TMP_FontAsset Latin => Load("Audiowide", "Audiowide/Audiowide-Regular.ttf");
    public static TMP_FontAsset Japanese => Load("ZenKakuGothicNew", "ZenKakuGothicNew/ZenKakuGothicNew-Medium.ttf");

    private static TMP_FontAsset Load(string name, string source)
    {
        const string folder = "Assets/Resources/UIFonts";
        string path = folder + "/" + name + " SDF.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) return asset;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/00_InportAssets/" + source);
        if (font == null) throw new System.InvalidOperationException("Missing UI font: " + source);
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources", "UIFonts");
        asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, true);
        asset.name = name + " SDF";
        AssetDatabase.CreateAsset(asset, path);
        foreach (var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture, asset);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    [MenuItem("Tools/GoStraight/Prepare TextMeshPro Fonts")]
    public static void Prepare()
    {
        Latin.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /:-");
        Japanese.TryAddCharacters("ジャンプ移動攻撃調べる押す開く");
        EditorUtility.SetDirty(Latin);
        EditorUtility.SetDirty(Japanese);
        AssetDatabase.SaveAssets();
    }
}
