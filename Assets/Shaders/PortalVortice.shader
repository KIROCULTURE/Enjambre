// Vórtice animado — portal de Victoria de Nivel 1 (GameManager.SecuenciaVictoria).
// Reemplaza al disco plano (ShapeFactory.Ficha sin muescas, un color liso
// creciendo) por un remolino real: brazos en espiral que giran hacia el
// centro más un borde brillante tipo horizonte de eventos. El
// crecimiento/pulso y el lerp de color oscuro->vívido siguen viviendo en
// C# (transform.localScale + SpriteRenderer.color, sin tocar esa lógica
// ya probada) — este shader solo reemplaza el RELLENO, que antes era un
// círculo liso y ahora gira de verdad, así el jugador entrando se lee
// como "cruzando un portal" y no "parado debajo de un sprite".
Shader "Enjambre/PortalVortice"
{
    Properties
    {
        _ColorBorde ("Color de borde", Color) = (0.3, 0.1, 0.6, 1)
        _ColorNucleo ("Color de núcleo", Color) = (2.5, 1.8, 3.2, 1)
        _VelocidadGiro ("Velocidad de giro", Float) = 1.3
        _BrazosEspiral ("Brazos de la espiral", Float) = 3.0
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
                float _VelocidadGiro;
                float _BrazosEspiral;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            #define TAU 6.28318530718

            float4 Frag(Varyings IN) : SV_Target
            {
                float2 centro = IN.uv - 0.5;
                float r = length(centro) * 2.0; // 0 en el centro, 1 en el borde del sprite
                float ang = atan2(centro.y, centro.x);

                // Brazos en espiral que giran hacia el centro con el
                // tiempo — la franja brillante de cada brazo se calcula
                // con un frac() sobre radio+ángulo+tiempo, clásico patrón
                // de vórtice.
                float fase = frac(r * 4.0 - _Time.y * _VelocidadGiro + ang / TAU * _BrazosEspiral);
                float brazo = smoothstep(0.0, 0.18, fase) * smoothstep(0.55, 0.18, fase);

                // Núcleo brillante al centro, se apaga hacia afuera.
                float nucleo = smoothstep(0.55, 0.0, r);
                // Borde tipo "horizonte de eventos": anillo fino y
                // brillante justo antes del límite del sprite.
                float borde = smoothstep(0.82, 0.94, r) * smoothstep(1.0, 0.94, r);
                // Recorte circular — sin esto el sprite se vería
                // cuadrado en las esquinas.
                float mascaraCirculo = smoothstep(1.0, 0.9, r);

                // A propósito SIN piso parejo de alpha (nada de "+0.12"
                // constante): el hueco entre brazos y entre núcleo/borde
                // tiene que quedar realmente transparente. Pedido
                // explícito — antes el jugador "se metía debajo de un
                // sprite gigante" (un disco casi opaco con sortingOrder
                // por encima de todo); con huecos de verdad se lee como
                // cruzar un vórtice, no desaparecer detrás de un círculo.
                float intensidad = saturate(brazo * 1.1 + nucleo * 0.35 + borde * 1.6);
                float alpha = intensidad * mascaraCirculo;

                float3 rgb = lerp(_ColorBorde.rgb, _ColorNucleo.rgb, saturate(nucleo + borde));

                return float4(rgb * IN.color.rgb, alpha * IN.color.a * _ColorBorde.a);
            }
            ENDHLSL
        }
    }
}
