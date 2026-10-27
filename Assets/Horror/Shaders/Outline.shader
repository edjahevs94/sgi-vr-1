// Contorno de color sólido para URP (técnica "casco invertido"):
// se dibuja la malla inflada a lo largo de sus normales y solo por sus caras traseras,
// así asoma un borde alrededor del objeto. El grosor se mide en píxeles de pantalla,
// de modo que se ve igual de grueso de cerca que de lejos.
// No usa luces ni niebla, por eso se ve en la oscuridad total.
Shader "SgiVr/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.1, 1)
        _Width ("Grosor (píxeles)", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = TransformWorldToHClipDir(normalWS).xy;

                // Desplaza el vértice hacia fuera en pantalla un número fijo de píxeles.
                float len = length(normalCS);
                if (len > 1e-5)
                {
                    float2 offset = normalCS / len * (_Width * 2.0 / _ScreenParams.xy);
                    positionCS.xy += offset * positionCS.w;
                }
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
