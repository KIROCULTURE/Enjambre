using UnityEngine;

/// <summary>
/// Fondo detrás del enjambre: una nebulosa radial que sigue el centro de
/// masa del jugador y un campo de estrellas fijo que titila individualmente.
/// Ambas texturas se generan por código, sin necesitar ningún asset de
/// arte importado.
///
/// El color y el "respiro" de la nebulosa ya no dependen de cuántos
/// enemigos hay cerca: siguen GameManager.Intensidad, la misma escalada de
/// tiempo que gobierna toda la estética de la partida. Al empezar, la
/// nebulosa es casi oscura y respira lento e imperceptible; cerca del
/// clímax se pone violeta y el respiro se vuelve un parpadeo nervioso.
/// </summary>
public class FondoNebulosa : MonoBehaviour
{
    [Header("Nebulosa — el color real se interpola cada frame según GameManager.Intensidad")]
    public Color colorApagado = new Color(0.0706f, 0.0706f, 0.0863f); // #121216, al empezar la partida
    public Color colorVivido = new Color(0.1098f, 0.0549f, 0.1647f); // #1c0e2a, en el clímax
    public float radioNebulosa = 6.5f;

    [Header("Respiro (pulso de alpha)")]
    public float alphaBase = 0.75f;
    public float amplitudPulsoMin = 0.03f;
    public float amplitudPulsoMax = 0.12f;

    [Header("Nivel 2 — fondo propio (bug: se quedaba en modo fiesta de Nivel 1)")]
    // Bug real reportado (Fase 1, punto 2): esta nebulosa seguía mezclando
    // el arcoíris de "modo fiesta" de Nivel 1 durante la cutscene/pelea de
    // Nivel 2, porque el Fever de Nivel 1 no se apagaba al cruzar el
    // portal. Nivel 2 ahora tiene su propia paleta (un carmesí tenso, nada
    // de arcoíris), atada a tPelea/duracionPeleaNivel2 — a propósito NO
    // ligada a la vida del boss (a diferencia de una primera idea): la
    // Fase 2 del rediseño grande (Super Administrador) no tiene vida de
    // boss ni orbes, es pura supervivencia por tiempo, así que el reloj de
    // la pelea es la única señal que existe durante TODA la pelea de
    // punta a punta — y de paso hace que el fondo se intensifique solo
    // hacia la escalada de privilegios y el clímax final, sin cableado
    // extra.
    public Color colorApagadoNivel2 = new Color(0.0784f, 0.0392f, 0.0471f); // #140c0c, arranca la pelea
    public Color colorVividoNivel2 = new Color(0.2941f, 0.0392f, 0.0549f); // #4b0a0e, cerca del final

    [Header("Modo fiesta — feedback visual mientras dura el Fever (solo Nivel 1)")]
    // Mientras el Fever está activo, la nebulosa se mezcla con un color que
    // gira por el espectro y el respiro se acelera/agranda — cuanto más
    // alto el nivel, más "fiesta". nivelFeverSuavizado evita el salto seco
    // que se vería si esto siguiera directo al entero NivelFever (que sube
    // de a uno cada golpe de combo, y ahora también puede bajar de a uno
    // solo con el decaimiento).
    public float velocidadArcoiris = 0.35f;
    public float mezclaMaximaFiesta = 0.6f;
    float nivelFeverSuavizado = 1f;

    [Header("Estrellas")]
    public int cantidadEstrellas = 90;
    // Cubre el mundo jugable completo (ver GameManager.mitadMundoAncho/Alto)
    // con margen: ahora la cámara pasea por un área más grande que la
    // pantalla (estilo Agar.io), así que el campo de estrellas ya no puede
    // ser del tamaño de un solo viewport o se vería el borde al paniar.
    public float anchoMundo = 22f;
    public float altoMundo = 12f;
    public float velocidadTitilar = 1.2f;

    SpriteRenderer nebulosaSR;
    SpriteRenderer estrellasSR;
    Texture2D texEstrellas;

    struct Estrella { public int x, y; public byte brilloBase; public float fase, vel; }
    Estrella[] estrellas;

    void Awake()
    {
        var goNebulosa = new GameObject("Nebulosa");
        goNebulosa.transform.SetParent(transform, false);
        nebulosaSR = goNebulosa.AddComponent<SpriteRenderer>();
        nebulosaSR.sprite = GenerarSpriteRadial(256);
        nebulosaSR.sortingOrder = -100;
        nebulosaSR.transform.localScale = Vector3.one * radioNebulosa * 2f;

        var goEstrellas = new GameObject("Estrellas");
        goEstrellas.transform.SetParent(transform, false);
        estrellasSR = goEstrellas.AddComponent<SpriteRenderer>();
        estrellasSR.sprite = GenerarSpriteEstrellas(256);
        estrellasSR.sortingOrder = -101;
        estrellasSR.transform.position = Vector3.zero;
        estrellasSR.transform.localScale = new Vector3(anchoMundo, altoMundo, 1f);
    }

