using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) la Forma Precisa como mecánica pura, sin
/// depender de simular teclas reales (Input no se puede tickear en batch
/// mode) — todo pasa por FormaPrecisa.Mover(direccion, enfocado, dt),
/// que es la misma función que Update() llama, solo que acá se invoca
/// directo con una dirección sintética. Confirma: sin inercia (una sola
/// llamada llega a la velocidad completa, soltar para en seco), el modo
/// enfocado mueve más lento, el clamp a los límites de la cámara, y el
/// sistema de vidas/invulnerabilidad. También captura dos instancias
/// lado a lado (normal / enfocada) para ver la diferencia real entre el
/// sprite visual y el hitbox chico. No guarda la escena.
/// </summary>
public static class VerificarFormaPrecisa
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    const float Epsilon = 0.01f;

    [MenuItem("Enjambre/Debug/Verificar Forma Precisa")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        Debug.Log($"Arena real: mitadAncho={gm.mitadAncho:F2} mitadAlto={gm.mitadAlto:F2}");

        if (gm.formaPrecisaPrefab == null) { Debug.LogError("FALLÓ: GameManager.formaPrecisaPrefab está sin asignar — correr antes 'Enjambre/Crear Prefab Forma Precisa'."); return; }

        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector3.zero, Quaternion.identity);
        Invocar(fp, "Awake");

        // --- 1. Hitbox real << sprite visual ---
        var col = fp.GetComponent<CircleCollider2D>();
        Debug.Log($"radioHitbox={fp.radioHitbox}, collider.radius={col.radius}, radioVisual={fp.radioVisual}, escala visual={fp.visual.localScale}");
        if (Mathf.Abs(col.radius - fp.radioHitbox) > 0.001f)
            Debug.LogError("FALLÓ: el collider no quedó al radio de radioHitbox.");
        else if (fp.radioHitbox >= fp.radioVisual)
            Debug.LogError("FALLÓ: el hitbox debería ser bien más chico que el sprite visual, no al revés.");
        else
            Debug.Log("OK: hitbox real bien más chico que el sprite (convención danmaku).");

        // --- 2. Sin inercia: una sola llamada llega a la velocidad completa ---
        fp.Mover(Vector2.right, false, 1f);
        float xTrasUnSegundo = fp.transform.position.x;
        Debug.Log($"Tras Mover(derecha, 1s) desde x=0: x={xTrasUnSegundo:F3} (esperado ≈{fp.velocidad:F3}, sin rampa de aceleración)");
        if (Mathf.Abs(xTrasUnSegundo - fp.velocidad) > Epsilon)
            Debug.LogError($"FALLÓ: se esperaba x≈{fp.velocidad}, dio {xTrasUnSegundo} — ¿quedó algo de inercia/aceleración?");
        else
            Debug.Log("OK: respuesta instantánea, sin rampa.");

        // --- 3. Sin dirección = frenada en seco (no sigue por inercia) ---
        fp.Mover(Vector2.zero, false, 1f);
        float xTrasSoltar = fp.transform.position.x;
        Debug.Log($"Tras Mover(sin dirección, 1s): x={xTrasSoltar:F3} (esperado igual a {xTrasUnSegundo:F3} — no debería seguir moviéndose solo)");
        if (Mathf.Abs(xTrasSoltar - xTrasUnSegundo) > Epsilon)
            Debug.LogError("FALLÓ: se siguió moviendo sin input — hay inercia residual, no debería haberla.");
        else
            Debug.Log("OK: frena en seco al soltar, cero inercia residual.");

        // --- 4. Modo enfocado mueve más lento ---
        fp.transform.position = Vector3.zero;
        fp.Mover(Vector2.right, true, 1f);
        float xEnfocado = fp.transform.position.x;
        Debug.Log($"Tras Mover(derecha, ENFOCADO, 1s): x={xEnfocado:F3} (esperado ≈{fp.velocidadEnfocado:F3})");
        if (Mathf.Abs(xEnfocado - fp.velocidadEnfocado) > Epsilon || xEnfocado >= fp.velocidad)
            Debug.LogError("FALLÓ: el modo enfocado debería mover a velocidadEnfocado, más lento que el normal.");
        else
            Debug.Log("OK: enfocado reduce la velocidad de verdad.");

        // --- 5. Clamp a los límites reales de la arena ---
        fp.transform.position = Vector3.zero;
        fp.Mover(Vector2.right, false, 1000f); // dt gigante a propósito, para forzar el clamp
        float xClampeado = fp.transform.position.x;
        float xMaximoEsperado = gm.mitadAncho - fp.radioVisual;
        Debug.Log($"Tras Mover(derecha, dt gigante): x={xClampeado:F3} (esperado clampeado a {xMaximoEsperado:F3})");
        if (Mathf.Abs(xClampeado - xMaximoEsperado) > Epsilon)
            Debug.LogError("FALLÓ: no se clampeó al límite real de la arena.");
        else
            Debug.Log("OK: no se puede salir del área jugable.");

        // --- 6. Vidas: golpe simple resta una vida, invulnerabilidad bloquea el siguiente ---
        fp.transform.position = Vector3.zero;
        int vidasIniciales = fp.vidas;
        fp.RecibirGolpe();
        Debug.Log($"Tras 1 golpe: vidas={fp.vidas} (esperado {vidasIniciales - 1})");
        fp.RecibirGolpe(); // debería ignorarse — sigue invulnerable, sin avanzar tiempo real
        Debug.Log($"Tras un 2do golpe inmediato (todavía invulnerable): vidas={fp.vidas} (esperado igual, {vidasIniciales - 1} — la invulnerabilidad debe bloquearlo)");
        if (fp.vidas != vidasIniciales - 1)
            Debug.LogError("FALLÓ: o no restó la primera vida, o la invulnerabilidad no bloqueó el golpe inmediato siguiente.");
        else
            Debug.Log("OK: un golpe resta una vida; la invulnerabilidad post-golpe bloquea el siguiente golpe inmediato.");

        // --- 7. Sin vidas dispara el evento (limpiando invulnerabilidad a mano entre golpes, simulando que pasó tiempo real) ---
        bool eventoDisparado = false;
        fp.AlQuedarSinVidas += () => eventoDisparado = true;
        var campoInvulnerable = typeof(FormaPrecisa).GetField("invulnerable", Flags);
        while (fp.vidas > 0)
        {
            campoInvulnerable.SetValue(fp, false);
            fp.RecibirGolpe();
        }
        Debug.Log($"Tras vaciar las vidas: vidas={fp.vidas}, evento AlQuedarSinVidas disparado={eventoDisparado}");
        if (fp.vidas != 0 || !eventoDisparado)
            Debug.LogError("FALLÓ: al llegar a 0 vidas debería disparar AlQuedarSinVidas.");
        else
            Debug.Log("OK: sin vidas dispara el evento que la pelea real (punto 4) va a escuchar.");

        // --- 8. Captura: normal vs. enfocada (hitbox visible) lado a lado ---
        Object.DestroyImmediate(fp.gameObject);
        var normal = Object.Instantiate(gm.formaPrecisaPrefab, new Vector3(-1.3f, 0f, 0f), Quaternion.identity);
        Invocar(normal, "Awake");
        normal.Mover(Vector2.zero, false, 0f);
        var campoActualizarVisual = typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags);
        campoActualizarVisual.Invoke(normal, new object[] { false });

        var enfocada = Object.Instantiate(gm.formaPrecisaPrefab, new Vector3(1.3f, 0f, 0f), Quaternion.identity);
        Invocar(enfocada, "Awake");
        campoActualizarVisual.Invoke(enfocada, new object[] { true });

        // La captura es de las dos instancias solas en la arena — sin el
        // Canvas del menú tapándolas (arranca activo en la escena guardada).
        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);
        if (gm.hudRoot != null) gm.hudRoot.SetActive(false);

        int w = 960, h = 540;
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

        var dir = Path.Combine(Application.dataPath, "..", "..", "capturas");
        Directory.CreateDirectory(dir);
        var ruta = Path.Combine(dir, "forma_precisa.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("VerificarFormaPrecisa: captura guardada en " + ruta);

        // Segunda captura, con la cámara acercada sobre la instancia
        // enfocada — el punto del hitbox real (radioHitbox, chico a
        // propósito) se ve mejor de cerca. Desde Fase 2 el punto queda
        // visible SIEMPRE (ver FormaPrecisa.ActualizarVisual); enfocado
        // ahora solo lo agranda un poco, que es justo lo que esta captura
        // deja ver comparada con "normal.png".
        bool eraOrtografica = cam.orthographic;
        float sizeOriginal = cam.orthographicSize;
        Vector3 posOriginal = cam.transform.position;
        cam.orthographic = true;
        cam.orthographicSize = 0.5f;
        cam.transform.position = new Vector3(enfocada.transform.position.x, enfocada.transform.position.y, posOriginal.z);

        var rt2 = new RenderTexture(w, h, 24);
        cam.targetTexture = rt2;
        cam.Render();
        RenderTexture.active = rt2;
        var tex2 = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex2.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex2.Apply();
        cam.targetTexture = prevRT;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(rt2);

        cam.orthographic = eraOrtografica;
        cam.orthographicSize = sizeOriginal;
        cam.transform.position = posOriginal;

        var ruta2 = Path.Combine(dir, "forma_precisa_hitbox_zoom.png");
        File.WriteAllBytes(ruta2, tex2.EncodeToPNG());
        Object.DestroyImmediate(tex2);
        Debug.Log("VerificarFormaPrecisa: captura de zoom guardada en " + ruta2);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
