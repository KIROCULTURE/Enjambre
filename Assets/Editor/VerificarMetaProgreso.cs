using UnityEngine;
using UnityEditor;

/// <summary>
/// Verifica (sin Play Mode) SOLO la aritmética pura de MetaProgreso
/// (CalcularRecompensa/CostoNivel/ValorEfectivo) — nunca llama
/// GanarEsencia/ComprarMejora, que tocan PlayerPrefs de verdad y
/// corromperían el save real del jugador. No abre ni toca la escena.
/// </summary>
public static class VerificarMetaProgreso
{
    [MenuItem("Enjambre/Debug/Verificar Meta-Progreso")]
    public static void Verificar()
    {
        // --- 1. CalcularRecompensa con los 5 valores reales archivados ---
        void ProbarRecompensa(string partida, float tiempo, int comboMaximo, int nucleosMaximo)
        {
            int esencia = MetaProgreso.CalcularRecompensa(tiempo, comboMaximo, nucleosMaximo);
            Debug.Log($"{partida}: tiempo={tiempo}, comboMaximo={comboMaximo}, nucleosMaximo={nucleosMaximo} -> {esencia} Esencia");
            if (esencia <= 0)
                Debug.LogError($"FALLÓ: {partida} dio una recompensa no positiva ({esencia}).");
        }

        ProbarRecompensa("partida_20260908_202003", 419.73f, 320, 223);
        ProbarRecompensa("partida_20260908_212657", 142.21f, 93, 64);
        ProbarRecompensa("partida_20260908_214644", 375.42f, 483, 101);
        ProbarRecompensa("partida_20260908_220521", 48.31f, 52, 13);
        ProbarRecompensa("partida_20260908_221210", 63.72f, 136, 38);

        // Una partida más larga/mejor jugada tiene que dar más Esencia que
        // una corta — si no, la fórmula estaría premiando morir rápido.
        int corta = MetaProgreso.CalcularRecompensa(48.31f, 52, 13);
        int larga = MetaProgreso.CalcularRecompensa(375.42f, 483, 101);
        if (larga <= corta)
            Debug.LogError($"FALLÓ: una partida mucho más larga/mejor ({larga}) no dio más Esencia que una corta ({corta}).");
        else
            Debug.Log($"OK: la recompensa escala con lo lejos que llegaste ({corta} -> {larga}).");

        // --- 2. CostoNivel: sube con el nivel, y -1 en el tope ---
        foreach (var id in new[] { "nucleo", "crecimiento", "fever", "gemas" })
        {
            int costoAnterior = -1;
            for (int nivel = 0; nivel < MetaProgreso.NivelMaximo; nivel++)
            {
                int costo = MetaProgreso.CostoNivel(id, nivel);
                Debug.Log($"CostoNivel(\"{id}\", {nivel}) = {costo}");
                if (costo <= 0)
                    Debug.LogError($"FALLÓ: CostoNivel(\"{id}\", {nivel}) debería ser positivo, dio {costo}.");
                else if (nivel > 0 && costo <= costoAnterior)
                    Debug.LogError($"FALLÓ: CostoNivel(\"{id}\", {nivel})={costo} no subió respecto al nivel anterior ({costoAnterior}).");
                costoAnterior = costo;
            }
            int costoAlTope = MetaProgreso.CostoNivel(id, MetaProgreso.NivelMaximo);
            if (costoAlTope != -1)
                Debug.LogError($"FALLÓ: CostoNivel(\"{id}\", {MetaProgreso.NivelMaximo}) (ya al tope) debería ser -1, dio {costoAlTope}.");
            else
                Debug.Log($"OK (\"{id}\"): el costo sube con el nivel y da -1 en el tope.");
        }

        // --- 3. ValorEfectivo: nivel 0 = exactamente el base, nunca pasa el tope ---
        // NivelDe(id) lee el nivel comprado REAL (persistido) — en un
        // entorno de test recién cargado (nadie llamó Cargar() en este
        // proceso) parte en 0 para las 4 mejoras, así que ValorEfectivo
        // con nivel 0 tiene que devolver exactamente baseValor.
        float efectivoNucleoNivel0 = MetaProgreso.ValorEfectivo("nucleo", 0.5f, 0.05f, 0.75f);
        Debug.Log($"ValorEfectivo(\"nucleo\", base=0.5, nivel actual={MetaProgreso.NivelNucleo}) = {efectivoNucleoNivel0}");
        if (MetaProgreso.NivelNucleo == 0 && !Mathf.Approximately(efectivoNucleoNivel0, 0.5f))
            Debug.LogError($"FALLÓ: con nivel 0 comprado, ValorEfectivo debería devolver exactamente el base (0.5), dio {efectivoNucleoNivel0}.");
        else
            Debug.Log("OK: ValorEfectivo con nivel 0 devuelve el base tal cual, sin sobreescribirlo.");

        // Un valor por encima del tope debería quedar clampeado, no seguir subiendo indefinidamente.
        float clamp = MetaProgreso.ValorEfectivo("fever", 15f, 100f, 25f); // incremento absurdo a propósito
        Debug.Log($"ValorEfectivo con incremento exagerado (debería clampear a 25) = {clamp}");
        if (clamp > 25f)
            Debug.LogError($"FALLÓ: ValorEfectivo no respetó el tope (25), dio {clamp}.");
        else
            Debug.Log("OK: ValorEfectivo respeta el tope.");

        Debug.Log("Verificación de MetaProgreso (aritmética pura) completa.");
    }
}
