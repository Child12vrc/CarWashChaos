// TECH S04 — vehicle body: base paint blended with mud / grease / water spots / foam sampled in UV2.
// Textures are bound per instance with a MaterialPropertyBlock (no material instancing).
Shader "CarWash/VehicleDirt"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.15, 0.4, 0.85, 1)
        _MudCol ("Mud", Color) = (0.48, 0.36, 0.2, 1)
        _GreaseCol ("Grease", Color) = (0.07, 0.07, 0.06, 1)
        _SpotCol ("Water Spots", Color) = (0.82, 0.84, 0.8, 1)
        [NoScaleOffset] _DirtInit ("Dirt Init", 2D) = "black" {}
        [NoScaleOffset] _CleanAcc ("Clean Acc", 2D) = "black" {}
        [NoScaleOffset] _FoamMask ("Foam", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DirtInit); SAMPLER(sampler_DirtInit);
            TEXTURE2D(_CleanAcc);
            TEXTURE2D(_FoamMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _MudCol, _GreaseCol, _SpotCol;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv2 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv2 : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv2 = i.uv2;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 init = SAMPLE_TEXTURE2D(_DirtInit, sampler_DirtInit, i.uv2);
                float4 cl = SAMPLE_TEXTURE2D(_CleanAcc, sampler_DirtInit, i.uv2);
                float foam = saturate(SAMPLE_TEXTURE2D(_FoamMask, sampler_DirtInit, i.uv2).r);
                float mud = saturate(init.r - cl.r);
                float grease = saturate(init.g - cl.g);
                float spots = saturate(cl.b - cl.a);

                half3 albedo = _BaseColor.rgb;
                albedo = lerp(albedo, _MudCol.rgb, mud * 0.92);
                albedo = lerp(albedo, _GreaseCol.rgb, grease * 0.85);
                albedo = lerp(albedo, _SpotCol.rgb, spots * 0.75);
                albedo = lerp(albedo, half3(1, 1, 1), foam * 0.85);
                float smoothness = lerp(0.8, 0.1, max(mud, grease));

                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = saturate(dot(n, light.direction));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 h = normalize(light.direction + v);
                float spec = pow(saturate(dot(n, h)), exp2(10.0 * smoothness + 1.0)) * smoothness;
                half3 ambient = SampleSH(n);
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half3 col = albedo * (ambient + light.color * ndl * atten) + light.color * spec * atten;
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
