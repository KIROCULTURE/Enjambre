using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Reproduce el bug reportado: recolectar el mismo orbe con dos núcleos en
/// el mismo frame agendaba dos reemplazos, y con el tiempo (más núcleos =
/// más choques simultáneos) la cantidad de orbes crecía sin límite hasta
/// crashear. Prueba 1: dispara el doble-golpe directo sobre el mismo orbe.
/// Prueba 2: deja correr ~22s reales de juego (forzando choques cada tanto
/// para generar más núcleos) y registra la cantidad de orbes en el tiempo.
/// </summary>
public static class OrbeLeakVerifier
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static int fase = 0;
    static float faseInicio, verifInicio, ultimoLog;

    static bool Pasaron(float s) => Time.realtimeSinceStartup - faseInicio > s;
    static void CambiarFase(int n) { fase = n; faseInicio = Time.realtimeSinceStartup; }

    [MenuItem("Enjambre/Verificar fuga de orbes")]
    public static void Verificar()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        verifInicio = Time.realtimeSinceStartup;
        ultimoLog = 0;
        CambiarFase(0);
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Salir(int codigo)
    {
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(codigo);
        // Revertir el ajuste de Enter Play Mode al salir (mismo criterio que PlayModeVerifier).
        EditorSettings.enterPlayModeOptionsEnabled = false;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (Time.realtimeSinceStartup - verifInicio > 60f)
        {
            Debug.LogError("VERIF-ORBE: se agotó el límite de 60s reales (fase=" + fase + "). Forzando salida.");
            Salir(1);
            return;
        }
        var gm = GameManager.Instancia;

        switch (fase)
        {
            case 0:
                if (Pasaron(0.3f)) { gm.BotonJugar(); CambiarFase(1); }
                break;

            case 1: // prueba directa: doble-golpe manual sobre el mismo orbe
                if (Pasaron(0.3f))
                {
                    var n = Object.FindFirstObjectByType<Nucleo>();
                    var o = Object.FindFirstObjectByType<Orbe>();
                    int antes = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Length;
                    gm.ProcesarPickupOrbe(n, o);
                    gm.ProcesarPickupOrbe(n, o); // mismo orbe, segunda vez en el mismo frame
                    Debug.Log("VERIF-ORBE: doble-golpe manual sobre el mismo orbe -> orbes antes=" + antes + " (el Invoke de reemplazo tarda 0.4s, se confirma en fase 2)");
                    CambiarFase(2);
                }
                break;

            case 2: // esperar los 0.4s del Invoke y confirmar que solo se generó 1 reemplazo, no 2
                if (Pasaron(0.7f))
                {
                    int ahora = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Length;
                    Debug.Log("VERIF-ORBE: tras el doble-golpe y 0.7s -> orbes ahora=" + ahora + " (esperado 5, un solo reemplazo agendado pese a llamar 2 veces)");
                    CambiarFase(3);
                }
                break;

            case 3: // dejar correr el juego ~22s reales, forzando choques cada tanto para generar más núcleos,
                     // y loguear la cantidad de orbes cada 2s para confirmar que no crece sin límite
                {
                    if (Time.realtimeSinceStartup - faseInicio - ultimoLog > 2f)
                    {
                        ultimoLog = Time.realtimeSinceStartup - faseInicio;
                        var nucleos = Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None);
                        var orbes = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None);
                        Debug.Log("VERIF-ORBE: t=" + ultimoLog.ToString("F1") + "s nucleos=" + nucleos.Length + " orbes=" + orbes.Length);

                        // forzar un choque para ir generando más núcleos con el tiempo
                        var e = Object.FindFirstObjectByType<Enemigo>();
                        if (e != null && nucleos.Length > 0) e.transform.position = nucleos[0].transform.position;

                        // forzar pickups masivos: mandar todos los núcleos actuales sobre orbes existentes
                        for (int i = 0; i < nucleos.Length && i < orbes.Length; i++)
                            nucleos[i].transform.position = orbes[i % orbes.Length].transform.position;
                    }

                    if (Pasaron(8f)) CambiarFase(4);
                }
                break;

            case 4:
                {
                    var orbes = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None);
                    var nucleos = Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None);
                    Debug.Log("VERIF-ORBE: FINAL tras ~22s -> nucleos=" + nucleos.Length + " orbes=" + orbes.Length + " (esperado orbes cerca de orbesObjetivo=5, no cientos)");
                    Salir(0);
                }
                break;
        }
    }
}
