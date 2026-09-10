using UnityEngine;

/// <summary>
/// Punto único para disparar feedback visual: chispas, ondas de choque
/// y popups de texto. Equivale a las funciones sueltas chispa()/onda()/popup()
/// de la versión web, pero como un singleton con prefabs asignables.
/// </summary>
public class EfectosVisuales : MonoBehaviour
{
    public static EfectosVisuales Instancia { get; private set; }

    [Header("Prefabs")]
    public SimpleParticle chispaPrefab;
    public RingPulse ondaPrefab;
    public PopupText popupPrefab;

    void Awake() => Instancia = this;

    public void Chispas(Vector3 pos, Color color, int cantidad, float fuerza)
    {
        if (chispaPrefab == null) return;
        // Más partículas a medida que sube la intensidad (de ~10 a ~20 en
        // un evento típico): el mismo golpe se siente más grande cerca del clímax.
        float intensidad = GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f;
        int cantidadFinal = Mathf.RoundToInt(cantidad * (1f + intensidad));
        for (int i = 0; i < cantidadFinal; i++)
        {
            var p = Instantiate(chispaPrefab, pos, Quaternion.identity);
            p.Lanzar(Random.insideUnitCircle * fuerza, color);
        }
    }

    public void Onda(Vector3 pos, Color color, float radioMax)
    {
        if (ondaPrefab == null) return;
        Instantiate(ondaPrefab, pos, Quaternion.identity).Lanzar(color, radioMax);
    }

    public void Telegraph(Vector3 pos, float duracion)
    {
        if (ondaPrefab == null) return;
        Instantiate(ondaPrefab, pos, Quaternion.identity)
            .LanzarTelegraph(new Color(1f, 0.184f, 0.373f), 0.55f, duracion);
    }

    public void Popup(Vector3 pos, string texto, Color color, float escalaExtra = 1f)
    {
        if (popupPrefab == null) return;
        Instantiate(popupPrefab, pos, Quaternion.identity).Lanzar(texto, color, escalaExtra);
    }
}
