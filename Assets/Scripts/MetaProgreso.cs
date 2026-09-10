using UnityEngine;

/// <summary>
/// Moneda persistente ("Esencia") y mejoras permanentes entre partidas —
/// la primera capa de meta-progresión de Enjambre: cada partida da Esencia
/// según cuánto sobreviviste/combo/núcleos alcanzados (ver
/// CalcularRecompensa), gastable en mejoras que suman sobre el valor base
/// tuneable del Inspector de GameManager (nunca lo sobreescriben — ver
/// ValorEfectivo).
///
/// La aritmética pura (CalcularRecompensa/CostoNivel/ValorEfectivo) no
/// toca PlayerPrefs, para poder testearla en batch mode sin arriesgar
/// corromper el save real del jugador. Solo GanarEsencia/ComprarMejora
/// tocan PlayerPrefs, y guardan de inmediato (mismo patrón que
/// VolumenControlador.SetVolumen) — no al final de la partida, porque
/// comprar tiene que persistir en el momento en que el jugador lo hace.
/// </summary>
public static class MetaProgreso
{
    const string ClaveEsencia = "enjambre_esencia";
    const string ClaveNivelNucleo = "enjambre_mejora_nucleo";
    const string ClaveNivelCrecimiento = "enjambre_mejora_crecimiento";
    const string ClaveNivelFever = "enjambre_mejora_fever";
    const string ClaveNivelGemas = "enjambre_mejora_gemas";
    const string ClaveNivelDosDesbloqueado = "enjambre_nivel2_desbloqueado";
    const string ClaveFormaPrecisaDesbloqueada = "enjambre_forma_precisa_desbloqueada";
    const string ClaveCutsceneCierreVista = "enjambre_cutscene_cierre_vista";

    public const int NivelMaximo = 5;

    public static int Esencia { get; private set; }
    public static int NivelNucleo { get; private set; }
    public static int NivelCrecimiento { get; private set; }
    public static int NivelFever { get; private set; }
    public static int NivelGemas { get; private set; }
    /// <summary>Se desbloquea la primera vez que se entra al portal de Victoria (ver GameManager.SecuenciaVictoria) — persiste entre partidas.</summary>
    public static bool NivelDosDesbloqueado { get; private set; }
    /// <summary>
    /// La "Forma Precisa" — el personaje jugable de Nivel 2, narrativamente
    /// el virus recompilado a la fuerza por el hechizo fallido del boss.
    /// Se desbloquea en el mismo instante que Nivel 2 (cruzar el portal es
    /// el único gate), pero es su propio flag porque conceptualmente son
    /// dos cosas distintas: uno habilita un modo de juego, el otro habilita
    /// un personaje. Persiste entre partidas.
    /// </summary>
    public static bool FormaPrecisaDesbloqueada { get; private set; }
    /// <summary>
    /// Se marca la primera vez que se termina de ver la cutscene de cierre
    /// (punto 5: boss recasta, campo de fuerza a esquinas opuestas, láser
    /// final, transformación a la forma de Nivel 3) — ganar de nuevo en
    /// una partida futura no la vuelve a mostrar entera. Persiste entre
    /// partidas.
    ///
    /// La cutscene de APERTURA es distinta a propósito: usa un flag de
    /// sesión en memoria (GameManager.cutsceneAperturaVistaSesion), no
    /// PlayerPrefs — pedido explícito de que un reintento tras morir la
    /// saltee, pero abrir el juego de nuevo siempre la muestre.
    /// </summary>
    public static bool CutsceneCierreVista { get; private set; }

    public static void Cargar()
    {
        Esencia = PlayerPrefs.GetInt(ClaveEsencia, 0);
        NivelNucleo = PlayerPrefs.GetInt(ClaveNivelNucleo, 0);
        NivelCrecimiento = PlayerPrefs.GetInt(ClaveNivelCrecimiento, 0);
        NivelFever = PlayerPrefs.GetInt(ClaveNivelFever, 0);
        NivelGemas = PlayerPrefs.GetInt(ClaveNivelGemas, 0);
        NivelDosDesbloqueado = PlayerPrefs.GetInt(ClaveNivelDosDesbloqueado, 0) == 1;
        FormaPrecisaDesbloqueada = PlayerPrefs.GetInt(ClaveFormaPrecisaDesbloqueada, 0) == 1;
        CutsceneCierreVista = PlayerPrefs.GetInt(ClaveCutsceneCierreVista, 0) == 1;
    }

