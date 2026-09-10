using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica la lógica del Fever temporal (sin Play Mode): como
/// Vector3.SmoothDamp y los timers dependen de Time.deltaTime (que no
/// avanza fuera de Play Mode), esto no puede simular el paso real del
/// tiempo — en su lugar llama a ProcesarPickupOrbe repetidas veces (que no
/// depende de deltaTime) para revisar la máquina de estados de subida de
/// nivel, y fuerza el decaimiento con reflection para revisar la bajada.
/// No guarda la escena.
/// </summary>
public static class VerificarFeverTemporal
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Fever Temporal")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        gm.EmpezarPartida();

        Debug.Log($"Tras EmpezarPartida: NivelFever={gm.NivelFever} orbesActivos={LeerCampo<int>(gm, "orbesActivos")} " +
                   $"(esperado {gm.orbesObjetivo})");

        var nucleo = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        nucleo.radio = 0.5f;

        // 100 recolecciones seguidas (sin cortar la ventana de combo, ya
        // que Time.time apenas avanza entre llamadas sincrónicas) suben el
        // Fever de nivel 1 a 5 en los combos 20/40/60/80, y lo renuevan sin
        // subir más en el 100.
        for (int i = 1; i <= 100; i++)
        {
            var orbe = Object.Instantiate(gm.orbePrefab, Vector3.zero, Quaternion.identity);
            gm.ProcesarPickupOrbe(nucleo, orbe);
            if (i % 20 == 0)
            {
                int objetivo = LeerObjetivoEfectivo(gm);
                Debug.Log($"Combo {i}: NivelFever={gm.NivelFever} tFever={LeerCampo<float>(gm, "tFever"):F1} " +
                           $"ObjetivoOrbesEfectivo={objetivo} orbesActivos={LeerCampo<int>(gm, "orbesActivos")}");
            }
        }

        if (gm.NivelFever != gm.nivelFeverMax)
            Debug.LogError($"FALLÓ: esperaba NivelFever={gm.nivelFeverMax} tope, quedó en {gm.NivelFever}");
        else
            Debug.Log("OK: Fever llegó al tope esperado.");

        // Fuerza el decaimiento (bypaseando Time.deltaTime): expira tFever
        // y fuerza tDecaimientoFever vencido antes de cada tick para que
        // el nivel baje de a uno sin depender de tiempo real.
        var campoTFever = typeof(GameManager).GetField("tFever", Flags);
        var campoTDecaimiento = typeof(GameManager).GetField("tDecaimientoFever", Flags);
        var metodoDecaimiento = typeof(GameManager).GetMethod("ActualizarDecaimientoFever", Flags);
        campoTFever.SetValue(gm, 0f);

        for (int paso = 0; paso < 5; paso++)
        {
            int nivelAntes = gm.NivelFever;
            campoTDecaimiento.SetValue(gm, -0.01f);
            metodoDecaimiento.Invoke(gm, null);
            Debug.Log($"Decaimiento paso {paso}: NivelFever {nivelAntes} -> {gm.NivelFever}, ObjetivoOrbesEfectivo={LeerObjetivoEfectivo(gm)}");
        }

        if (gm.NivelFever != 1)
            Debug.LogError($"FALLÓ: esperaba que el Fever bajara hasta 1, quedó en {gm.NivelFever}");
        else
            Debug.Log("OK: Fever decayó del todo hasta apagarse (nivel 1).");

        // Un paso extra no debería bajar nada más (ya está en el piso).
        int nivelPrevio = gm.NivelFever;
        campoTDecaimiento.SetValue(gm, -0.01f);
        metodoDecaimiento.Invoke(gm, null);
        if (gm.NivelFever != nivelPrevio)
            Debug.LogError($"FALLÓ: el nivel siguió bajando de 1 a {gm.NivelFever}");
        else
            Debug.Log("OK: el nivel no baja de 1.");
    }

    static int LeerObjetivoEfectivo(GameManager gm)
    {
        var prop = typeof(GameManager).GetProperty("ObjetivoOrbesEfectivo", Flags);
        return (int)prop.GetValue(gm);
    }

    static T LeerCampo<T>(object obj, string nombre) =>
        (T)obj.GetType().GetField(nombre, Flags).GetValue(obj);

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
