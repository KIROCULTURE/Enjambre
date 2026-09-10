using System.Collections;
using UnityEngine;

/// <summary>
/// Una "pared" del evento especial Muralla Láser (ver
/// GameManager.RafagaMurallaLaser) y de la familia "láser telegrafiado"
/// de la pelea de Nivel 2 (GameManager.PatronBarridoSimple/Cruz/Corredor/Abanico):
/// telegrafea (con flujo de energía animado, ver Update) durante
/// duracionTelegraph, después resuelve UN SOLO chequeo de área contra el
/// enjambre/Forma Precisa (GameManager.ResolverImpactoLaser) — no
/// OnTriggerStay2D continuo, que arriesgaría una cascada (un núcleo
/// recién partido por el mismo golpe podría seguir adentro del área y
/// re-disparar el golpe en el mismo frame). Después de resolver, brilla
/// a pleno un instante y se destruye sola.
///
/// Punto 6 (shaders): si GameManager.materialHazEnergia está asignado, el
/// color/degradé/flujo de energía y el pulso de "carga" del telegraph
/// viven en el shader (Assets/Shaders/HazEnergia.shader), modulados por
/// MaterialPropertyBlock (_ColorBorde/_Progreso) — un solo Material
/// compartido para todas las paredes a la vez, sin instanciar uno por
/// pared. Si no está asignado (escena vieja sin correr el setup de
/// nuevo), cae de vuelta al degradé horneado en textura de
/// ShapeFactory.Haz con el pulso calculado a mano, igual que antes.
///
/// Nueva pieza (revisión, patrones propios de la Fase 2): además del
/// Configurar(Rect,...) original (paredes axis-aligned, ResolverImpactoLaser),
/// hay un Configurar(centro, tamaño, ángulo, ...) que rota transform de
/// verdad — para los rayos radiales que salen del boss en cualquier
/// ángulo, no solo horizontal/vertical. Ese camino resuelve contra
/// GameManager.ResolverImpactoLaserRotado en vez de ResolverImpactoLaser,
/// SIN tocar el camino Rect original (usado por RafagaMurallaLaser, los 4
/// patrones heredados y VerificarMurallaLaser — no vale la pena
/// arriesgarlos por una rotación que ninguno de ellos necesita).
/// </summary>
public class LaserHazard : MonoBehaviour
{
    SpriteRenderer sr;
    Rect area;
    Vector2 centroRotado, tamanoRotado;
    float anguloGrados;
    bool rotado;
    float duracionTelegraph;
    float duracionActivo;
    Color color;
    float tInicioTelegraph;
    bool activo;
    MaterialPropertyBlock mpb;

    /// <summary>Camino original — pared axis-aligned, resuelve contra ResolverImpactoLaser(Rect). Sin cambios de comportamiento.</summary>
    public void Configurar(Rect areaMundo, float telegraph, float activoDur, Color colorMuro)
    {
        rotado = false;
        area = areaMundo;
        ConfigurarComun(telegraph, activoDur, colorMuro);

        transform.position = new Vector3(area.center.x, area.center.y, 0f);
        transform.localScale = new Vector3(Mathf.Max(area.width, 0.01f), Mathf.Max(area.height, 0.01f), 1f);
    }

    /// <summary>Camino nuevo — pared rotada un ángulo cualquiera alrededor de `centro`, resuelve contra ResolverImpactoLaserRotado. `tamaño` es (ancho, largo) ANTES de rotar (a ángulo 0 equivale exactamente al Rect centrado en `centro` de ese mismo tamaño — ver VerificarFase2PatronesPropios.cs, la prueba de equivalencia).</summary>
    public void Configurar(Vector2 centro, Vector2 tamano, float anguloGrados, float telegraph, float activoDur, Color colorMuro)
    {
        rotado = true;
        centroRotado = centro;
        tamanoRotado = tamano;
        this.anguloGrados = anguloGrados;
        ConfigurarComun(telegraph, activoDur, colorMuro);

        transform.position = new Vector3(centro.x, centro.y, 0f);
        transform.localScale = new Vector3(Mathf.Max(tamano.x, 0.01f), Mathf.Max(tamano.y, 0.01f), 1f);
        transform.rotation = Quaternion.Euler(0f, 0f, anguloGrados);
    }

    void ConfigurarComun(float telegraph, float activoDur, Color colorMuro)
    {
        duracionTelegraph = telegraph;
        duracionActivo = activoDur;
        color = colorMuro;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = -5;

        var matHaz = GameManager.Instancia != null ? GameManager.Instancia.materialHazEnergia : null;
        if (matHaz != null)
        {
            sr.sprite = ShapeFactory.Cuadrado(8, Color.white); // solo portador de malla/UV — el shader ignora el contenido de la textura
            sr.sharedMaterial = matHaz;
            mpb = new MaterialPropertyBlock();
            mpb.SetColor("_ColorBorde", color);
            mpb.SetColor("_ColorNucleo", Color.white);
            mpb.SetFloat("_Progreso", 0f);
            sr.SetPropertyBlock(mpb);
        }
        else
        {
            sr.sprite = ShapeFactory.Haz(64, color);
            sr.color = new Color(1f, 1f, 1f, 0.16f);
        }

        tInicioTelegraph = Time.time;
        StartCoroutine(Secuencia());
    }

    // Pulso de "carga" que se acelera y se hace más intenso a medida que
    // se acerca el impacto — una cuenta regresiva legible sin necesitar
    // texto. Con shader, esto es nada más el factor _Progreso (el flujo
    // de energía en sí vive en el shader vía _Time); sin shader, se
    // recalcula el pulso a mano igual que antes.
    void Update()
    {
        if (activo || sr == null || duracionTelegraph <= 0f) return;
        float progreso = Mathf.Clamp01((Time.time - tInicioTelegraph) / duracionTelegraph);

        if (mpb != null)
        {
            mpb.SetFloat("_Progreso", progreso);
            sr.SetPropertyBlock(mpb);
        }
        else
        {
            float frecuencia = Mathf.Lerp(4f, 13f, progreso);
            float onda = (Mathf.Sin(Time.time * frecuencia) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.14f, 0.32f, progreso) + onda * Mathf.Lerp(0.05f, 0.28f, progreso);
            sr.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    IEnumerator Secuencia()
    {
        yield return new WaitForSeconds(duracionTelegraph);

        if (rotado) GameManager.Instancia?.ResolverImpactoLaserRotado(centroRotado, tamanoRotado, anguloGrados);
        else GameManager.Instancia?.ResolverImpactoLaser(area);
        activo = true;
        if (mpb != null)
        {
            mpb.SetFloat("_Progreso", 1f);
            sr.SetPropertyBlock(mpb);
        }
        else
        {
            sr.color = Color.white; // alpha=1 — el degradé horneado del sprite ya hace el resto
        }

        yield return new WaitForSeconds(duracionActivo);
        Destroy(gameObject);
    }
}
