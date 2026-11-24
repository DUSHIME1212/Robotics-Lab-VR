// ATLAS · Pulse glow (teleport pads, IK handle, indicator lamps). URP, unlit, opaque or additive, Quest-safe.
// Emissive colour that "breathes", with a bright fresnel edge and optional radial ring (for floor pads).
Shader "RoboticsLab/PulseGlow"
{
    Properties
    {
        [HDR] _Color ("Glow Color", Color) = (0.3, 0.75, 1.0, 1)
        _BaseColor ("Base Color", Color) = (0.05, 0.08, 0.1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.45
        _RimPower ("Rim Power", Range(0.5, 8)) = 2
        _RingCount ("Radial Rings (0 = off)", Float) = 0
        _RingSpeed ("Ring Speed", Float) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Toggle] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        Pass
        {
            Name "PulseGlow"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _BaseColor;
                half _PulseSpeed, _PulseMin, _RimPower, _RingCount, _RingSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
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
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower);
                half pulse = lerp(_PulseMin, 1.0h, 0.5h + 0.5h * sin(_Time.y * _PulseSpeed * 6.2831853));
                half rings = 0;
                if (_RingCount > 0.5h)
                {
                    float r = length(i.uv - 0.5) * 2.0;                       // 0 centre .. 1 edge (cylinder cap UVs)
                    rings = smoothstep(0.75, 1.0, frac(r * _RingCount - _Time.y * _RingSpeed)) * (1.0 - r);
                }
                half glow = saturate(pulse * 0.7h + rim * 0.6h + rings);
                return half4(_BaseColor.rgb + _Color.rgb * glow, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
