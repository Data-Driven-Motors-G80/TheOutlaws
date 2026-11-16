using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Bakes portable, static SDF fonts. Gameplay never needs an installed system font.</summary>
public static class WorldShiftTypographySetup
{
    private const string Folder = "Assets/Resources/WorldShiftFonts";

    [MenuItem("The Outlaws/Art/Create Missing Font Assets")]
    public static void CreateMissingFonts()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources","WorldShiftFonts");
        var body = Create("Barlow-Medium.ttf","Barlow Medium");
        var bold = Create("Barlow-Bold.ttf","Barlow Bold");
        var heading = Create("BarlowCondensed-SemiBold.ttf","Barlow Condensed Semibold");
        body.fontWeightTable[7].regularTypeface = bold;
        var fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        body.fallbackFontAssetTable = new List<TMP_FontAsset> {fallback};
        bold.fallbackFontAssetTable = new List<TMP_FontAsset> {fallback};
        heading.fallbackFontAssetTable = new List<TMP_FontAsset> {body};
        foreach (var font in new[] {body,bold,heading}) EditorUtility.SetDirty(font);
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        var serialized = new SerializedObject(settings);
        serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = body;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("Worldshift typography: Barlow Medium / Bold and Barlow Condensed Semibold ready.");
    }

    private static TMP_FontAsset Create(string sourceName,string assetName)
    {
        string path = Folder+"/"+assetName+".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Barlow/"+sourceName);
        if (source == null) throw new InvalidOperationException("Missing typeface: "+sourceName);
        var asset = TMP_FontAsset.CreateFontAsset(source,72,7,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
        asset.name = assetName;
        string characters = new string(Enumerable.Range(32,95).Concat(Enumerable.Range(160,96))
            .Concat(new[] {0x2013,0x2014,0x2018,0x2019,0x201C,0x201D,0x2022,0x2026,0x2190,0x2191,0x2192,0x2193})
            .Select(c=>(char)c).ToArray());
        asset.TryAddCharacters(characters,out string missing,true);
        if (!string.IsNullOrEmpty(missing) && missing.Any(c=>c>=32 && c<=126))
            throw new InvalidOperationException(assetName+" could not bake the basic UI alphabet.");
        asset.normalStyle = asset.boldStyle = 0f;
        asset.material.SetFloat("_WeightNormal",0f);asset.material.SetFloat("_WeightBold",0f);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset,path);
        asset.material.name = assetName+" Material";
        AssetDatabase.AddObjectToAsset(asset.material,asset);
        foreach (var texture in asset.atlasTextures)
        {
            texture.name = assetName+" Atlas";
            AssetDatabase.AddObjectToAsset(texture,asset);
        }
        EditorUtility.SetDirty(asset);
        return asset;
    }
}
