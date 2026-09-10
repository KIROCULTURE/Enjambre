using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) los 3 enemigos nuevos que reemplazan los
/// patrones de Fever/rayos aleatorios (bullet hell) por densidad/variedad
/// survivor-like: Cazador (persigue al núcleo más aislado, no al centro de
/// masa), Estatico/Coágulo (no se mueve, obstáculo) y Rival (persigue la
/// gema más cercana y la roba). Como en batch mode no hay Play Mode,
/// Time.deltaTime no es confiable para simular movimiento frame a frame —
/// se verifica estado/targeting vía reflexión en vez de tickear Update()
/// varias veces. No guarda la escena.
/// </summary>
public static class VerificarEnemigosNuevos
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Enemigos Nuevos")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // --- 1. NucleoMasAislado + Cazador ---
        // Un cluster de 3 núcleos cerca del origen + 1 lejos: el centro de
        // masa queda tirado hacia el cluster, así el lejano es
        // inequívocamente el más aislado (con solo 2 núcleos de igual masa
        // el centro cae justo en el medio y quedan empatados en distancia).
        Nucleo NuevoNucleo(Vector3 pos)
        {
            var n = Object.Instantiate(gm.nucleoPrefab, pos, Quaternion.identity);
            n.radio = 0.5f;
            Invocar(n, "Awake");
            Invocar(n, "OnEnable");
            return n;
        }
        NuevoNucleo(new Vector3(0f, 0f, 0f));
        NuevoNucleo(new Vector3(0.2f, 0f, 0f));
        NuevoNucleo(new Vector3(-0.2f, 0f, 0f));
        var nucleoLejano = NuevoNucleo(new Vector3(6f, 0f, 0f));

        var masAislado = gm.NucleoMasAislado();
        Debug.Log($"NucleoMasAislado() -> {(masAislado == nucleoLejano ? "el núcleo lejano" : "OTRO (mal)")}");
        if (masAislado != nucleoLejano)
            Debug.LogError("FALLÓ: NucleoMasAislado() no devolvió el núcleo más lejos del centro de masa.");
        else
            Debug.Log("OK: NucleoMasAislado() identifica correctamente al rezagado.");

        var cazador = Object.Instantiate(gm.enemigoPrefab, new Vector3(-6f, 0f, 0f), Quaternion.identity);
        Invocar(cazador, "Awake");
        float velocidadBaseCazador = cazador.velocidad;
        cazador.Inicializar(1f);
        cazador.ConfigurarCazador(nucleoLejano);

        var campoTipo = typeof(Enemigo).GetField("tipoAtaque", Flags);
        var campoObjetivoCazador = typeof(Enemigo).GetField("objetivoCazador", Flags);
        var tipoCazador = campoTipo.GetValue(cazador);
        var objetivoDelCazador = campoObjetivoCazador.GetValue(cazador);

        Debug.Log($"Cazador: tipoAtaque={tipoCazador}, objetivo={(objetivoDelCazador == nucleoLejano ? "núcleo lejano (correcto)" : "OTRO (mal)")}, velocidad {cazador.velocidad:F2} (base ~{velocidadBaseCazador:F2})");
        if (tipoCazador.ToString() != "Cazador")
            Debug.LogError("FALLÓ: ConfigurarCazador no dejó tipoAtaque en Cazador.");
        else if (objetivoDelCazador != nucleoLejano)
            Debug.LogError("FALLÓ: el Cazador no quedó apuntando al núcleo más aislado.");
        else if (cazador.velocidad <= velocidadBaseCazador)
            Debug.LogError("FALLÓ: el Cazador debería ser más rápido que un enemigo homing normal.");
        else
            Debug.Log("OK: Cazador configurado — apunta al rezagado, no al centro de masa, y es más rápido.");

        // --- 2. Estatico (Coágulo): no se mueve ---
        var coagulo = Object.Instantiate(gm.enemigoPrefab, new Vector3(2f, 2f, 0f), Quaternion.identity);
        Invocar(coagulo, "Awake");
        coagulo.Inicializar(1f);
        coagulo.ConfigurarEstatico();
        Vector3 posAntes = coagulo.transform.position;
        Invocar(coagulo, "Update"); // aunque Time.deltaTime sea 0 en batch mode, velocidad=0 hace el resultado determinista
        Vector3 posDespues = coagulo.transform.position;

        Debug.Log($"Coágulo: velocidad={coagulo.velocidad:F2} (esperado 0), radio={coagulo.radio:F2} (esperado 1.0-1.5), posición antes={posAntes} después={posDespues}");
        if (coagulo.velocidad != 0f)
            Debug.LogError("FALLÓ: ConfigurarEstatico debería dejar velocidad en 0.");
        else if (coagulo.radio < 1f || coagulo.radio > 1.5f)
            Debug.LogError($"FALLÓ: el radio del Coágulo ({coagulo.radio:F2}) está fuera del rango esperado 1.0-1.5.");
        else if (posAntes != posDespues)
            Debug.LogError("FALLÓ: el Coágulo se movió pese a tener velocidad 0.");
        else
            Debug.Log("OK: el Coágulo no se mueve y es notablemente más grande que un enemigo normal.");

        // --- 3. Rival: apunta a la gema más cercana y la roba al tocarla ---
        var orbeLejano = Object.Instantiate(gm.orbePrefab, new Vector3(10f, 0f, 0f), Quaternion.identity);
        Invocar(orbeLejano, "Awake");
        var orbeCercano = Object.Instantiate(gm.orbePrefab, new Vector3(1f, 0f, 0f), Quaternion.identity);
        Invocar(orbeCercano, "Awake");

        var rival = Object.Instantiate(gm.enemigoPrefab, new Vector3(0.5f, 0f, 0f), Quaternion.identity);
        Invocar(rival, "Awake");
        rival.Inicializar(1f);
        rival.ConfigurarRival();

        var campoObjetivoRival = typeof(Enemigo).GetField("objetivoRival", Flags);
        var objetivoDelRival = campoObjetivoRival.GetValue(rival);
        Debug.Log($"Rival: objetivo={(objetivoDelRival == orbeCercano ? "gema cercana (correcto)" : "OTRA (mal)")}");
        if (objetivoDelRival != (object)orbeCercano)
            Debug.LogError("FALLÓ: el Rival no quedó apuntando a la gema más cercana.");
        else
            Debug.Log("OK: el Rival apunta a la gema más cercana, no al enjambre.");

        // Simula el toque real contra esa gema (mismo camino de código que
        // dispara un OnTriggerEnter2D real, sin depender de física en Play Mode).
        var colliderOrbe = orbeCercano.GetComponent<CircleCollider2D>();
        typeof(Enemigo).GetMethod("OnTriggerEnter2D", Flags).Invoke(rival, new object[] { colliderOrbe });
        bool orbeDestruido = orbeCercano == null || orbeCercano.recolectado;
        Debug.Log($"Tras el toque del Rival: gema marcada como robada/destruida={orbeDestruido}");
        if (!orbeDestruido)
            Debug.LogError("FALLÓ: el Rival tocó la gema pero no se procesó como robada.");
        else
            Debug.Log("OK: el Rival roba la gema al tocarla.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