    Sprite GenerarSpriteRadial(int tam)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float cx = tam / 2f, cy = tam / 2f, r = tam / 2f;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
    }

    Sprite GenerarSpriteEstrellas(int tam)
    {
        texEstrellas = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        texEstrellas.filterMode = FilterMode.Point;
        texEstrellas.wrapMode = TextureWrapMode.Repeat;

        var pix = new Color32[tam * tam];
        for (int i = 0; i < pix.Length; i++) pix[i] = new Color32(0, 0, 0, 0);

        estrellas = new Estrella[cantidadEstrellas];
        for (int i = 0; i < cantidadEstrellas; i++)
        {
            var e = new Estrella
            {
                x = Random.Range(0, tam),
                y = Random.Range(0, tam),
                brilloBase = (byte)Random.Range(140, 235),
                fase = Random.Range(0f, Mathf.PI * 2f),
                vel = Random.Range(0.5f, 1.3f)
            };
            estrellas[i] = e;
            pix[e.y * tam + e.x] = new Color32(210, 195, 230, e.brilloBase);
        }
        texEstrellas.SetPixels32(pix);
        texEstrellas.Apply();
        return Sprite.Create(texEstrellas, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 32, 0, SpriteMeshType.FullRect);
    }

    void Update()
    {
        var gm = GameManager.Instancia;
        if (gm == null) return;

        nebulosaSR.transform.position = gm.CentroDeMasa();

        float intensidad;
        Color c;
        float feverT = 0f;

        if (gm.modoNivel2)
        {
            // Nada de Fever/modo fiesta acá — ese estado es de Nivel 1 y no
            // se resetea al cruzar el portal (a propósito: el jugador todavía
            // lo está viendo en el HUD de Nivel 1 hasta el último frame).
            // Ramp propio, ver el comentario del header.
            intensidad = gm.duracionPeleaNivel2 > 0f ? Mathf.Clamp01(gm.TiempoPelea / gm.duracionPeleaNivel2) : 0f;
            c = Color.Lerp(colorApagadoNivel2, colorVividoNivel2, intensidad);
        }
        else
        {
            intensidad = gm.Intensidad;
            c = Color.Lerp(colorApagado, colorVivido, intensidad);

            nivelFeverSuavizado = Mathf.MoveTowards(nivelFeverSuavizado, gm.NivelFever, Time.unscaledDeltaTime * 2.5f);
            feverT = gm.nivelFeverMax > 1 ? Mathf.Clamp01((nivelFeverSuavizado - 1f) / (gm.nivelFeverMax - 1f)) : 0f;
            if (feverT > 0f)
            {
                float hue = Mathf.Repeat(Time.unscaledTime * velocidadArcoiris, 1f);
                Color arcoiris = Color.HSVToRGB(hue, 0.85f, 1f);
                c = Color.Lerp(c, arcoiris, feverT * mezclaMaximaFiesta);
            }
        }

        // Respiro: lento e imperceptible al empezar, parpadeo nervioso cerca
        // del clímax — tanto la velocidad como la amplitud del pulso siguen
        // la misma Intensidad que gobierna el resto de la estética (en
        // Nivel 2, el ramp propio de más arriba).
        float velocidadPulso = Mathf.Lerp(0.4f, 3f, intensidad);
        float amplitudPulso = Mathf.Lerp(amplitudPulsoMin, amplitudPulsoMax, intensidad);
        if (feverT > 0f)
        {
            velocidadPulso *= Mathf.Lerp(1f, 3f, feverT);
            amplitudPulso *= Mathf.Lerp(1f, 1.8f, feverT);
        }

        float pulso = Mathf.Sin(Time.time * velocidadPulso) * amplitudPulso;
        c.a = Mathf.Clamp01(alphaBase + pulso);
        nebulosaSR.color = c;

        // Titileo por-estrella real, pero no hace falta recalcular cada frame.
        if (Time.frameCount % 3 == 0) ActualizarTitileo(feverT);
    }

    void ActualizarTitileo(float feverT = 0f)
    {
        float velExtra = Mathf.Lerp(1f, 2.2f, feverT);
        for (int i = 0; i < estrellas.Length; i++)
        {
            var e = estrellas[i];
            float b = 0.35f + Mathf.Sin(Time.time * velocidadTitilar * e.vel * velExtra + e.fase) * 0.35f;
            byte alpha = (byte)Mathf.Clamp(e.brilloBase * (0.4f + b), 0, 255);
            texEstrellas.SetPixel(e.x, e.y, new Color32(210, 195, 230, alpha));
        }
        texEstrellas.Apply(false);
    }
}
