using UnityEngine;

/// <summary>
/// La gema cian que hace crecer al núcleo que la toca. También puede ser
/// la variante especial "de velocidad" (amarilla, forma de rayo): en vez
/// de crecer, le da al enjambre un impulso de velocidad temporal.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class Orbe : MonoBehaviour
{
    public float radio = 0.14f;
    public float velocidadGiro = 25f;
    public bool esVelocidad = false;
    // Especial y raro: al recogerlo hace estallar varias gemas normales
    // alrededor (ver GameManager.ProcesarPickupOrbe/GenerarOrbeCercaDe) en
    // vez de dar un efecto directo al núcleo, como sí hace esVelocidad.
    public bool esExplosivo = false;
    // Fase 5 del rediseño de Nivel 2: marca los orbes que suelta el boss
    // tras un pulso potente (ver GameManager.GenerarOrbesDelBossNivel2) —
    // GameManager.ProcesarPickupOrbeNivel2 les da MENOS carga que a un
    // orbe normal (uno de los dos frenos contra el loop combo->pulso
    // potente->orbes->combo). A propósito sin cambio de sprite/color: la
    // recompensa tiene que sentirse generosa a la vista, el freno es de
    // ritmo, no algo que el jugador tenga que distinguir a simple vista.
    public bool origenBoss = false;

    [Header("Visual — el color real se interpola cada frame según GameManager.Intensidad")]
    public Color colorApagado = new Color(0.302f, 0.431f, 0.376f); // #4d6e60, al empezar la partida
    // HDR a propósito (canales > 1) — con Bloom.threshold=1 (ver
    // AplicarPostProcesado.cs) esto hace que la gema brille de verdad en
    // el clímax en vez de ser un cian saturado plano.
    public Color colorVivido = new Color(0.4f, 1.8f, 1.24f);

    // El orbe de velocidad no escala con la intensidad: siempre es amarillo,
    // para que se distinga a simple vista del orbe normal en cualquier momento.
    static readonly Color ColorVelocidad = new Color(1f, 0.85f, 0.1f);
    // Violeta: no se parece a ningún otro color ya usado (cian de la gema,
    // amarillo de velocidad, ámbar de crecimiento, magenta de Fever, rojo
    // de enemigos), para que se reconozca de lejos como "otra cosa".
    static readonly Color ColorExplosivo = new Color(0.65f, 0.3f, 1f);

    Color ColorActual => Color.Lerp(colorApagado, colorVivido, GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f);

    /// <summary>
    /// Evita recolectar el mismo orbe dos veces: Destroy() no es inmediato,
    /// así que dos núcleos podrían tocarlo en el mismo frame y cada uno
    /// agendaría su propio orbe de reemplazo, haciendo crecer la cantidad
    /// de orbes sin límite con el tiempo.
    /// </summary>
    [HideInInspector] public bool recolectado = false;

    SpriteRenderer sr;
    CircleCollider2D col;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        gameObject.tag = "Orbe";
        AplicarApariencia();
    }

    /// <summary>Llamado por GameManager justo después de instanciarlo como orbe de velocidad.</summary>
    public void MarcarComoVelocidad()
    {
        esVelocidad = true;
        AplicarApariencia();
    }

    /// <summary>Llamado por GameManager justo después de instanciarlo como orbe explosivo.</summary>
    public void MarcarComoExplosivo()
    {
        esExplosivo = true;
        AplicarApariencia();
    }

    void AplicarApariencia()
    {
        sr.sprite = esVelocidad ? ShapeFactory.Rayo(48) : esExplosivo ? ShapeFactory.Estrella(48, 8) : ShapeFactory.Diamante(48);
        sr.color = esVelocidad ? ColorVelocidad : esExplosivo ? ColorExplosivo : ColorActual;
        float r = (esVelocidad || esExplosivo) ? radio * 1.4f : radio;
        transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
    }

    void Update()
    {
        transform.Rotate(0, 0, velocidadGiro * (esVelocidad || esExplosivo ? 1.8f : 1f) * Time.deltaTime);
        // Los especiales (velocidad, explosivo) tienen color fijo, no
        // escalan con Intensidad como la gema normal.
        if (!esVelocidad && !esExplosivo) sr.color = ColorActual;
    }
}
