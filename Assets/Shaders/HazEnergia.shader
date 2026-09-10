// Haz de energía animado — paredes láser de Nivel 2 (LaserHazard.cs) y el
// rayo final de la cutscene de cierre (GameManager.CrearHazVisual).
// Reemplaza al degradé horneado en textura (ShapeFactory.Haz, que sigue
// existiendo como respaldo si este material no está asignado) por uno
// calculado en el fragment shader: mismo criterio de "distancia al borde
// más cercano en UV" (se lee bien estirado en cualquier orientación, sin
// importar si la pared es horizontal o vertical), más una veta de ruido
// que fluye con el tiempo — "energía corriendo" — y un factor de
// progreso (telegraph -> resuelto) que antes vivía como un pulso de
// Mathf.Sin en C# (LaserHazard.Update) y ahora es parte del shader.
//
// Un solo shader sirve para CUALQUIER color (rojo=láser normal,
// violeta=hechizo/proyectiles, dorado=rayo final) vía MaterialPropertyBlock
// por instancia — no hace falta una textura horneada por color como antes.
Shader "Enjambre/HazEnergia"
{
    Properties
    {
        _ColorBorde ("Color de borde", Color) = (2.2, 0.33, 0.33, 1)
        _ColorNucleo ("Color de núcleo", Color) = (1, 1, 1, 1)
        _Progreso ("Progreso (0=telegraph, 1=resuelto)", Range(0,1)) = 1
        _VelocidadFlujo ("Velocidad del flujo de energía", Float) = 1.6
        _EscalaRuido ("Escala del ruido", Float) = 5.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBorde;
                float4 _ColorNucleo;
                float _Progreso;
                float _VelocidadFlujo;
                float _EscalaRuido;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Ruido a valor (interpolación bicúbica sobre un hash de
            // grilla) — liviano, alcanza de sobra para una veta de
            // energía que se ve de lejos y en movimiento.
            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                // Distancia al borde MÁS CERCANO en UV normalizado (0 en
                // el borde, 0.5 en el centro) — mismo truco que
                // ShapeFactory.Haz: el eje que queda comprimido en mundo
                // real (el grosor de la pared) hereda el degradé nítido,
                // el eje estirado (el largo) apenas se nota salvo cerca
                // de las puntas.
                float d = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float nucleo = saturate(d * 2.0);
                float alphaForma = smoothstep(0.0, 1.0, nucleo);

                // Flujo de energía: ruido deslizándose a lo largo del eje
                // largo del sprite (uv.x) con el tiempo — se lee como
                // "algo corriendo por el haz" sin importar la orientación
                // real en mundo (mismo razonamiento que arriba).
                float flujo = Ruido(float2(uv.x * _EscalaRuido - _Time.y * _VelocidadFlujo, uv.y * _EscalaRuido));
                float intensidadFlujo = lerp(0.78, 1.18, flujo);

                float3 rgb = lerp(_ColorBorde.rgb, _ColorNucleo.rgb, saturate((nucleo - 0.5) / 0.5) * 0.9);
                float alpha = alphaForma * intensidadFlujo;

                // Progreso del telegraph: tenue y con el ruido más
                // presente ("cargando") antes de resolver; brillo pleno
                // al resolver — reemplaza el pulso de Mathf.Sin que antes
                // vivía en LaserHazard.Update().
                alpha *= lerp(0.3, 1.0, _Progreso);
                rgb *= lerp(0.75, 1.0, _Progreso);

                return float4(rgb * IN.color.rgb, alpha * IN.color.a * _ColorBorde.a);
            }
            ENDHLSL
        }
    }
}
