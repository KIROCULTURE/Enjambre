using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;

/// <summary>
/// Reemplaza CUALQUIER dirección visual tomada antes (Balatro, glitch/CRT
/// con Rubik Glitch + Share Tech Mono, cajas con sombra dura, barra de
/// integridad) por una estética restringida: la partida empieza casi sin
/// personalidad visual y se vuelve más intensa a medida que
/// GameManager.Intensidad sube, en sincro con la dificultad real. Acá solo
/// se resuelve la parte "estática" de eso (tipografía neutra, HUD sin
/// cajas, paneles planos, botones simples) — el color que sí escala con
/// Intensidad vive en Nucleo/Enemigo/Orbe/FondoNebulosa.
///
/// Reejecutable de verdad: al empezar, desarma su propia decoración previa
/// Y la de Phase2Styler (misma lista de nombres generados), recupera el
/// contenido real armado a mano hacia la raíz, y reconstruye de cero.
/// </summary>
public static class EstiloMinimal
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Paleta de UI neutra: nada de cian/rojo saturado. El único color que
    // "vibra" en pantalla es el de las formas de juego, que ya escala con
    // Intensidad — la interfaz se queda quieta y discreta a propósito.
    static readonly Color ColorTexto = new Color(0.90f, 0.90f, 0.88f);
    static readonly Color ColorSub = new Color(0.55f, 0.56f, 0.60f);
    static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.06f, 0.82f);
    static readonly Color FondoOscuro = new Color(0.02f, 0.02f, 0.03f, 0.78f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);
    // El Fever es un sistema aparte (premia racha de combos, no escala con
    // Intensidad), así que tiene su propio acento fijo en vez de un
    // apagado→vívido: un magenta cálido que no se confunde con el ámbar
    // de "crecimiento" ni el amarillo de "combo"/orbe de velocidad.
    static readonly Color ColorFever = new Color(1f, 0.35f, 0.65f);
    // Núcleos activos = la vida del enjambre: un azul vivo que hace de link
    // visual directo con el color real del sprite del núcleo (ver
    // AplicarSpriteNucleo.cs), para que el HUD se lea como "esto sos vos".
    static readonly Color ColorVida = new Color(0.35f, 0.75f, 1f);
    // El único color saturado que se permite fuera del gameplay: el título
    // (ver EstilizarTitulo), un rojo sangre apagado a tono con Pirata One
    // — el resto de la interfaz se queda neutra a propósito.
    static readonly Color ColorTitulo = new Color(0.72f, 0.16f, 0.2f);
    // Un tono apenas más claro que el panel de fondo: es lo que hace que
    // la caja de controles se lea como una "tarjeta" propia adentro del
    // menú, sin agregar bordes ni sombras.
    static readonly Color ColorCajaControles = new Color(0.11f, 0.11f, 0.13f, 0.9f);
    // Celeste — la Esencia (moneda de las mejoras permanentes) es su
    // propio concepto, no debe leerse como el mismo azul de ColorVida
    // (núcleos) ni como ningún otro color de gameplay ya en uso.
    static readonly Color ColorEsencia = new Color(0.55f, 0.85f, 1f);

    static TMP_FontAsset fSistema;
    // Fuente con personalidad para el texto de Fever (Bangers, estilo
    // cómic — ver cita APA 7 en Créditos), distinta de la tipografía
    // neutra que usa el resto del HUD a propósito.
    static TMP_FontAsset fFever;
    // Fuente gótica/dramática para los títulos (Pirata One — inspirada en
    // Vampire Survivors, ver cita APA 7 en Créditos). Es EL ÚNICO lugar
    // del juego con esta personalidad — el resto de la interfaz se queda
    // neutra a propósito (ver EstilizarTitulo).
    static TMP_FontAsset fTitulo;

    // Todo lo que este script (o Phase2Styler, antes) generó y hay que
    // tirar antes de reconstruir. Si una segunda pasada no borrara esto,
    // quedaría el estilo viejo mezclado con el nuevo dentro de la misma
    // jerarquía.
    static readonly string[] NombresGenerados = {
        "Fondo", "Caja", "Chip_Tiempo", "Chip_Nucleos", "Chip_Record", "Barra_Fondo", "Creditos"
    };

    // Contenido real armado a mano (botones, textos que otros scripts
    // rellenan en runtime, el slider de volumen) que hay que rescatar
    // hacia la raíz antes de borrar las cajas viejas, en vez de perderlo.
    // Nota: "_Bar" (la barra de integridad vieja) queda afuera a propósito
    // — esta pasada la elimina para siempre, no la conserva.
    static readonly string[] NombresContenidoReal = {
        "_PlayButton", "_RetryButton", "_MenuButton", "T_Resultado", "T_Record", "T_Time", "T_Cores",
        "BotonCreditos", "BotonInstrucciones", "BotonSalir", "FilaVolumen"
    };

    [MenuItem("Enjambre/Aplicar Estilo (Escalada Progresiva)")]
    public static void Aplicar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null)
        {
            Debug.LogError("EstiloMinimal: no encontré 'LiberationSans SDF' (la fuente por defecto de TMP).");
            return;
        }
        // Homoarakhn (1001fonts.com, licencia FFC gratis para uso comercial)
        // en vez de Bangers — mismo rol (texto de Fever, HUD con energía),
        // otra procedencia (ver Créditos).
        fFever = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Homoarakhn SDF.asset");
        if (fFever == null)
            Debug.LogWarning("EstiloMinimal: no encontré 'Homoarakhn SDF' (corré primero Enjambre/Generar Font Asset Homoarakhn) — T_Fever se queda con la fuente del sistema.");
        // English Towne (dafont.com, Dieter Steffmann, 100% gratis) en vez
        // de Pirata One — mismo rol (título gótico/dramático), otra
        // procedencia (ver Créditos).
        fTitulo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/EnglishTowne SDF.asset");
        if (fTitulo == null)
            Debug.LogWarning("EstiloMinimal: no encontré 'EnglishTowne SDF' (corré primero Enjambre/Generar Font Asset English Towne) — los títulos se quedan con la fuente del sistema.");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        var hud = raices.First(g => g.name == "HUD").transform;
        var panelMenu = raices.First(g => g.name == "Panel Menú").transform;
        var panelFin = raices.First(g => g.name == "Panel Fin").transform;
        var panelCreditosGo = raices.FirstOrDefault(g => g.name == "Panel Créditos");

        // GameManager.hud apunta al componente HudController (vive en el
        // hijo chico "Hud_Controller", sin visual propio); lo que hay que
        // prender/apagar para que el HUD se vea de verdad es la raíz.
        gm.hudRoot = hud.gameObject;
        EditorUtility.SetDirty(gm);

        var spriteMouse = ImportarIconoUI("Assets/Sprites/IconoMouse.png");
        var spriteFlechas = ImportarIconoUI("Assets/Sprites/IconoFlechas.png");
        var spriteEnjambre = ImportarIconoUI("Assets/Sprites/IconoEnjambre.png");

        // HUD y Panel Fin arrancan desactivados en la escena guardada; armar
        // RectTransforms bajo un Canvas inactivo los deja con localScale
        // (0,0,0) hasta el próximo layout pass, que no corre en batch mode.
        // Los activamos mientras se arman y los dejamos como estaban antes
        // de guardar.
        bool hudEstabaActivo = hud.gameObject.activeSelf;
        bool panelFinEstabaActivo = panelFin.gameObject.activeSelf;
        hud.gameObject.SetActive(true);
        panelFin.gameObject.SetActive(true);

        DesarmarDecoracionPrevia(hud);
        DesarmarDecoracionPrevia(panelMenu);
        DesarmarDecoracionPrevia(panelFin);

        UsarCamaraEnCanvas(hud.GetComponent<Canvas>(), cam, 0);
        UsarCamaraEnCanvas(panelMenu.GetComponent<Canvas>(), cam, 10);
        UsarCamaraEnCanvas(panelFin.GetComponent<Canvas>(), cam, 10);

        EstilarHud(hud);
        EstilarPanelMenu(panelMenu, gm, spriteMouse, spriteFlechas, spriteEnjambre);
        EstilarPanelFin(panelFin, gm);
        if (panelCreditosGo != null) EstilarPanelCreditos(panelCreditosGo.transform);
        AplicarFuentePopupPrefab();

        hud.gameObject.SetActive(hudEstabaActivo);
        panelFin.gameObject.SetActive(panelFinEstabaActivo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("EstiloMinimal: listo.");
    }

    static void DesarmarDecoracionPrevia(Transform raiz)
    {
        foreach (var nombre in NombresGenerados)
        {
            var t = BuscarHijo(raiz, nombre);
            if (t == null) continue;

            var hijosReales = t.GetComponentsInChildren<Transform>(true)
                .Where(h => h != t && NombresContenidoReal.Contains(h.name))
                .ToList();
            foreach (var h in hijosReales)
            {
                h.SetParent(raiz, false);
                h.localScale = Vector3.one;
            }
            Object.DestroyImmediate(t.gameObject);
        }
    }

    static void UsarCamaraEnCanvas(Canvas c, Camera cam, int orden)
    {
        c.renderMode = RenderMode.ScreenSpaceCamera;
        c.worldCamera = cam;
        c.planeDistance = 5f;
        c.overrideSorting = true;
        c.sortingOrder = orden;
    }

    // ---------- HUD: texto plano en las esquinas, sin cajas ni barra ----------

    static void EstilarHud(Transform hud)
    {
        UbicarTextoHud(hud, "T_Time", new Vector2(0f, 1f), new Vector2(24, -22), TextAlignmentOptions.TopLeft, 26, ColorTexto);
        UbicarTextoHud(hud, "T_Record", new Vector2(1f, 1f), new Vector2(-24, -22), TextAlignmentOptions.TopRight, 26, ColorSub);

        // Núcleos activos = la vida del enjambre (si llega a 0, se acabó la
        // partida) — antes era chico y discreto porque "la masa ya se ve a
        // simple vista", pero es el dato más importante del HUD y tenía que
        // leerse como tal: ahora es el texto más grande arriba, en el azul
        // que lo liga visualmente al sprite real del núcleo.
        UbicarTextoHud(hud, "T_Cores", new Vector2(0.5f, 1f), new Vector2(0, -36), TextAlignmentOptions.Top, 34, ColorVida);

        // T_Fever no existía a mano en la escena — GameManager lo prende y
        // apaga solo (HudController.Actualizar lo desactiva sin Fever
        // activo), así que arranca oculto en el estado guardado también.
        // Fuente con personalidad (Bangers, estilo cómic) en vez de la
        // tipografía neutra del resto del HUD, más grande para que se note
        // — HudController la anima (pulso + color que gira) mientras dura.
        var fever = AsegurarTextoHud(hud, "T_Fever", new Vector2(0.5f, 1f), new Vector2(0, -88), TextAlignmentOptions.Top, 30, ColorFever, "FEVER");
        if (fFever != null) fever.font = fFever;
        fever.characterSpacing = 1;
        fever.gameObject.SetActive(false);

        // T_ModoBonus (el aviso de "MODO BONUS" tras el final de demo) ya
        // no existe — el final de demo se reemplazó por la cutscene del
        // Nivel 2 (ver GameManager.EntrarCutsceneNivel2). Se destruye acá
        // si quedó de una corrida anterior del script, en vez de dejarlo
        // huérfano en la escena.
        var modoBonusViejo = BuscarHijo(hud, "T_ModoBonus");
        if (modoBonusViejo != null) Object.DestroyImmediate(modoBonusViejo.gameObject);

        var hc = hud.GetComponentInChildren<HudController>(true);
        if (hc != null) { hc.textoFever = fever; EditorUtility.SetDirty(hc); }
    }

    static void UbicarTextoHud(Transform hud, string nombre, Vector2 ancla, Vector2 pos, TextAlignmentOptions align, float tam, Color color)
    {
        var texto = BuscarHijo(hud, nombre);
        if (texto == null) { Debug.LogError("EstiloMinimal: no encontré " + nombre + " en HUD"); return; }
        EstilizarTextoHud(texto.GetComponent<RectTransform>(), texto.GetComponent<TMP_Text>(), hud, ancla, pos, align, tam, color);
    }

    // Igual que UbicarTextoHud, pero crea el TMP_Text si todavía no
    // existe en la escena (para indicadores nuevos como T_Fever que no
    // vienen armados a mano).
    static TMP_Text AsegurarTextoHud(Transform hud, string nombre, Vector2 ancla, Vector2 pos, TextAlignmentOptions align, float tam, Color color, string textoInicial)
    {
        var t = BuscarHijo(hud, nombre);
        var tmp = t != null ? t.GetComponent<TMP_Text>() : CrearTextoEn(hud, nombre, textoInicial, tam, color, align, pos, new Vector2(220, 34));
        EstilizarTextoHud(tmp.GetComponent<RectTransform>(), tmp, hud, ancla, pos, align, tam, color);
        return tmp;
    }

    static void EstilizarTextoHud(RectTransform rt, TMP_Text tmp, Transform hud, Vector2 ancla, Vector2 pos, TextAlignmentOptions align, float tam, Color color)
    {
        rt.SetParent(hud, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(220, 40);

        tmp.font = fSistema;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 0;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontSize = tam;
        tmp.enableAutoSizing = false;
    }

    // ---------- Panel Menú ----------

    static void EstilarPanelMenu(Transform raiz, GameManager gm, Sprite spriteMouse, Sprite spriteFlechas, Sprite spriteEnjambre)
    {
        // Mucho más tenue que el 0.78 que usan Pausa/Instrucciones/Créditos
        // (esos SÍ quieren tapar bien el fondo, se abren sobre una partida
        // en curso) — acá el fondo es FondoNebulosa.cs (nebulosa +
        // estrellas titilando, ya procedural, ver ese script), y taparlo
        // casi del todo era literalmente la razón principal por la que el
        // menú se sentía "una caja gris con botones": había un rectángulo
        // casi opaco encima de un fondo que ya existía y nunca se veía.
        AgregarFondoOscuro(raiz, new Color(FondoOscuro.r, FondoOscuro.g, FondoOscuro.b, 0.25f));

        // "Caja" se queda como ancla de layout (todo lo de abajo sigue
        // posicionado relativo a ella, cero matemática nueva) pero ya NO
        // se pinta — nada de panel rectangular de fondo detrás del título/
        // botones, todo flota directo sobre la nebulosa. Ídem
        // "CajaControles" más abajo.
        var caja = CrearPanelPlano("Caja", raiz, new Vector2(420, 400));
        caja.GetComponent<Image>().color = Color.clear;

        // Recomposición real (no solo materiales): el título sube y crece
        // más (60 -> 68), la caja de controles (mouse/flechas/"Mover")
        // SE SACA del todo — ya existe una pantalla de Instrucciones
        // dedicada a explicar los controles, tenerlo duplicado acá era
        // puro relleno visual — y los 4 botones secundarios pasan de un
        // grid 2x2 apretado a una sola fila angosta bien abajo, dejando un
        // vacío real en el medio (Jugar, grande, respirando) en vez de un
        // stack vertical apretado de arriba a abajo.
        var tituloSombra = CrearTexto("TituloSombra", caja, "Enjambre", 68, new Color(0f, 0f, 0f, 0.55f),
            TextAlignmentOptions.Center, new Vector2(3, 146), new Vector2(460, 90));
        EstilizarTitulo(tituloSombra, "Enjambre");
        tituloSombra.color = new Color(0f, 0f, 0f, 0.55f); // EstilizarTitulo pisa el color con ColorTitulo — se repone después, la sombra tiene que quedar oscura, no roja
        AgregarTitulo(caja, "Titulo", "Enjambre", new Vector2(0, 150), 68);

        // Un enjambre de verdad (Delapouite, CC BY 3.0 — ver Créditos)
        // flanqueando el título en vez de solo texto: tenue y chico, un
        // guiño al nombre del juego sin competir con la tipografía.
        IconoEnCaja(caja, "IconoEnjambreIzq", spriteEnjambre, ColorTitulo, new Vector2(-192, 153), new Vector2(34, 34));
        var iconoDer = IconoEnCaja(caja, "IconoEnjambreDer", spriteEnjambre, ColorTitulo, new Vector2(192, 153), new Vector2(34, 34));
        iconoDer.transform.localScale = new Vector3(-1f, 1f, 1f); // espejado, para que "abrace" el título desde el otro lado

        // Línea de acento bajo el título — antes era un separador gris
        // discreto (lenguaje de "borde de panel"); ahora es un acento en
        // el mismo rojo del título, más protagonista sin agregar más texto.
        var divisor = CrearImagen("Divisor", caja, ColorTitulo);
        var rtDiv = divisor.rectTransform;
        rtDiv.anchorMin = rtDiv.anchorMax = new Vector2(0.5f, 0.5f);
        rtDiv.anchoredPosition = new Vector2(0, 100);
        rtDiv.sizeDelta = new Vector2(120, 3);

        // La caja de controles (mouse/flechas/"Mover") que vivía acá se
        // eliminó — ver comentario de arriba. spriteMouse/spriteFlechas
        // quedan sin usar en este método (Instrucciones los sigue usando).

        var jugar = BuscarHijo(raiz, "_PlayButton");
        if (jugar != null)
        {
            // Más grande que antes (200x38 -> 260x48) y centrado en el
            // vacío que dejó sacar la caja de controles — es el único
            // botón primario, tiene que leerse como tal de lejos.
            ReubicarBajoPanel(jugar, caja, new Vector2(0, 15), new Vector2(260, 48));
            RestilarBotonExistente(jugar, "Jugar");
            var labelJugar = jugar.GetComponentInChildren<TMP_Text>();
            if (labelJugar != null) labelJugar.fontSize = 20;
        }

        // Los 4 secundarios en una sola fila angosta, bien abajo — no
        // compiten con Jugar por atención, se leen como una "repisa" de
        // opciones secundarias, mismo lenguaje que la fila de 3 botones
        // del pack de referencia (ahí son 3 en columna; acá son 4, más
        // angostos, en fila, para no alargar el menú de más).
        var tamSecundario = new Vector2(95, 34);
        const float ySecundaria = -62;
        float[] xSecundaria = { -159, -53, 53, 159 };

        var instrucciones = BuscarHijo(raiz, "BotonInstrucciones");
        if (instrucciones == null)
        {
            var boton = CrearBotonFlat("BotonInstrucciones", caja, new Vector2(xSecundaria[0], ySecundaria), tamSecundario, "Instrucciones", 11);
            UnityEventTools.AddPersistentListener(boton.onClick, gm.AbrirInstrucciones);
        }
        else
        {
            ReubicarBajoPanel(instrucciones, caja, new Vector2(xSecundaria[0], ySecundaria), tamSecundario);
            RestilarBotonExistente(instrucciones, "Instrucciones");
            var l = instrucciones.GetComponentInChildren<TMP_Text>();
            if (l != null) l.fontSize = 11;
        }

        var creditos = BuscarHijo(raiz, "BotonCreditos");
        if (creditos != null)
        {
            ReubicarBajoPanel(creditos, caja, new Vector2(xSecundaria[1], ySecundaria), tamSecundario);
            RestilarBotonExistente(creditos, "Créditos");
            var l = creditos.GetComponentInChildren<TMP_Text>();
            if (l != null) l.fontSize = 13;
        }

        var mejoras = BuscarHijo(raiz, "BotonMejoras");
        if (mejoras == null)
        {
            var boton = CrearBotonFlat("BotonMejoras", caja, new Vector2(xSecundaria[2], ySecundaria), tamSecundario, "Mejoras", 13);
            UnityEventTools.AddPersistentListener(boton.onClick, gm.AbrirMejoras);
        }
        else
        {
            ReubicarBajoPanel(mejoras, caja, new Vector2(xSecundaria[2], ySecundaria), tamSecundario);
            RestilarBotonExistente(mejoras, "Mejoras");
            var l = mejoras.GetComponentInChildren<TMP_Text>();
            if (l != null) l.fontSize = 13;
        }

        var salir = BuscarHijo(raiz, "BotonSalir");
        if (salir == null)
        {
            var boton = CrearBotonFlat("BotonSalir", caja, new Vector2(xSecundaria[3], ySecundaria), tamSecundario, "Salir", 13);
            UnityEventTools.AddPersistentListener(boton.onClick, gm.BotonSalir);
        }
        else
        {
            ReubicarBajoPanel(salir, caja, new Vector2(xSecundaria[3], ySecundaria), tamSecundario);
            RestilarBotonExistente(salir, "Salir");
            var l = salir.GetComponentInChildren<TMP_Text>();
            if (l != null) l.fontSize = 13;
        }

        // BotonNivel2 (el atajo condicional viejo, solo visible tras
        // desbloquear) se reemplazó por "Niveles" — un selector siempre
        // visible con los dos niveles elegibles (ver PantallaNiveles.cs y
        // el comentario en GameManager.panelNiveles). Se destruye acá si
        // quedó de una corrida anterior del script.
        var nivel2Viejo = caja.Find("BotonNivel2");
        if (nivel2Viejo != null) Object.DestroyImmediate(nivel2Viejo.gameObject);

        // Mismo escalón visual que tenía Nivel 2 (entre Jugar y los
        // secundarios) — siempre visible, ya no depende de MetaProgreso.
        var niveles = BuscarHijo(raiz, "BotonNiveles");
        if (niveles == null)
        {
            var boton = CrearBotonFlat("BotonNiveles", caja, new Vector2(0, -27), new Vector2(170, 26), "Niveles", 14);
            UnityEventTools.AddPersistentListener(boton.onClick, gm.AbrirNiveles);
            niveles = boton.transform;
        }
        else
        {
            ReubicarBajoPanel(niveles, caja, new Vector2(0, -27), new Vector2(170, 26));
            RestilarBotonExistente(niveles, "Niveles");
            var l = niveles.GetComponentInChildren<TMP_Text>();
            if (l != null) l.fontSize = 14;
        }

        // Volumen: ya no está en el flujo vertical principal (competía con
        // Jugar/los secundarios por atención) — ahora es un detalle chico
        // abajo del todo, como el número de versión/íconos sociales del
        // pack de referencia.
        var filaVolumen = BuscarHijo(raiz, "FilaVolumen");
        if (filaVolumen != null)
        {
            ReubicarBajoPanel(filaVolumen, caja, new Vector2(0, -118), filaVolumen.GetComponent<RectTransform>().sizeDelta);
            RestilarFilaVolumen(filaVolumen);
        }
    }

    static Image IconoEnCaja(Transform padre, string nombre, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        var img = CrearImagen(nombre, padre, color);
        img.sprite = sprite;
        img.preserveAspect = true;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    // ---------- Panel Fin ----------

    static void EstilarPanelFin(Transform raiz, GameManager gm)
    {
        AgregarFondoOscuro(raiz);

        // Más alta que antes: ahora T_Resultado/T_Record muestran 3 líneas
        // cada uno (tiempo/combo/núcleos de la partida + sus récords), no
        // una sola línea.
        var caja = CrearPanelPlano("Caja", raiz, new Vector2(380, 400));
        AgregarTitulo(caja, "Titulo", "Disuelto", new Vector2(0, 160), 34);

        ReubicarTextoEnCaja(raiz, caja, "T_Resultado", new Vector2(0, 80), new Vector2(340, 85), 13, ColorTexto);
        ReubicarTextoEnCaja(raiz, caja, "T_Record", new Vector2(0, -35), new Vector2(340, 85), 12, ColorSub);

        // Cuánta Esencia (moneda de las mejoras permanentes, ver
        // MetaProgreso/PantallaMejoras) dejó esta partida — no existía
        // antes de la meta-progresión, así que se crea directo (no hay
        // GameObject viejo que reubicar, a diferencia de T_Resultado/Record).
        var esenciaGo = BuscarHijo(raiz, "T_Esencia");
        TMP_Text textoEsencia = esenciaGo != null ? esenciaGo.GetComponent<TMP_Text>() : null;
        if (textoEsencia == null)
            textoEsencia = CrearTexto("T_Esencia", caja, "+0 Esencia", 15, ColorEsencia,
                TextAlignmentOptions.Center, new Vector2(0, -105), new Vector2(300, 28));
        gm.textoEsenciaGanada = textoEsencia;

        // Tres botones angostos en vez de 2, para que entre Mejoras sin
        // agrandar el panel (Reintentar/Mejoras/Menú).
        var reintentar = BuscarHijo(raiz, "_RetryButton");
        if (reintentar != null)
        {
            ReubicarBajoPanel(reintentar, caja, new Vector2(-120, -165), new Vector2(110, 36));
            RestilarBotonExistente(reintentar, "Reintentar");
            // "Reintentar" es la palabra más larga de las 3 — con el botón
            // angosto (110, para que entren 3 en la fila) se salía del
            // borde con el tamaño de fuente que traía de antes (pensado
            // para un botón de 140).
            var labelReintentar = reintentar.GetComponentInChildren<TMP_Text>();
            if (labelReintentar != null) labelReintentar.fontSize = 13;
        }

        var mejoras = BuscarHijo(raiz, "BotonMejorasFin");
        if (mejoras == null)
        {
            var boton = CrearBotonFlat("BotonMejorasFin", caja, new Vector2(0, -165), new Vector2(110, 36), "Mejoras", 15);
            UnityEventTools.AddPersistentListener(boton.onClick, gm.AbrirMejoras);
        }
        else
        {
            ReubicarBajoPanel(mejoras, caja, new Vector2(0, -165), new Vector2(110, 36));
            RestilarBotonExistente(mejoras, "Mejoras");
        }

        var menu = BuscarHijo(raiz, "_MenuButton");
        if (menu == null)
        {
            var boton = CrearBotonFlat("_MenuButton", caja, new Vector2(120, -165), new Vector2(110, 36), "Menú", 15);
            UnityEventTools.AddPersistentListener(boton.onClick, gm.BotonVolverAlMenu);
        }
        else
        {
            ReubicarBajoPanel(menu, caja, new Vector2(120, -165), new Vector2(110, 36));
            RestilarBotonExistente(menu, "Menú");
        }
    }

    // ---------- Panel Créditos (armado por PantallaCreditos.cs) ----------

    static void EstilarPanelCreditos(Transform raiz)
    {
        var fondo = BuscarHijo(raiz, "Fondo");
        if (fondo != null)
        {
            var img = fondo.GetComponent<Image>();
            if (img != null) img.color = FondoOscuro;
        }

        var caja = BuscarHijo(raiz, "Caja");
        if (caja == null) return;

        // La caja vieja tenía Sombra+Borde+Relleno; nos quedamos solo con un
        // panel plano.
        var sombra = caja.Find("Sombra");
        var borde = caja.Find("Borde");
        if (sombra != null) Object.DestroyImmediate(sombra.gameObject);
        if (borde != null) Object.DestroyImmediate(borde.gameObject);
        var relleno = caja.Find("Relleno");
        if (relleno != null)
        {
            var img = relleno.GetComponent<Image>();
            if (img != null) img.color = PanelBg;
            var rt = relleno.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // OJO con la altura: el Canvas usa referenceResolution 960x600,
        // pero CanvasScaler (match 0.5) hace que la altura EFECTIVA en
        // unidades de canvas caiga bastante por debajo de 600 en pantallas
        // anchas de verdad (16:9 real ronda los ~570, no 600) — un panel
        // pegado al límite de 600 se corta arriba/abajo en el juego real
        // aunque se vea bien en una captura de prueba con otra relación de
        // aspecto. Por eso 520 de alto acá, con margen real de sobra.
        var cajaRt = caja.GetComponent<RectTransform>();
        // 540 (no 520): la cita de Bangers se sumó a la de Kenney en el
        // mismo bloque de abajo y necesitaba un poco más de aire. 540 ya
        // está probado seguro en este mismo techo real (ver
        // PantallaInstrucciones.cs, que usa la misma altura).
        cajaRt.sizeDelta = new Vector2(520, 540);

        // Título con aberración cromática (Titulo_R/Titulo_C) -> un solo texto plano.
        var tituloR = caja.Find("Titulo_R");
        var tituloC = caja.Find("Titulo_C");
        if (tituloR != null) Object.DestroyImmediate(tituloR.gameObject);
        if (tituloC != null) Object.DestroyImmediate(tituloC.gameObject);
        var titulo = caja.Find("Titulo")?.GetComponent<TMP_Text>();
        if (titulo != null)
        {
            EstilizarTitulo(titulo, "Créditos");
            titulo.fontSize = 28;
            titulo.enableWordWrapping = true;
            ReubicarBajoPanel(titulo.transform, caja, new Vector2(0, 218), new Vector2(440, 48));
        }

        var equipo = caja.Find("Equipo")?.GetComponent<TMP_Text>();
        if (equipo != null)
        {
            RestilarTextoPlano(caja, "Equipo", ColorTexto);
            ReubicarBajoPanel(equipo.transform, caja, new Vector2(0, 150), new Vector2(440, 72));
            equipo.richText = true;
            equipo.fontSize = 12;
            equipo.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorSub)}>Desarrollado por:</color>\nAlejandro Gómez\nDarío Valdebenito\nBastián Allende";
        }

        // Datos de la game jam para la que se hizo el juego.
        var gameJam = AsegurarTextoEnCaja(caja, "GameJam", new Vector2(0, 85), new Vector2(460, 34), 10, ColorSub);
        gameJam.text = "Hecho para la Game Jam de la Universidad Gabriela Mistral 2026\nTaller de Videojuegos 2";

        // El concepto detrás del juego — su propia línea, con un poco más
        // de peso que el resto del texto secundario.
        var concepto = AsegurarTextoEnCaja(caja, "Concepto", new Vector2(0, 50), new Vector2(460, 24), 12, ColorTexto);
        concepto.fontStyle = FontStyles.Italic;
        concepto.text = "Concepto: la muerte no es el fin";

        // Un solo header + un solo bloque de citas para TODOS los assets
        // externos (música, íconos, tipografías) — separarlos en varias
        // secciones (como antes) se quedaba sin aire cada vez que se
        // sumaba una cita nueva; consolidado entra cómodo incluso con las
        // 6 citas actuales.
        var assetsHeader = AsegurarTextoEnCaja(caja, "MusicaHeader", new Vector2(0, 15), new Vector2(420, 22), 12, ColorSub);
        assetsHeader.text = "Assets externos";

        // Cita1/Cita2 (música) y IconosHeader (el header viejo, separado)
        // quedan sin uso — todo su contenido se movió al bloque único de
        // abajo. Se ocultan en vez de borrarse por si algún día conviene
        // volver a separarlos.
        var cita1Old = caja.Find("Cita1");
        if (cita1Old != null) cita1Old.gameObject.SetActive(false);
        var cita2Old = caja.Find("Cita2");
        if (cita2Old != null) cita2Old.gameObject.SetActive(false);
        var iconosHeaderOld = caja.Find("IconosHeader");
        if (iconosHeaderOld != null) iconosHeaderOld.gameObject.SetActive(false);

        var citaTodo = AsegurarTextoEnCaja(caja, "CitaIconos", new Vector2(0, -72), new Vector2(460, 175), 8.5f, ColorSub);
        citaTodo.text =
            "van Meijl, K. (s.f.). <i>Space main theme</i> [pista de audio]. SoundCloud.\nhttps://soundcloud.com/kyra-van-meijl/space-main-theme\n" +
            "van Meijl, K. (s.f.). <i>Exploding sun</i> [pista de audio]. SoundCloud.\nhttps://soundcloud.com/kyra-van-meijl/exploding-sun\n" +
            "van Meijl, K. (s.f.). <i>AI Malware</i> [pista de audio]. SoundCloud.\nhttps://soundcloud.com/kyra-van-meijl/ai-malware\n" +
            // Música de la pelea de Nivel 2 desde el rediseño grande a boss
            // fight sincronizado por tiempo — CC BY 4.0 real (a diferencia
            // de las 3 de arriba, esta SÍ exige mención de la licencia).
            "Van Meijl, K. (2024). <i>Industrial Planet</i> [Canción]. En <i>8-bit Space Music</i>. Kyra's Voice [CC BY 4.0].\nhttps://kyrasvoice.itch.io/8-bit-space-music\n" +
            "Kenney. (s.f.). <i>Input prompts</i> [Paquete de íconos]. Kenney.nl.\nhttps://kenney.nl/assets/input-prompts\n" +
            "Steffmann, D. (2006). <i>English Towne</i> [Tipografía]. DaFont.\nhttps://www.dafont.com/english-towne.font\n" +
            "1001Fonts. (s.f.). <i>Homoarakhn</i> [Tipografía]. 1001Fonts.\nhttps://www.1001fonts.com/homoarakhn-font.html\n" +
            "Delapouite. (s.f.). <i>Ants</i> [Ícono] [CC BY 3.0]. Game-icons.net.\nhttps://game-icons.net/1x1/delapouite/ants.html\n" +
            "LuizMelo. (2020). <i>EVil Wizard 2</i> [Sprites — placeholder del boss] [CC0]. itch.io.\nhttps://luizmelo.itch.io/evil-wizard-2";
        citaTodo.richText = true;
        citaTodo.fontSize = 7f; // 9 citas — se achica un pelín más (y la caja crece un poco) para que entren todas sin cortarse

        // BotonNivel2/BotonReanudar (el atajo a Nivel 2 y el "seguir
        // jugando" del final de demo) ya no existen en esta pantalla — el
        // final de demo se reemplazó por la cutscene del Nivel 2
        // (GameManager.EntrarCutsceneNivel2) y el acceso a Nivel 2 vive
        // solo en el botón del menú principal. Se destruyen acá si
        // quedaron de una corrida anterior del script.
        var botonNivel2Viejo = caja.Find("BotonNivel2");
        if (botonNivel2Viejo != null) Object.DestroyImmediate(botonNivel2Viejo.gameObject);
        var botonReanudarViejo = caja.Find("BotonReanudar");
        if (botonReanudarViejo != null) Object.DestroyImmediate(botonReanudarViejo.gameObject);

        // "Volver" vuelve a ser el único botón, centrado — ya no comparte
        // la fila con "Reanudar".
        var botonVolver = caja.Find("BotonVolver");
        if (botonVolver != null)
        {
            var sombraBoton = caja.Find("BotonVolver_Sombra");
            if (sombraBoton != null) Object.DestroyImmediate(sombraBoton.gameObject);
            ReubicarBajoPanel(botonVolver, caja, new Vector2(0, -228), new Vector2(150, 34));
            RestilarBotonExistente(botonVolver, "Menú");
        }
    }

    // Crea (la primera vez) o reubica/reestila (después) un texto plano
    // dentro de la caja de créditos — a diferencia de RestilarTextoPlano,
    // que asume que el objeto ya existe a mano.
    static TMP_Text AsegurarTextoEnCaja(Transform caja, string nombre, Vector2 pos, Vector2 size, float tam, Color color)
    {
        var t = caja.Find(nombre);
        TMP_Text tmp = t != null ? t.GetComponent<TMP_Text>() : CrearTextoEn(caja, nombre, "", tam, color, TextAlignmentOptions.Center, pos, size);
        ReubicarBajoPanel(tmp.transform, caja, pos, size);
        tmp.font = fSistema;
        tmp.fontStyle = FontStyles.Normal;
        tmp.characterSpacing = 0;
        tmp.color = color;
        tmp.fontSize = tam;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    static void RestilarTextoPlano(Transform padre, string nombre, Color color)
    {
        var t = padre.Find(nombre);
        if (t == null) return;
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp == null) return;
        tmp.font = fSistema;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = color;
        tmp.characterSpacing = 0;
    }

    // ---------- Helpers compartidos ----------

    static void ReubicarBajoPanel(Transform t, Transform caja, Vector2 pos, Vector2 tam)
    {
        var rt = t.GetComponent<RectTransform>();
        rt.SetParent(caja, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    static void ReubicarTextoEnCaja(Transform raiz, Transform caja, string nombre, Vector2 pos, Vector2 size, float tam, Color color)
    {
        var t = BuscarHijo(raiz, nombre);
        if (t == null) return;
        ReubicarBajoPanel(t, caja, pos, size);
        var tmp = t.GetComponent<TMP_Text>();
        tmp.font = fSistema;
        tmp.fontStyle = FontStyles.Normal;
        tmp.characterSpacing = 0;
        tmp.color = color;
        tmp.fontSize = tam;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    // Restila un botón armado a mano (Image + Button + TMP hijo ya
    // conectado a su propio onClick): saca la sombra dura y el "hundido"
    // que la acompañaba, y deja un rectángulo sólido con texto plano.
    static void RestilarBotonExistente(Transform boton, string texto)
    {
        var img = boton.GetComponent<Image>();
        if (img != null)
        {
            img.color = BotonFill;
            // Pastilla (ver ShapeFactory) en vez del cuadrado plano de
            // antes — pedido explícito de un look más pulido/menos "caja
            // con esquinas rectas" para el menú principal, aplicado acá
            // (el paso de reestilo compartido) para que TODOS los botones
            // del juego queden consistentes, no solo los del menú.
            img.sprite = ShapeFactory.Pastilla(64, 20, Color.white);
            img.type = Image.Type.Sliced;
        }

        var bh = boton.GetComponent<BotonHundido>();
        if (bh != null) Object.DestroyImmediate(bh);

        var sombra = boton.parent.Find(boton.name + "_Sombra");
        if (sombra != null) Object.DestroyImmediate(sombra.gameObject);

        var label = boton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.font = fSistema;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 0;
            label.color = BotonTexto;
            label.text = texto;
        }
    }

    static void RestilarFilaVolumen(Transform fila)
    {
        var etiqueta = fila.Find("Etiqueta")?.GetComponent<TMP_Text>();
        if (etiqueta != null) { etiqueta.font = fSistema; etiqueta.color = ColorSub; etiqueta.fontStyle = FontStyles.Normal; etiqueta.text = "Volumen"; }

        var slider = fila.Find("SliderVolumen");
        if (slider == null) return;
        var fill = slider.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fill != null) fill.color = ColorTexto;
        var handle = slider.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
        if (handle != null) handle.color = ColorTexto;
        var fondo = slider.Find("Fondo")?.GetComponent<Image>();
        if (fondo != null) fondo.color = PanelBg;
    }

    static Button CrearBotonFlat(string nombre, Transform padre, Vector2 pos, Vector2 tam, string texto, float fontSize)
    {
        var img = CrearImagen(nombre, padre, BotonFill);
        img.sprite = ShapeFactory.Pastilla(64, 20, Color.white);
        img.type = Image.Type.Sliced;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var boton = img.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;

        var label = CrearTextoEn(rt, "Texto", texto, fontSize, BotonTexto, TextAlignmentOptions.Center, Vector2.zero, tam);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        return boton;
    }

    // Panel plano único (sin borde ni sombra) para paneles/cajas de fondo
    // — a diferencia de los botones (ver Pastilla en RestilarBotonExistente/
    // CrearBotonFlat), esto sigue siendo un rectángulo recto: el pedido de
    // esquinas redondeadas fue específicamente sobre los botones, no sobre
    // cada panel de fondo del juego.
    static Transform CrearPanelPlano(string nombre, Transform padre, Vector2 tam)
    {
        var img = CrearImagen(nombre, padre, PanelBg);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = tam;
        return rt;
    }

    static void AgregarFondoOscuro(Transform raiz) => AgregarFondoOscuro(raiz, FondoOscuro);

    static void AgregarFondoOscuro(Transform raiz, Color color)
    {
        var go = new GameObject("Fondo", typeof(RectTransform));
        go.transform.SetParent(raiz, false);
        go.transform.SetAsFirstSibling();
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // Único lugar con mayúsculas + letter-spacing + fuente con personalidad
    // (Pirata One, gótica/dramática — inspirada en Vampire Survivors, ver
    // cita APA 7 en Créditos) y color saturado (ColorTitulo). Todo lo
    // demás usa la fuente del sistema en su caso natural, neutro — la
    // única "voz" fuerte del juego es el título, en cada pantalla donde
    // aparece (Menú, Fin, Créditos).
    static void AgregarTitulo(Transform padre, string nombre, string texto, Vector2 pos, float tam)
    {
        var tmp = CrearTexto(nombre, padre, texto, tam, ColorTexto, TextAlignmentOptions.Center, pos, new Vector2(400, 60));
        EstilizarTitulo(tmp, texto);
    }

    static void EstilizarTitulo(TMP_Text tmp, string texto)
    {
        tmp.font = fTitulo != null ? fTitulo : fSistema;
        tmp.fontStyle = FontStyles.Normal; // Pirata One ya es lo bastante pesada; Bold la deformaba
        tmp.text = texto.ToUpperInvariant();
        tmp.characterSpacing = fTitulo != null ? 2 : 8;
        tmp.color = fTitulo != null ? ColorTitulo : ColorTexto;
    }

    // Los popups en mundo (+MASA, COMBO xN, ¡CRECIMIENTO!, FEVER xN,
    // hitos de tiempo) viven en un prefab aparte (Popup.prefab) que un
    // estilo anterior había dejado en Rubik Glitch — una Google Font con
    // personalidad, justo lo que este pase de estilo evita en todos lados.
    static void AplicarFuentePopupPrefab()
    {
        const string rutaPopup = "Assets/Prefabs/Popup.prefab";
        var prefabRoot = PrefabUtility.LoadPrefabContents(rutaPopup);
        var tmp = prefabRoot.GetComponent<TextMeshPro>();
        if (tmp == null) { Debug.LogError("EstiloMinimal: Popup.prefab no tiene TextMeshPro."); PrefabUtility.UnloadPrefabContents(prefabRoot); return; }

        tmp.font = fSistema;
        tmp.fontStyle = FontStyles.Bold;

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, rutaPopup);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    // Íconos de mouse/flechas (Kenney, CC0 — ver cita en Créditos): se
    // importan como Sprite normal de UI, sin PPU especial (a diferencia
    // de AplicarSpriteNucleo.cs, esto es solo para un Image de UGUI).
    static Sprite ImportarIconoUI(string ruta)
    {
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer == null) { Debug.LogError("EstiloMinimal: no encontré el importer de " + ruta); return null; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (sprite == null) Debug.LogError("EstiloMinimal: no pude cargar el Sprite tras reimportar " + ruta);
        return sprite;
    }

    static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // Ícono de mouse sin arte importado: un cuerpo rectangular sólido más
    // una ruedita circular (el sprite redondo que trae Unity por defecto
    // para sliders/knobs) cerca de la punta — se lee como mouse mucho
    // mejor que una simple línea divisoria.
    static TMP_Text CrearTexto(string nombre, Transform padre, string texto, float tam, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 size) =>
        CrearTextoEn(padre, nombre, texto, tam, color, align, pos, size);

    static TMP_Text CrearTextoEn(Transform padre, string nombre, string texto, float tam, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.font = fSistema;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        if (raiz.name == nombre) return raiz;
        return raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nombre && t != raiz);
    }
}
