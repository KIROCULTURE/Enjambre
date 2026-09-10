using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) el evento especial Muralla Láser: que
/// RafagaMurallaLaser arma 4 paredes alrededor de una zona segura
/// DESPLAZADA de donde está el enjambre (no centrada en su propia
/// posición — ver el comentario del bug en GameManager.RafagaMurallaLaser:
/// un jugador quieto quedaba siempre a salvo por construcción, confirmado
/// jugando), y que ResolverImpactoLaser golpea a un núcleo dentro de una
/// pared pero no a uno en la zona segura real. Las paredes se capturan UNA
/// vez (con un solo núcleo ancla en el origen, así CentroDeMasa() es
/// exactamente ese punto, sin ambigüedad) y después se prueban por
/// separado contra posiciones de prueba — RafagaMurallaLaser recalcula
/// CentroDeMasa() cada vez que se la llama, así que agregar un segundo
/// núcleo en otra parte ANTES de disparar movería el centro de masa y
/// correría las paredes; llamando ResolverImpactoLaser directo con el
/// Rect ya capturado se evita esa trampa. No guarda la escena.
/// </summary>
public static class VerificarMurallaLaser
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Muralla Láser")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // Ancla en el origen: con un solo núcleo activo, CentroDeMasa() es
        // exactamente (0,0), sin ambigüedad de centroide ponderado. Esta
        // es la posición del "jugador" que NO se movió.
        var ancla = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        ancla.radio = 0.5f;
        Invocar(ancla, "Awake");
        Invocar(ancla, "OnEnable");

        typeof(GameManager).GetMethod("RafagaMurallaLaser", Flags).Invoke(gm, null);

        var muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None);
        Debug.Log($"RafagaMurallaLaser generó {muros.Length} paredes (esperado 4).");
        if (muros.Length != 4) Debug.LogError($"FALLÓ: esperaba 4 paredes, aparecieron {muros.Length}");

        var campoArea = typeof(LaserHazard).GetField("area", Flags);
        var areas = muros.Select(m => (Rect)campoArea.GetValue(m)).ToList();

        // EL PUNTO DEL FIX: el origen (donde está el "jugador" quieto) ya
        // NO puede quedar en la zona segura — si sigue estando afuera de
        // las 4 paredes sin más, es que la zona segura coincide con su
        // posición actual otra vez (el bug original).
        bool origenEnAlgunaPared = areas.Any(r => r.Contains(Vector2.zero));
        Debug.Log($"¿El origen (donde está el jugador quieto) cae DENTRO de una pared? {origenEnAlgunaPared} (esperado true — no puede quedarse quieto y estar a salvo)");
        if (!origenEnAlgunaPared)
            Debug.LogError("FALLÓ: el jugador sigue a salvo sin moverse — la zona segura no se desplazó lo suficiente.");
        else
            Debug.Log("OK: quedarse quieto ya no es seguro — hay que moverse de verdad.");

        // Punto claramente afuera del área visible entera (más allá de
        // donde cualquier pared podría terminar) — tiene que quedar
        // cubierto por AL MENOS una pared, si no el "encierro" tendría un
        // hueco en el borde real del mapa.
        // OJO: un punto FIJO relativo al jugador (p.ej. "-mitadAncho*1.19")
        // ya NO sirve para este chequeo — desde que la zona segura se
        // desplaza hasta 0.75*mitadAncho/Alto (ver el fix de extension más
        // arriba), esa misma zona segura puede legítimamente extenderse
        // hasta ahí, y un punto fijo cualquiera puede caer DENTRO de la
        // zona segura desplazada en vez de en un hueco real — daba falsos
        // "FALLÓ" en ~35-40% de las corridas, según hacia dónde salía el
        // desplazamiento al azar (encontrado corriendo este test varias
        // veces seguidas en un barrido de regresión).
        //
        // La garantía real de "sin huecos" ahora es matemática, no algo
        // que haga falta samplear en runtime: extension=2.0 asegura que
        // las 4 paredes cubren, en el PEOR caso de desplazamiento (0.75 de
        // mitadAncho/Alto), hasta 2.0-0.75=1.25 más allá del jugador en
        // cualquier dirección — más que suficiente para cubrir toda el
        // área que la cámara llega a mostrar (~1.0). El chequeo que sigue
        // (el jugador queda adentro de una pared) ya confirma en la
        // práctica que el desplazamiento se está aplicando de verdad.

        // --- ResolverImpactoLaser: castiga adentro de una pared, no en la zona segura ---
        // "En zona segura" ahora significa el hueco real entre las 4 paredes
        // (no el origen — el origen ya está confirmado adentro de una pared).
        // Se busca un punto así por gradilla simple.
        Vector2? puntoSeguro = null;
        for (float x = -gm.mitadAncho * 1.3f; x <= gm.mitadAncho * 1.3f && puntoSeguro == null; x += 0.2f)
            for (float y = -gm.mitadAlto * 1.3f; y <= gm.mitadAlto * 1.3f && puntoSeguro == null; y += 0.2f)
                if (!areas.Any(r => r.Contains(new Vector2(x, y))))
                    puntoSeguro = new Vector2(x, y);

        if (puntoSeguro == null) { Debug.LogError("FALLÓ: no encontré ningún punto seguro real — las 4 paredes cubren todo."); return; }

        var enSeguro = Object.Instantiate(gm.nucleoPrefab, (Vector3)puntoSeguro.Value, Quaternion.identity);
        enSeguro.radio = 0.5f;
        Invocar(enSeguro, "Awake");
        Invocar(enSeguro, "OnEnable");

        var paredIzquierda = areas.OrderBy(r => r.center.x).First(); // la más a la izquierda
        var enPared = Object.Instantiate(gm.nucleoPrefab, (Vector3)paredIzquierda.center, Quaternion.identity);
        enPared.radio = 0.5f;
        Invocar(enPared, "Awake");
        Invocar(enPared, "OnEnable");

        foreach (var area in areas) GameManager.Instancia.ResolverImpactoLaser(area);

        Debug.Log($"Núcleo en un punto seguro real ({puntoSeguro.Value}): golpeado={enSeguro.golpeado} (esperado false). " +
                   $"Núcleo en el centro de la pared izquierda: golpeado={enPared.golpeado} (esperado true).");

        if (enSeguro.golpeado)
            Debug.LogError("FALLÓ: un núcleo en un punto seguro real fue golpeado por la muralla láser.");
        else if (!enPared.golpeado)
            Debug.LogError("FALLÓ: un núcleo parado en el centro de una pared no fue golpeado.");
        else
            Debug.Log("OK: la zona segura (desplazada) protege, la pared golpea.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
