// Corrupción glitch — la transformación a Super Administrador (punto 9,
// Fase 7/GameManager.AsegurarSuperAdministradorNivel2). A diferencia de
// HazEnergia/PortalVortice (que pintan un sprite BLANCO plano desde cero),
// este shader trabaja sobre un sprite de VERDAD (el Administrador,
// Kenney Toon Characters) — muestrea _MainTex y lo distorsiona en vez de
// generar el relleno. Franjas horizontales que saltan de a "tics" (no un
// temblor continuo) más un split de canales RGB — "el sistema
// reescribiéndose", no magia (pedido explícito del proyecto: nada de
// partículas mágicas, estética de consola/glitch). _Intensidad=0 deja
// pasar el sprite intacto, así que el mismo material sirve para animar
// 0->1->reposo bajo sin tener que cambiar de material a mitad de camino.
Shader "Enjambre/CorrupcionGlitch"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Intensidad ("Intensidad de la corrupción (0=normal, 1=glitch total)", Range(0,1)) = 0
        _ColorTinte ("Tinte de corrupción", Color) = (2.6, 0.3, 3.2, 1)
        _EscalaFranjas ("Escala de las franjas horizontales", Float) = 24
        _VelocidadParpadeo ("Velocidad del parpadeo de franjas (tics/seg)", Float) = 16
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensidad;
                float4 _ColorTinte;
                float _EscalaFranjas;
                float _VelocidadParpadeo;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // Sin corrupción: el sprite intacto, sin ningún costo
                // extra de muestreo — el estado de reposo (fuera de la
                // transformación) es literalmente el sprite normal.
                if (_Intensidad <= 0.001)
                {
                    float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                    return baseCol * IN.color;
                }

                // Franjas horizontales que saltan de fila en fila,
                // recalculadas por "tic" (floor del tiempo, no continuo)
                // — un parpadeo digital, no un temblor suave.
                float franja = floor(IN.uv.y * _EscalaFranjas);
                float tic = floor(_Time.y * _VelocidadParpadeo);
                float ruido = Hash(float2(franja, tic));
                // 0.04 (no 0.12): el sprite del Administrador ocupa pocos
                // píxeles reales en pantalla (cámara de Nivel 2, ver
                // capturas) — un desplazamiento pensado "a ojo" en UV
                // normalizado se ve mucho más agresivo de lo esperado a
                // ese tamaño. Bajado tras ver el resultado forzado a
                // _Intensidad=1 (CapturaTerminalNivel2.CapturarGlitchForzado):
                // con 0.12 el sprite se leía "hecho pedazos", no "el mismo
                // personaje glitcheando".
                float desplazamiento = (ruido - 0.5) * 0.04 * _Intensidad;

                // Split de canales (RGB desalineados horizontalmente) —
                // "algo se está reescribiendo", el mismo lenguaje que
                // cualquier corrupción de datos real. Mismo motivo que
                // arriba para el valor bajo.
                float despCanal = 0.008 * _Intensidad;
                float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(desplazamiento + despCanal, 0)).r;
                float4 muestraG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(desplazamiento, 0));
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(desplazamiento - despCanal, 0)).b;

                float3 rgb = float3(r, muestraG.g, b);
                float alpha = muestraG.a;

                // Tinte de corrupción: pulsa con el mismo "tic" que las
                // franjas (step, no lerp continuo) — se lee como
                // interferencia intermitente, no un filtro de color fijo
                // puesto encima todo el tiempo.
                float pulso = step(0.6, Hash(float2(tic, 7.0)));
                rgb = lerp(rgb, rgb * _ColorTinte.rgb, pulso * _Intensidad * 0.7);

                return float4(rgb * IN.color.rgb, alpha * IN.color.a);
            }
            ENDHLSL
        }
    }
}
