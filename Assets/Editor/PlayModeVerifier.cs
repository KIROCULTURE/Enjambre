using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verificación headless del loop completo: entra a Play, pasa del menú a
/// jugar, fuerza una recolección de orbe y un choque contra enemigo
/// reposicionando objetos reales (para que Physics2D dispare los
/// OnTrigger de verdad, no simulado a mano), y saca capturas de pantalla
/// en los momentos clave. Los tiempos de espera usan reloj real
/// (Time.realtimeSinceStartup), no conteo de frames: en modo batch la
/// cantidad de Update() por segundo varía mucho según la máquina, así
/// que contar frames daba esperas poco confiables.
/// </summary>
public static class PlayModeVerifier
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static int fase = 0;
    static string dirCapturas;
    static float faseInicio;
    static float verifInicio;
    static float radioAntesDePickup;

    static bool Pasaron(float segundosReales) => Time.realtimeSinceStartup - faseInicio > segundosReales;

    static void CambiarFase(int nueva)
    {
        fase = nueva;
        faseInicio = Time.realtimeSinceStartup;
    }

    [MenuItem("Enjambre/Verificar Loop (Play headless)")]
    public static void Verificar()
    {
        dirCapturas = Path.Combine(Application.dataPath, "..", "..", "capturas");
        Directory.CreateDirectory(dirCapturas);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Entrar a Play recarga el dominio de C# por defecto, lo que borra
        // cualquier suscripción a EditorApplication.update hecha antes de
        // este punto (así se quedó colgado el primer intento: el Tick()
        // nunca se volvía a llamar). Lo desactivamos para esta verificación.
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        verifInicio = Time.realtimeSinceStartup;
        CambiarFase(0);
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Capturar(string nombre)
    {
        var cam = Camera.main;
        int w = 800, h = 600;
        var rt = new RenderTexture(w, h, 24);
        var prevRT = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = prevRT;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(rt);

        // Nota: los Canvas Screen Space Overlay no pasan por Camera.Render(),
        // así que esta captura muestra el mundo (núcleos/enemigos/orbes/cámara)
        // pero no el HUD ni los paneles de UI.
        var bytes = tex.EncodeToPNG();
        var ruta = Path.Combine(dirCapturas, nombre + ".png");
        File.WriteAllBytes(ruta, bytes);
        Object.DestroyImmediate(tex);
        Debug.Log("PlayModeVerifier: captura guardada en " + ruta);
    }

    static void Salir(int codigo)
    {
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(codigo);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;

        if (Time.realtimeSinceStartup - verifInicio > 60f)
        {
            Debug.LogError("VERIF: se agotó el límite de 60s reales sin terminar (fase=" + fase + "). Forzando salida.");
            Salir(1);
            return;
        }
        var gm = GameManager.Instancia;

        switch (fase)
        {
            case 0: // esperar a que arranque el menú
                if (Pasaron(0.3f))
                {
                    Debug.Log("VERIF: estado tras arrancar = " + gm.estado + " (esperado Menu)");
                    Capturar("01_menu");
                    gm.BotonJugar();
                    CambiarFase(1);
                }
                break;

            case 1: // confirmar que empezó a jugar y hay 1 nucleo
                if (Pasaron(0.3f))
                {
                    var nucleos = Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None);
                    Debug.Log("VERIF: estado=" + gm.estado + " nucleos=" + nucleos.Length + " (esperado Jugando, 1)");
                    Capturar("02_jugando_inicio");
                    CambiarFase(2);
                }
                break;

            case 2: // forzar recolección de orbe: teletransportar el nucleo sobre un orbe
                {
                    var n = Object.FindFirstObjectByType<Nucleo>();
                    var o = Object.FindFirstObjectByType<Orbe>();
                    if (n != null && o != null)
                    {
                        radioAntesDePickup = n.radio;
                        n.transform.position = o.transform.position;
                        CambiarFase(3);
                    }
                    else
                    {
                        Debug.LogError("VERIF: no encontré Nucleo u Orbe para probar el pickup");
                        CambiarFase(4);
                    }
                }
                break;

            case 3: // reafirmar la posición cada frame (por si Nucleo.Update lo aleja) y esperar la física
                {
                    var n = Object.FindFirstObjectByType<Nucleo>();
                    var o = Object.FindFirstObjectByType<Orbe>();
                    if (n != null && o != null) n.transform.position = o.transform.position;

                    if (Pasaron(0.5f))
                    {
                        var orbes = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None);
                        Debug.Log("VERIF: pickup orbe -> radio antes=" + radioAntesDePickup + " radio ahora=" + (n != null ? n.radio.ToString() : "null(destruido? no debería)") +
                            " orbes en escena=" + orbes.Length + " (esperado radio mayor, orbes=4 o 5 si ya se regeneró)");
                        CambiarFase(4);
                    }
                }
                break;

            case 4: // esperar a que nazca un enemigo (telegraph + spawn) y forzar el choque
                {
                    var e = Object.FindFirstObjectByType<Enemigo>();
                    if (e != null)
                    {
                        var n = Object.FindFirstObjectByType<Nucleo>();
                        n.transform.position = e.transform.position;
                        Debug.Log("VERIF: enemigo apareció con gm.Tiempo=" + gm.Tiempo + "s, forzando choque");
                        CambiarFase(5);
                    }
                    else if (Pasaron(4f))
                    {
                        Debug.LogError("VERIF: no apareció ningún enemigo tras 4s reales, gm.Tiempo=" + gm.Tiempo + "s (intervaloEnemigoInicial=1.6s + telegraph=0.4s deberían alcanzar)");
                        CambiarFase(6);
                    }
                }
                break;

            case 5: // mantenerlo encima y comprobar que se partió en dos
                {
                    var n = Object.FindFirstObjectByType<Nucleo>();
                    var e = Object.FindFirstObjectByType<Enemigo>();
                    if (n != null && e != null) n.transform.position = e.transform.position;

                    if (Pasaron(0.5f))
                    {
                        var nucleos = Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None);
                        Debug.Log("VERIF: tras choque con enemigo -> nucleos ahora=" + nucleos.Length + " (esperado 2, partido en dos)");
                        Capturar("03_tras_choque");
                        CambiarFase(6);
                    }
                }
                break;

            case 6: // destruir todos los nucleos para forzar fin de partida
                {
                    foreach (var n in Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None))
                        Object.Destroy(n.gameObject);
                    CambiarFase(7);
                }
                break;

            case 7:
                if (Pasaron(0.3f))
                {
                    Debug.Log("VERIF: estado tras destruir todos los núcleos = " + gm.estado + " (esperado Fin)");
                    Capturar("04_fin");
                    CambiarFase(8);
                }
                break;

            case 8:
                Salir(0);
                break;
        }
    }
}
