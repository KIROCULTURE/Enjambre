using UnityEngine;
using UnityEditor;

/// <summary>
/// Configura los PNG provistos por el usuario como Sprite y los conecta al
/// prefab del núcleo del jugador (Nucleo.spriteJugador / spriteGolpeado),
/// reemplazando la forma procedural. Reejecutable: solo reimporta y reasigna.
/// </summary>
public static class AplicarSpriteNucleo
{
    const string RutaSprite = "Assets/Sprites/NucleoJugador.png";
    const string RutaSpriteGolpeado = "Assets/Sprites/NucleoJugadorGolpeado.png";
    const string RutaPrefab = "Assets/Prefabs/Nucleo.prefab";

    [MenuItem("Enjambre/Aplicar Sprite de Núcleo")]
    public static void Aplicar()
    {
        var sprite = ImportarComoSprite(RutaSprite);
        var spriteGolpeado = ImportarComoSprite(RutaSpriteGolpeado);
        if (sprite == null || spriteGolpeado == null) return;

        var prefabRoot = PrefabUtility.LoadPrefabContents(RutaPrefab);
        var nucleo = prefabRoot.GetComponent<Nucleo>();
        if (nucleo == null)
        {
            Debug.LogError("AplicarSpriteNucleo: el prefab no tiene componente Nucleo.");
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return;
        }

        nucleo.spriteJugador = sprite;
        nucleo.spriteGolpeado = spriteGolpeado;

        // Si ya había un sprite generado por ShapeFactory asignado a mano
        // en el Visual, lo limpiamos: Awake() solo asigna spriteJugador si
        // sr.sprite está en null.
        var visual = prefabRoot.transform.Find("Visual");
        var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (sr != null) sr.sprite = null;

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, RutaPrefab);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("AplicarSpriteNucleo: listo.");
    }

    static Sprite ImportarComoSprite(string ruta)
    {
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("AplicarSpriteNucleo: no encontré el importer de " + ruta);
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        // Ancho real de la imagen (850px) = 1 unidad de mundo, mismo
        // criterio "1 unidad" que usan las formas proceduales (Sprite.Create
        // con pixelsPerUnit = tamaño de la textura).
        importer.spritePixelsPerUnit = 850;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (sprite == null) Debug.LogError("AplicarSpriteNucleo: no pude cargar el Sprite tras reimportar " + ruta);
        return sprite;
    }
}
