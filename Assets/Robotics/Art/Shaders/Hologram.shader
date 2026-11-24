// ATLAS · Hologram (ECHO assistant). URP, unlit, additive, single-pass-instanced (Quest) safe.
// Fresnel rim + world-space scanlines + subtle flicker.
Shader "RoboticsLab/Hologram"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.35, 0.85, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimBoost ("Rim Boost", Range(0, 4)) = 1.6
        _BaseAlpha ("Core Alpha", Range(0, 1)) = 0.12
        _LineDensity ("Scanline Density (per m)", Float) = 180
        _LineSpeed ("Scanline Speed", Float) = 0.6
        _LineStrength ("Scanline Strength", Range(0, 1)) = 0.35
        _Flicker ("Flicker", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Hologram"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower, _RimBoost, _BaseAlpha, _LineDensity, _LineSpeed, _LineStrength, _Flicker;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimBoost;
                half lines = 0.5h + 0.5h * sin((i.positionWS.y - _Time.y * _LineSpeed) * _LineDensity);
                half scan = lerp(1.0h, lines, _LineStrength);
                half flicker = 1.0h - _Flicker * (0.5h + 0.5h * sin(_Time.y * 37.0) * sin(_Time.y * 11.0));
                half intensity = (_BaseAlpha + rim) * scan * flicker;
                return half4(_Color.rgb * intensity, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