    /// <summary>Idempotente — llamarlo de nuevo en partidas siguientes no hace nada raro.</summary>
    public static void DesbloquearNivel2()
    {
        if (NivelDosDesbloqueado) return;
        NivelDosDesbloqueado = true;
        PlayerPrefs.SetInt(ClaveNivelDosDesbloqueado, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Idempotente, igual que DesbloquearNivel2 (mismo momento de disparo, flag separado).</summary>
    public static void DesbloquearFormaPrecisa()
    {
        if (FormaPrecisaDesbloqueada) return;
        FormaPrecisaDesbloqueada = true;
        PlayerPrefs.SetInt(ClaveFormaPrecisaDesbloqueada, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Idempotente — ver CutsceneCierreVista.</summary>
    public static void MarcarCutsceneCierreVista()
    {
        if (CutsceneCierreVista) return;
        CutsceneCierreVista = true;
        PlayerPrefs.SetInt(ClaveCutsceneCierreVista, 1);
        PlayerPrefs.Save();
    }

    // --- Aritmética pura (sin PlayerPrefs) ---

    /// <summary>Esencia ganada al morir, según lo lejos que llegaste esta partida. Placeholder — se retunea con telemetría real.</summary>
    public static int CalcularRecompensa(float tiempo, int comboMaximo, int nucleosMaximo) =>
        Mathf.RoundToInt(tiempo * 0.4f + comboMaximo * 0.25f + nucleosMaximo * 0.3f);

    static readonly int[] CostosNucleo = { 30, 60, 100, 150, 220 };
    static readonly int[] CostosCrecimiento = { 30, 60, 100, 150, 220 };
    static readonly int[] CostosFever = { 40, 80, 130, 190, 260 };
    static readonly int[] CostosGemas = { 25, 50, 85, 130, 190 };

    static int[] TablaCostos(string id) => id switch
    {
        "nucleo" => CostosNucleo,
        "crecimiento" => CostosCrecimiento,
        "fever" => CostosFever,
        "gemas" => CostosGemas,
        _ => null,
    };

    static int NivelDe(string id) => id switch
    {
        "nucleo" => NivelNucleo,
        "crecimiento" => NivelCrecimiento,
        "fever" => NivelFever,
        "gemas" => NivelGemas,
        _ => 0,
    };

    /// <summary>Costo para subir del nivel actual al siguiente, o -1 si ya está en NivelMaximo.</summary>
    public static int CostoNivel(string id, int nivelActual)
    {
        var tabla = TablaCostos(id);
        if (tabla == null || nivelActual >= NivelMaximo) return -1;
        return tabla[nivelActual];
    }

    /// <summary>El valor real a usar en juego: base + lo comprado, topeado — nunca sobreescribe baseValor.</summary>
    public static float ValorEfectivo(string id, float baseValor, float incrementoPorNivel, float tope) =>
        Mathf.Min(tope, baseValor + NivelDe(id) * incrementoPorNivel);

    // --- Funciones con efecto (tocan PlayerPrefs, guardan de inmediato) ---

    public static void GanarEsencia(int cantidad)
    {
        if (cantidad <= 0) return;
        Esencia += cantidad;
        PlayerPrefs.SetInt(ClaveEsencia, Esencia);
        PlayerPrefs.Save();
    }

    /// <summary>Intenta comprar el próximo nivel de una mejora. Devuelve false si no alcanza la Esencia o ya está al máximo.</summary>
    public static bool ComprarMejora(string id)
    {
        int nivelActual = NivelDe(id);
        int costo = CostoNivel(id, nivelActual);
        if (costo < 0 || Esencia < costo) return false;

        Esencia -= costo;
        int nuevoNivel = nivelActual + 1;
        string clave = id switch
        {
            "nucleo" => ClaveNivelNucleo,
            "crecimiento" => ClaveNivelCrecimiento,
            "fever" => ClaveNivelFever,
            "gemas" => ClaveNivelGemas,
            _ => null,
        };
        if (clave == null) return false;

        switch (id)
        {
            case "nucleo": NivelNucleo = nuevoNivel; break;
            case "crecimiento": NivelCrecimiento = nuevoNivel; break;
            case "fever": NivelFever = nuevoNivel; break;
            case "gemas": NivelGemas = nuevoNivel; break;
        }
        PlayerPrefs.SetInt(clave, nuevoNivel);
        PlayerPrefs.SetInt(ClaveEsencia, Esencia);
        PlayerPrefs.Save();
        return true;
    }
}
