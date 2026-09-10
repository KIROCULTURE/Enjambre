using UnityEngine;
using System.Collections;

/// <summary>
/// Música de menú y de juego, con crossfade y corte al perder. Arrastrá
/// tus propios archivos de audio en "clipMenu" / "clipJuego" en el
/// Inspector — sin ellos el juego sigue funcionando igual, simplemente
/// no suena música.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instancia { get; private set; }

    [Header("Arrastrá acá tus archivos de música")]
    public AudioClip clipMenu;
    public AudioClip clipJuego;
    public AudioClip clipNivel2;

    [Range(0f, 1f)] public float volumen = 0.5f;
    public float duracionFade = 0.6f;

    AudioSource fuente;
    Coroutine rutinaActual;

    void Awake()
    {
        Instancia = this;
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.volume = 0f;
    }

    public void ReproducirMenu() => Reproducir(clipMenu);
    public void ReproducirJuego() => Reproducir(clipJuego);
    public void ReproducirNivel2() => Reproducir(clipNivel2 != null ? clipNivel2 : clipJuego);

    /// <summary>
    /// Autoridad real del reloj de la pelea de Nivel 2 (Fase 6/7, pedido
    /// vía revisión): CambiarClip tarda duracionFade en llamar a
    /// fuente.Play() (fade-out antes de arrancar), así que tPelea (que
    /// arranca en 0 de forma síncrona en IniciarPeleaNivel2) y el audio
    /// real arrancan con un desfasaje fijo de ese mismo tiempo si tPelea
    /// se mide con Time.deltaTime — más de un beat completo (0.511s) de
    /// deriva constante contra la música real. GameManager.Update() usa
    /// esto para leer tPelea DEL audio cuando está sonando de verdad, y
    /// solo cae a deltaTime como fallback (Editor batch mode no reproduce
    /// audio de verdad, así que fuente.isPlaying nunca da true ahí — todos
    /// los Verificar* que fuerzan tPelea por reflection siguen intactos).
    /// </summary>
    public bool ReproduciendoClipNivel2 => clipNivel2 != null && fuente.clip == clipNivel2 && fuente.isPlaying;
    public float TiempoClipNivel2 => fuente.time;

    /// <summary>Corta la música (con fade) — se llama al perder.</summary>
    public void Detener()
    {
        if (rutinaActual != null) StopCoroutine(rutinaActual);
        rutinaActual = StartCoroutine(FadeYDetener());
    }

    void Reproducir(AudioClip clip)
    {
        if (clip == null) { Detener(); return; }
        if (fuente.clip == clip && fuente.isPlaying) return;
        if (rutinaActual != null) StopCoroutine(rutinaActual);
        rutinaActual = StartCoroutine(CambiarClip(clip));
    }

    IEnumerator CambiarClip(AudioClip clip)
    {
        yield return FadeVolumen(0f);
        fuente.clip = clip;
        fuente.Play();
        yield return FadeVolumen(volumen);
    }

    IEnumerator FadeYDetener()
    {
        yield return FadeVolumen(0f);
        fuente.Stop();
    }

    // Tiempo real (no Time.deltaTime): la música no debería frenarse
    // durante el freeze-frame de un golpe.
    IEnumerator FadeVolumen(float objetivo)
    {
        float inicio = fuente.volume;
        float t = 0f;
        while (t < duracionFade)
        {
            t += Time.unscaledDeltaTime;
            fuente.volume = Mathf.Lerp(inicio, objetivo, t / duracionFade);
            yield return null;
        }
        fuente.volume = objetivo;
    }
}
