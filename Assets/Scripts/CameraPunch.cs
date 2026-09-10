using UnityEngine;

/// <summary>
/// Cámara: persigue al enjambre dentro del mundo (estilo Agar.io) y le suma
/// shake + zoom de impacto encima. Ponla en la Main Camera.
///
/// El shake antes era una corrutina que StopCoroutine() interrumpía si
/// llegaba un golpe nuevo antes de que la anterior terminara, saltándose la
/// restauración final de posición/zoom (mismo problema que tenía el
/// hit-stop de GameManager). Ahora es un simple plazo evaluado en
/// Update() con tiempo real (no escalado): no hay corrutina que
/// interrumpir, así que siempre termina restaurando la posición base.
///
/// La posición base ya no es fija: cada frame se recalcula persiguiendo
/// GameManager.CentroDeMasa(), clampeada para que el rectángulo visible de
/// la cámara nunca se salga de GameManager.mitadMundoAncho/mitadMundoAlto
/// (el mundo jugable, más grande que lo que la cámara alcanza a mostrar).
/// </summary>
public class CameraPunch : MonoBehaviour
{
    public static CameraPunch Instancia { get; private set; }

    public float duracionSacudida = 0.25f;
    public float fuerzaSacudida = 0.12f;
    public float zoomGolpe = 0.06f;

    [Header("Seguimiento del enjambre")]
    public float suavizadoSeguimiento = 0.35f;

    Camera cam;
    Vector3 posBase;
    Vector3 velocidadSeguimiento;
    float zBase;
    float tamanoBase;
    float tiempoRealInicioSacudida = -999f;

    void Awake()
    {
        Instancia = this;
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        posBase = transform.localPosition;
        zBase = transform.localPosition.z;
        tamanoBase = cam.orthographicSize;
    }

    public void Golpear()
    {
        tiempoRealInicioSacudida = Time.unscaledTime;
    }

    void LateUpdate()
    {
        ActualizarSeguimiento();

        float t = Time.unscaledTime - tiempoRealInicioSacudida;
        if (t < duracionSacudida)
        {
            // Golpes más fuertes a medida que sube la intensidad: la
            // sacudida/zoom del principio de partida se siente contenida,
            // cerca del clímax pega mucho más fuerte.
            float intensidad = GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f;
            float escala = 0.6f + intensidad * 0.8f;
            float p = t / duracionSacudida;
            float fuerza = fuerzaSacudida * escala * (1f - p);
            transform.localPosition = posBase + (Vector3)Random.insideUnitCircle * fuerza;
            cam.orthographicSize = tamanoBase - zoomGolpe * escala * (1f - p);
        }
        else
        {
            transform.localPosition = posBase;
            cam.orthographicSize = tamanoBase;
        }
    }

    // Persigue el centro de masa del enjambre con un suavizado tipo
    // "cámara con inercia" (SmoothDamp, no Lerp) para que se sienta como
    // Agar.io en vez de pegada en seco a la posición del jugador. Clampeada
    // al rectángulo del mundo para que nunca se vea "fuera" del fondo/zona
    // de juego.
    void ActualizarSeguimiento()
    {
        var gm = GameManager.Instancia;
        Vector2 objetivo = gm != null ? gm.CentroDeMasa() : Vector2.zero;

        if (gm != null)
        {
            float limX = Mathf.Max(0f, gm.mitadMundoAncho - gm.mitadAncho);
            float limY = Mathf.Max(0f, gm.mitadMundoAlto - gm.mitadAlto);
            objetivo.x = Mathf.Clamp(objetivo.x, -limX, limX);
            objetivo.y = Mathf.Clamp(objetivo.y, -limY, limY);
        }

        Vector3 destino = new Vector3(objetivo.x, objetivo.y, zBase);
        posBase = Vector3.SmoothDamp(posBase, destino, ref velocidadSeguimiento, suavizadoSeguimiento);
    }
}
