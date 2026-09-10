using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Genera TMP Font Assets (SDF, atlas dinámico) a partir de los .ttf de
/// Bungee y Space Mono descargados en Assets/TextMesh Pro/Fonts. Usa
/// AtlasPopulationMode.Dynamic: no hace falta "hornear" el atlas a mano
/// en el Font Asset Creator, los glifos se agregan la primera vez que se
/// usan en tiempo de ejecución/editor.
/// </summary>
public static class FontAssetCreator
{
    const string CarpetaFuentes = "Assets/TextMesh Pro/Fonts";
    const string CarpetaDestino = "Assets/TextMesh Pro/Resources/Fonts & Materials";

    [MenuItem("Enjambre/Generar Font Assets (Bungee y Space Mono)")]
    public static void Generar()
    {
        Crear("Bungee-Regular", "Bungee SDF", 90, 1024);
        Crear("SpaceMono-Regular", "SpaceMono-Regular SDF", 70, 1024);
        Crear("SpaceMono-Bold", "SpaceMono-Bold SDF", 70, 1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FontAssetCreator: listo.");
    }

    [MenuItem("Enjambre/Generar Font Assets Glitch (Rubik Glitch y Share Tech Mono)")]
    public static void GenerarGlitch()
    {
        Crear("RubikGlitch-Regular", "RubikGlitch SDF", 90, 1024);
        Crear("ShareTechMono-Regular", "ShareTechMono SDF", 70, 1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FontAssetCreator: listo (glitch).");
    }

    [MenuItem("Enjambre/Generar Font Asset Bangers")]
    public static void GenerarBangers()
    {
        Crear("Bangers-Regular", "Bangers SDF", 90, 1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FontAssetCreator: listo (Bangers).");
    }

    [MenuItem("Enjambre/Generar Font Asset Pirata One")]
    public static void GenerarPirataOne()
    {
        Crear("PirataOne-Regular", "PirataOne SDF", 90, 1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FontAssetCreator: listo (Pirata One).");
    }

    // Pirata One y Bangers son Google Fonts — se reemplazan por fuentes de
    // otra procedencia (dafont.com / 1001fonts.com, ambas 100% gratis para
    // uso comercial, ver Créditos) para que el juego no dependa de esa
    // fuente específica de assets.
    [MenuItem("Enjambre/Generar Font Asset English Towne")]
    public static void GenerarEnglishTowne()
    {
        Crear("EnglishTowne", "EnglishTowne SDF", 90, 1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FontAssetCreator: listo (English Towne).");
    }

    [MenuItem("Enjambre/Generar Font Asset Homoarakhn")]
    public static void GenerarHomoarakhn()
    {
        Crear("Homoarakhn", "Homoarakhn SDF", 90, 1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FontAssetCreator: listo (Homoarakhn).");
    }

    static void Crear(string nombreTtf, string nombreAsset, int samplingPointSize, int atlasSize)
    {
        string rutaTtf = $"{CarpetaFuentes}/{nombreTtf}.ttf";
        var font = AssetDatabase.LoadAssetAtPath<Font>(rutaTtf);
        if (font == null)
        {
            Debug.LogError("FontAssetCreator: no encontré el .ttf en " + rutaTtf);
            return;
        }

        string rutaDestino = $"{CarpetaDestino}/{nombreAsset}.asset";
        if (File.Exists(rutaDestino))
        {
            Debug.Log("FontAssetCreator: ya existe " + rutaDestino + ", lo salteo (borralo a mano si querés regenerarlo).");
            return;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(font, samplingPointSize, 9,
            GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);

        if (fontAsset == null)
        {
            Debug.LogError("FontAssetCreator: CreateFontAsset devolvió null para " + nombreTtf);
            return;
        }

        AssetDatabase.CreateAsset(fontAsset, rutaDestino);
        if (fontAsset.atlasTextures != null)
            foreach (var tex in fontAsset.atlasTextures)
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, fontAsset);
        if (fontAsset.material != null)
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        Debug.Log("FontAssetCreator: creado " + rutaDestino);
    }
}
