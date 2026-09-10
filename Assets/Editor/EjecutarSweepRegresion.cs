using UnityEditor;
using UnityEngine;

/// <summary>
/// Corre todos los Verificar*.cs de punta a punta en una sola sesión de
/// batch mode (evita reabrir el Editor 15 veces) — herramienta temporal
/// para este barrido, no parte del set de pruebas permanente del
/// proyecto (cada Verificar*.cs sigue siendo invocable solo desde su
/// propio menú "Enjambre/Debug/...").
/// </summary>
public static class EjecutarSweepRegresion
{
    [MenuItem("Enjambre/Debug/Ejecutar Barrido Completo")]
    public static void Ejecutar()
    {
        Correr("VerificarFeverTemporal", VerificarFeverTemporal.Verificar);
        Correr("VerificarClustersYExplosivo", VerificarClustersYExplosivo.Verificar);
        Correr("VerificarRendimientoSeparacion", VerificarRendimientoSeparacion.Verificar);
        Correr("VerificarSeparacionNucleos", VerificarSeparacionNucleos.Verificar);
        Correr("VerificarTelemetriaYLaserPorTiempo", VerificarTelemetriaYLaserPorTiempo.Verificar);
        Correr("VerificarEscaladaPorTamano", VerificarEscaladaPorTamano.Verificar);
        Correr("VerificarEscaladaGlobal", VerificarEscaladaGlobal.Verificar);
        Correr("VerificarEnemigosNuevos", VerificarEnemigosNuevos.Verificar);
        Correr("VerificarMetaProgreso", VerificarMetaProgreso.Verificar);
        Correr("VerificarMurallaLaser", VerificarMurallaLaser.Verificar);
        Correr("VerificarYCapturarVictoria", VerificarYCapturarVictoria.Verificar);
        Correr("VerificarFormaPrecisa", VerificarFormaPrecisa.Verificar);
        Correr("VerificarNivel2", VerificarNivel2.Verificar);
        Correr("VerificarCutsceneApertura", VerificarCutsceneApertura.Verificar);
        Correr("VerificarPeleaNivel2", VerificarPeleaNivel2.Verificar);
        Correr("VerificarOrbesEnergiaNivel2", VerificarOrbesEnergiaNivel2.Verificar);
        Correr("VerificarVidaBossNivel2", VerificarVidaBossNivel2.Verificar);
        Correr("VerificarComboPulsoPotenteNivel2", VerificarComboPulsoPotenteNivel2.Verificar);
        Correr("VerificarEscaladaPrivilegiosNivel2", VerificarEscaladaPrivilegiosNivel2.Verificar);
        Correr("VerificarFase2LaseresNivel2", VerificarFase2LaseresNivel2.Verificar);
        Correr("VerificarTelemetriaPatronesNivel2", VerificarTelemetriaPatronesNivel2.Verificar);
        Correr("VerificarFase2PatronesPropios", VerificarFase2PatronesPropios.Verificar);

        Debug.Log("===== BARRIDO COMPLETO TERMINADO =====");
    }

    static void Correr(string nombre, System.Action metodo)
    {
        Debug.Log($"===== {nombre} — INICIO =====");
        try { metodo(); }
        catch (System.Exception e) { Debug.LogError($"EXCEPCIÓN en {nombre}: {e}"); }
        Debug.Log($"===== {nombre} — FIN =====");
    }
}
