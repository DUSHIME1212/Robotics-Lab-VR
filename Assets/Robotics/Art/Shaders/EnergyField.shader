// ATLAS · Energy field (light curtains, reach targets, drop zones). URP, unlit, alpha-blended, Quest-safe.
// Animated diagonal stripes in object space + fresnel edge + optional "alarm" pulse driven by _Alarm (0..1).
Shader "RoboticsLab/EnergyField"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.2, 1.0, 0.45, 1)
        [HDR] _AlarmColor ("Alarm Color", Color) = (1.0, 0.15, 0.1, 1)
        _Alarm ("Alarm (0-1)", Range(0, 1)) = 0
        _Alpha ("Base Alpha", Range(0, 1)) = 0.12
        _StripeDensity ("Stripe Density", Float) = 18
        _StripeSpeed ("Stripe Speed", Float) = 0.8
        _StripeAlpha ("Stripe Alpha", Range(0, 1)) = 0.25
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.6
        _Pulse ("Pulse Speed", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "EnergyField"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _AlarmColor;
                half _Alarm, _Alpha, _StripeDensity, _StripeSpeed, _StripeAlpha, _RimPower, _RimAlpha, _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
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
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - abs(dot(n, viewDir)), _RimPower);
                half stripe = step(0.5h, frac((i.positionOS.x + i.positionOS.y + i.positionOS.z) * _StripeDensity * 0.25 + _Time.y * _StripeSpeed));
                half pulse = 0.75h + 0.25h * sin(_Time.y * (_Pulse + _Alarm * 10.0));
                half3 col = lerp(_Color.rgb, _AlarmColor.rgb, _Alarm);
                half a = saturate((_Alpha + stripe * _StripeAlpha + rim * _RimAlpha) * pulse * (1.0h + _Alarm));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
