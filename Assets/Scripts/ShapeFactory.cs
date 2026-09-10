using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Genera sprites por código (estrella, diamante, anillo, cuadrado,
/// rayo, asteroide) para que el juego funcione sin necesitar ningún
/// asset de arte importado. Cada forma se cachea la primera vez que se
/// pide, así que crear muchos enemigos/orbes/núcleos no repite el
/// trabajo de generar la textura.
///
/// El trazo y el relleno llevan grano y grosor irregular (no un anillo
/// perfectamente uniforme) para que no se lean como una primitiva
/// geométrica con un filtro encima.
/// </summary>
public static class ShapeFactory
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly Color32 negro = new Color32(10, 8, 16, 255);

    public static Sprite Ficha(int tam, Color relleno, Color rellenoInterno, int muescas)
    {
        string clave = $"ficha_{tam}_{relleno}_{rellenoInterno}_{muescas}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = NuevaTextura(tam);
        float cx = tam / 2f, cy = tam / 2f;
        float r = tam * 0.46f, grosor = tam * 0.045f, rInterno = r * 0.5f;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                Color32 c = new Color32(0, 0, 0, 0);
                if (d <= r)
                {
                    c = d > rInterno ? (Color32)relleno : (Color32)rellenoInterno;
                    if (d >= r - grosor) c = negro;
                    if (muescas > 0)
                    {
                        float sector = (Mathf.PI * 2f) / muescas;
                        float mod = Mathf.Repeat(ang + Mathf.PI, sector);
                        if (d > r - grosor * 3.4f && mod < sector * 0.16f) c = negro;
                    }
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    // Blanco liso (sin color propio): el color de rol se aplica en
    // tiempo real vía SpriteRenderer.color (interpolado apagado→vívido
    // según la intensidad de la partida), así una sola forma cacheada
    // sirve para todo el rango en vez de una por color.
    static readonly Color Blanco = Color.white;

    public static Sprite Diamante(int tam)
    {
        string clave = $"diamante_{tam}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = NuevaTextura(tam);
        float cx = tam / 2f, cy = tam / 2f, r = tam * 0.42f, grosorBase = tam * 0.03f;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float m = Mathf.Abs(dx) + Mathf.Abs(dy);
                float ang = Mathf.Atan2(dy, dx);
                float grosor = grosorBase * RuidoTrazo(ang, 3.3f);
                Color32 c = new Color32(0, 0, 0, 0);
                if (m <= r)
                {
                    c = m >= r - grosor ? negro : ConBrillo(ConGrano((Color32)Blanco, x, y, 0.10f), x, y, cx, cy, r);
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    public static Sprite Estrella(int tam, int puntas)
    {
        string clave = $"estrella_{tam}_{puntas}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = NuevaTextura(tam);
        float cx = tam / 2f, cy = tam / 2f;
        float rO = tam * 0.46f, rI = tam * 0.2f;
        var exterior = VerticesEstrella(cx, cy, rO, rI, puntas, 1f, 8.4f);
        var interior = VerticesEstrella(cx, cy, rO, rI, puntas, 0.90f, 1.7f);

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color32 c = new Color32(0, 0, 0, 0);
                if (EnPoligono(p, exterior))
                    c = EnPoligono(p, interior) ? ConBrillo(ConGrano((Color32)Blanco, x, y, 0.10f), x, y, cx, cy, rO) : negro;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    public static Sprite Anillo(int tam, Color color)
    {
        string clave = $"anillo_{tam}_{color}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = NuevaTextura(tam);
        float cx = tam / 2f, cy = tam / 2f, r = tam * 0.47f, grosor = tam * 0.09f;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 c = (d <= r && d >= r - grosor) ? (Color32)color : new Color32(0, 0, 0, 0);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    public static Sprite Cuadrado(int tam, Color color)
    {
        string clave = $"cuadrado_{tam}_{color}";
        if (cache.TryGetValue(clave, out var existente)) return existente;
        var tex = NuevaTextura(tam);
        Color32 c = color;
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
                tex.SetPixel(x, y, c);
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    // Fase 3 del rediseño de Nivel 2: glow radial suave (sin borde duro),
    // para la aura de carga de energía de FormaPrecisa — la única forma de
    // este archivo pensada para superponerse ENCIMA de otro sprite en vez
    // de ser el sprite principal. Bilinear a propósito (a diferencia de
    // todo lo demás acá, que va con Point): un degradé amplio se nota
    // mucho el escalonado a este tamaño, un ícono chico de gameplay no.
    public static Sprite Aura(int tam)
    {
        string clave = $"aura_{tam}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

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
                a = a * a * a; // caída más marcada que el radial de FondoNebulosa — glow puntual, no fondo difuso
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    // Fase 2 del rediseño de Nivel 2: reemplaza al Cuadrado grande de
    // FormaPrecisa por un polígono tipo rombo/diamante con más lados
    // (hexágono/octágono), alargado en vertical para que siga leyéndose
    // como un diamante y no un octágono "gordo". Relleno liso de un solo
    // color (como Cuadrado, no como Diamante/Estrella) A PROPÓSITO:
    // FormaPrecisa lo tiñe entero en tiempo real (ColorActual, flash de
    // golpe, fade a Color.clear al morir) — un sprite con grano/contorno
    // negro horneado no se puede recolorear así.
    public static Sprite Poligono(int tam, int lados, Color color, float alargamientoVertical = 1.3f)
    {
        string clave = $"poligono_{tam}_{lados}_{color}_{alargamientoVertical}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = NuevaTextura(tam);
        float cx = tam / 2f, cy = tam / 2f, r = tam * 0.36f;
        var verts = VerticesPoligono(cx, cy, r, lados, alargamientoVertical);
        Color32 c = color;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                tex.SetPixel(x, y, EnPoligono(p, verts) ? c : new Color32(0, 0, 0, 0));
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    // Pastilla (botón redondeado de UI): un cuadrado con las 4 esquinas
    // recortadas en arco, pensado para usarse con Image.type=Sliced (el
    // border que devuelve el Sprite deja las esquinas con un radio fijo en
    // píxeles sin importar a qué tamaño se estire el botón real). A
    // diferencia del resto de las formas de este archivo (pensadas para
    // sprites de gameplay, Point filtering, sin antialiasing porque un
    // núcleo/orbe siempre se ve chico) esta va con Bilinear y el borde del
    // arco suavizado — un botón de menú se ve mucho más grande en pantalla
    // y un borde en escalera se nota.
    public static Sprite Pastilla(int tam, int radioEsquina, Color color)
    {
        string clave = $"pastilla_{tam}_{radioEsquina}_{color}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        int r = radioEsquina;
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                // Fuera de la franja de esquina (dentro de la cruz central)
                // siempre está lleno; en la franja de esquina se recorta en
                // arco, con 1px de suavizado en el borde para que no quede
                // un escalón duro al escalar.
                float cx = x < r ? r : (x >= tam - r ? tam - r - 1 : -1f);
                float cy = y < r ? r : (y >= tam - r ? tam - r - 1 : -1f);
                float a = 1f;
                if (cx >= 0f && cy >= 0f)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx + 0.5f, cy + 0.5f));
                    a = Mathf.Clamp01(r - d + 0.5f);
                }
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * a));
            }
        }
        tex.Apply();

        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam, 0,
            SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        cache[clave] = spr;
        return spr;
    }

    // Rayo (ícono de velocidad) para el orbe especial amarillo. Este sí
    // se tiñe con un color fijo (no es uno de los 4 roles que escalan
    // con la intensidad), pero igual se genera en blanco para reusar el
    // mismo pipeline de grano/brillo.
    public static Sprite Rayo(int tam)
    {
        string clave = $"rayo_{tam}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = NuevaTextura(tam);
        var puntosNorm = new List<Vector2>
        {
            new Vector2(0.55f, 0.00f),
            new Vector2(0.15f, 0.55f),
            new Vector2(0.40f, 0.55f),
            new Vector2(0.30f, 1.00f),
            new Vector2(0.85f, 0.45f),
            new Vector2(0.55f, 0.45f),
        };
        var exterior = new List<Vector2>();
        var centro = new Vector2(tam / 2f, tam / 2f);
        foreach (var p in puntosNorm) exterior.Add(new Vector2(p.x * tam, p.y * tam));
        var interior = InsetIrregular(exterior, centro, 0.88f, 0.9f);

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                Color32 c = new Color32(0, 0, 0, 0);
                if (EnPoligono(p, exterior))
                    c = EnPoligono(p, interior) ? ConBrillo(ConGrano((Color32)Blanco, x, y, 0.10f), x, y, centro.x, centro.y, tam * 0.42f) : negro;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = spr;
        return spr;
    }

    // Haz de energía (paredes láser — LaserHazard.cs) — antes eran un
    // Cuadrado plano estirado, un solo color sin variación. El color va
    // HORNEADO en la textura (no aplicado en tiempo real como el resto de
    // este archivo) porque LaserHazard estira este mismo sprite con
    // escalas MUY distintas en X e Y según la pared sea horizontal o
    // vertical — un degradé pensado para un solo eje (p.ej. "más brillo
    // en el centro vertical") se leía perfecto en una orientación y
    // aplastado/irreconocible en la otra. Usando la distancia al borde
    // MÁS CERCANO en UV normalizado (min de las 4 distancias) en vez de
    // un eje fijo, el eje que queda comprimido en mundo real (el grosor
    // de la pared) hereda el degradé nítido núcleo-blanco -> borde
    // teñido -> transparente, y el eje que queda estirado (el largo)
    // apenas se nota salvo cerca de las puntas — como un haz de verdad
    // rematando suave en los extremos, en vez de un rectángulo con las
    // puntas cuadradas.
    public static Sprite Haz(int tam, Color color)
    {
        string clave = $"haz_{tam}_{color}";
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float u = (x + 0.5f) / tam, v = (y + 0.5f) / tam;
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); // 0 en el borde, 0.5 en el centro
                float nucleo = Mathf.Clamp01(d * 2f); // 0 borde -> 1 centro
                float alpha = Mathf.SmoothStep(0f, 1f, nucleo);
                // Núcleo blanco-caliente cerca del centro, tiñendo hacia
                // `color` según se aleja — mismo lenguaje HDR que
                // colorApagado/colorVivido (Nucleo/FormaPrecisa).
                Color rgb = Color.Lerp(color, Color.white, Mathf.Clamp01((nucleo - 0.5f) / 0.5f) * 0.9f);
                tex.SetPixel(x, y, new Color(rgb.r, rgb.g, rgb.b, alpha));
            }
        }
        tex.Apply();
        var sprHaz = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        cache[clave] = sprHaz;
        return sprHaz;
    }

    // Ruido determinístico (producto de dos senos de frecuencia no
    // relacionada) para variar el grosor del trazo alrededor de una
    // silueta: un solo seno se ve "ola prolija"; el producto de dos no
    // conmensurables rompe esa periodicidad obvia y da un trazo más
    // parecido a mano temblorosa que a un anillo perfecto.
    static float RuidoTrazo(float ang, float semilla) =>
        0.55f + 0.75f * Mathf.Abs(Mathf.Sin(ang * 9f + semilla) * Mathf.Sin(ang * 5.3f + semilla * 1.9f));

    // Hash determinístico 0..1 a partir de coordenadas de píxel (agrupadas
    // de a 2x2 para que el grano se vea como motas, no como estática fina).
    static float Hash01(float x, float y)
    {
        float h = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // Oscurece una fracción de los píxeles del relleno según un hash
    // determinístico: es lo que le da la textura granulada/tipo tiza de
    // la referencia en vez de un color plano perfecto.
    static Color32 ConGrano(Color32 c, int x, int y, float intensidad)
    {
        float h = Hash01(Mathf.Floor(x / 2f), Mathf.Floor(y / 2f));
        if (h < intensidad)
        {
            float f = 0.72f;
            return new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), c.a);
        }
        return c;
    }

    // Brillo tipo gema/caramelo: un óvalo de luz arriba-izquierda del
    // centro con caída suave. Es lo que hace que algo se sienta "premio
    // pulido" en vez de un relleno plano — el mismo truco que un ícono
    // de moneda o gema en cualquier juego con ganas de dar dopamina.
    static Color32 ConBrillo(Color32 c, float x, float y, float cx, float cy, float radioForma, float intensidad = 0.75f)
    {
        float bx = cx - radioForma * 0.30f, by = cy + radioForma * 0.32f;
        float dx = (x - bx) / (radioForma * 0.60f);
        float dy = (y - by) / (radioForma * 0.42f);
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float brillo = Mathf.Clamp01(1f - d);
        brillo = brillo * brillo * intensidad;
        if (brillo <= 0.001f) return c;
        return Color32.Lerp(c, new Color32(255, 255, 255, c.a), brillo);
    }

    static Texture2D NuevaTextura(int tam)
    {
        var t = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static List<Vector2> VerticesEstrella(float cx, float cy, float rO, float rI, int puntas, float escala, float semillaRuido)
    {
        var lista = new List<Vector2>();
        int total = puntas * 2;
        for (int i = 0; i < total; i++)
        {
            float a = (i / (float)total) * Mathf.PI * 2f - Mathf.PI / 2f;
            float r = (i % 2 == 0 ? rO : rI) * escala * RuidoTrazo(a, semillaRuido);
            lista.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
        }
        return lista;
    }

    // Polígono regular, primer vértice apuntando hacia arriba (mismo
    // offset de -90° que VerticesEstrella) y con un alargamiento vertical
    // opcional — sin ruido en el radio a propósito, a diferencia de
    // VerticesEstrella: este contorno se rellena liso para poder teñirse
    // entero (ver Poligono() más arriba), un borde irregular ahí se
    // notaría como un temblor en cada frame que cambia el tinte.
    static List<Vector2> VerticesPoligono(float cx, float cy, float radio, int lados, float alargamientoVertical)
    {
        var lista = new List<Vector2>();
        for (int i = 0; i < lados; i++)
        {
            float a = (i / (float)lados) * Mathf.PI * 2f - Mathf.PI / 2f;
            lista.Add(new Vector2(cx + Mathf.Cos(a) * radio, cy + Mathf.Sin(a) * radio * alargamientoVertical));
        }
        return lista;
    }

    // Achica un polígono hacia su centro para dibujar el borde interior,
    // pero con un inset distinto por vértice (no un escalado uniforme):
    // así el trazo no queda con un grosor perfectamente parejo.
    static List<Vector2> InsetIrregular(List<Vector2> poly, Vector2 centro, float escalaBase, float semilla)
    {
        var resultado = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            float variacion = 0.85f + 0.15f * RuidoTrazo(i * 1.7f, semilla);
            resultado.Add(centro + (poly[i] - centro) * escalaBase * variacion);
        }
        return resultado;
    }

    // Algoritmo estándar de punto-en-polígono (ray casting / PNPOLY)
    static bool EnPoligono(Vector2 p, List<Vector2> poly)
    {
        bool dentro = false;
        int j = poly.Count - 1;
        for (int i = 0; i < poly.Count; i++)
        {
            if ((poly[i].y < p.y && poly[j].y >= p.y || poly[j].y < p.y && poly[i].y >= p.y) &&
                (poly[i].x + (p.y - poly[i].y) / (poly[j].y - poly[i].y) * (poly[j].x - poly[i].x) < p.x))
            {
                dentro = !dentro;
            }
            j = i;
        }
        return dentro;
    }
}
