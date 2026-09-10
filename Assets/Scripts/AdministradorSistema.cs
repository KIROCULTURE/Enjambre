using System.Collections;
using UnityEngine;

/// <summary>
/// El antagonista del Nivel 2 — narrativamente el administrador del
/// sistema que intenta borrar al virus (el jugador) y fracasa; cada
/// fallo lo deja en una forma nueva (de ahí el cambio de género entre
/// niveles). Actúa desde el miedo y la frustración, no la superioridad —
/// se mantiene lejos del jugador durante la pelea real (punto 4).
///
/// Sprite y Animator son PLACEHOLDER (Kenney "Toon Characters", personaje
/// Robot, CC0 — ver Assets/Sprites/Boss/LEEME_PLACEHOLDER.txt). Lo que
/// importa mantener estable para cuando se reemplace por arte final es el
/// AnimatorController con estos 3 estados nombrados: idle, lanzar_hechizo,
/// retroceso (ver Assets/Editor/CrearAdministradorSistema.cs) — reemplazar
/// el arte es solo cambiar los AnimationClip de cada estado, sin tocar
/// este script ni GameManager.
///
/// No tiene collider ni vida propia — no es golpeable en este diseño (sin
/// barra de vida del boss). Toda su interacción con el jugador pasa por
/// GameManager (la cutscene) y, en el punto 4, por los patrones de
/// ataque que dispara.
/// </summary>
public class AdministradorSistema : MonoBehaviour
{
    public SpriteRenderer sr;
    public Animator animator;

    // Pose "cayendo" para el beat de descenso — se asigna a mano porque
    // durante el descenso el Animator queda desactivado (ver Descender);
    // controlarlo por Animator sería un cuarto estado que no aporta nada
    // una vez puesto el arte final (el descenso es un tween de una sola
    // vez, no un estado reentrante como los otros tres).
    public Sprite spriteDescenso;

    /// <summary>Tween simple desde la posición actual hasta destino. El Animator se apaga mientras dura para que spriteDescenso no se pise con el estado Idle.</summary>
    public IEnumerator Descender(Vector2 destino, float duracion)
    {
        if (animator != null) animator.enabled = false;
        if (sr != null && spriteDescenso != null) sr.sprite = spriteDescenso;

        Vector2 inicio = transform.position;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duracion);
            // Ease-out cúbico: entra rápido y frena — se lee como un boss
            // "aterrizando" con peso, no flotando hasta su marca.
            float suavizado = 1f - Mathf.Pow(1f - p, 3f);
            transform.position = Vector2.Lerp(inicio, destino, suavizado);
            yield return null;
        }
        transform.position = destino;
        TerminarDescensoInstantaneo();
    }

    /// <summary>Deja al boss en un estado consistente sin tickear el tween — lo usa también GameManager.FinalizarCutsceneAperturaInstantanea cuando se saltea la cutscene.</summary>
    public void TerminarDescensoInstantaneo()
    {
        if (animator != null) animator.enabled = true; // vuelve a Idle, el estado default del controller
    }

    public void LanzarHechizo() => animator?.SetTrigger("LanzarHechizo");
    public void Retroceder() => animator?.SetTrigger("Retroceso");

    [Header("Movimiento en la pelea (punto 4) — se mantiene lejos del jugador a propósito, ver clase")]
    public float velocidadMovimiento = 2.0f;
    public float distanciaMinimaJugador = 1.7f;
    public float margenMovimiento = 0.7f;
    // La altura "de descanso" (fracción de mitadAlto) tiene que quedar
    // ESTRICTAMENTE por debajo del techo real (mitadAlto - margenMovimiento)
    // — bug real reportado: antes las dos eran el mismo número, así que
    // el objetivo en Y ya nacía pegado al techo. Cuando el jugador se
    // acercaba por ABAJO y el retroceso calculado quería empujar al boss
    // todavía más arriba, el clamp lo devolvía exactamente a donde ya
    // estaba — "no se aleja realmente, se opone" sin reaccionar. Con esta
    // fracción más baja queda aire real por encima para retroceder de
    // verdad hacia el techo cuando lo presionás.
    public float fraccionAlturaDescanso = 0.55f;

    // Gateado por GameManager.PeleaActiva (no por su propio estado) para
    // no moverse durante la cutscene de apertura, donde Descender() ya
    // controla la posición a mano. Fase 7: tampoco se mueve durante la
    // terminal de escalada de privilegios — "colapsado, tecleando un
    // comando" se lee mal si de fondo sigue reposicionándose por la arena
    // como si nada.
    void Update()
    {
        var gm = GameManager.Instancia;
        if (gm == null || gm.estado != EstadoJuego.Jugando || !gm.PeleaActiva || gm.EnEscaladaPrivilegiosNivel2) return;
        var jugador = gm.FormaPrecisaActiva;
        if (jugador == null) return;
        Vector2 objetivo = CalcularObjetivo(jugador.transform.position, gm.mitadAncho, gm.mitadAlto, margenMovimiento, distanciaMinimaJugador, fraccionAlturaDescanso);
        Mover(objetivo, Time.deltaTime);
    }

    public void Mover(Vector2 objetivo, float dt) =>
        transform.position = Vector2.MoveTowards(transform.position, objetivo, velocidadMovimiento * dt);

    /// <summary>
    /// Pura y testeable sin corrutina (ver VerificarPeleaNivel2.cs): en
    /// reposo se mantiene cerca (no pegado) del borde superior del área y
    /// del lado horizontal opuesto al jugador — nunca quieto exactamente
    /// arriba tuyo, así siempre hay que mirar a otra parte de la pantalla
    /// para leer el próximo ataque. Si el jugador se acerca más de
    /// distanciaMinima (desde cualquier lado, abajo incluido) retrocede
    /// de verdad en esa dirección, con aire real hasta el techo — tanto
    /// por caracterización (actúa desde el miedo, no la superioridad)
    /// como para que los patrones de área sigan siendo legibles.
    /// </summary>
    public static Vector2 CalcularObjetivo(Vector2 posJugador, float mitadAncho, float mitadAlto, float margen, float distanciaMinima, float fraccionAlturaDescanso)
    {
        float x = Mathf.Clamp(-posJugador.x * 0.7f, -mitadAncho + margen, mitadAncho - margen);
        float y = mitadAlto * fraccionAlturaDescanso;
        Vector2 objetivo = new Vector2(x, y);

        Vector2 haciaObjetivo = objetivo - posJugador;
        if (haciaObjetivo.magnitude < distanciaMinima)
        {
            Vector2 direccion = haciaObjetivo.sqrMagnitude > 0.0001f ? haciaObjetivo.normalized : Vector2.up;
            objetivo = posJugador + direccion * distanciaMinima;
        }
        objetivo.x = Mathf.Clamp(objetivo.x, -mitadAncho + margen, mitadAncho - margen);
        objetivo.y = Mathf.Clamp(objetivo.y, -mitadAlto + margen, mitadAlto - margen);
        return objetivo;
    }
}
