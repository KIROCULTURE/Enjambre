using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>Guarda una textura grande de cada forma generada para poder mirarla sin entrar a Play.</summary>
public static class PreviewShapes
{
    [MenuItem("Enjambre/Previsualizar Formas")]
    public static void Previsualizar()
    {
        var dir = Path.Combine(Application.dataPath, "..", "..", "capturas");
        Directory.CreateDirectory(dir);

        var texRayo = ShapeFactory.Rayo(256).texture;
        var rutaRayo = Path.Combine(dir, "rayo_preview.png");
        File.WriteAllBytes(rutaRayo, texRayo.EncodeToPNG());
        Debug.Log("PreviewShapes: guardado en " + rutaRayo);

        var texEstrella = ShapeFactory.Estrella(256, 5).texture;
        File.WriteAllBytes(Path.Combine(dir, "estrella_preview.png"), texEstrella.EncodeToPNG());

        var texDiamante = ShapeFactory.Diamante(256).texture;
        File.WriteAllBytes(Path.Combine(dir, "diamante_preview.png"), texDiamante.EncodeToPNG());
        Debug.Log("PreviewShapes: listo");
    }
}
