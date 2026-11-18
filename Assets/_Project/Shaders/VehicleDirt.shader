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
        _DustScale ("Dust noise scale (1/m)", Float) = 9
        _DustBump ("Dust bump", Float) = 6
        _Sparkle ("Fresh-clean sparkle", Range(0, 1)) = 1
        // M4.7 A1: PolyHaven brown_mud_02 for the mud layer (object-space triplanar); off → procedural mud tone
        [NoScaleOffset] _MudTex ("Mud albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _MudNrm ("Mud normal", 2D) = "bump" {}
        [NoScaleOffset] _MudMask ("Mud mask (G AO, A smoothness)", 2D) = "white" {}
        _MudTile ("Mud tiling (1/m)", Float) = 1.6
        _MudTexOn ("Mud texture on", Float) = 0
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DirtInit); SAMPLER(sampler_DirtInit);
            TEXTURE2D(_CleanAcc);
            float4 _CleanAcc_TexelSize;
            TEXTURE2D(_FoamMask);
            TEXTURE2D(_MudTex); SAMPLER(sampler_MudTex);
            TEXTURE2D(_MudNrm);
            TEXTURE2D(_MudMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _MudCol, _GreaseCol, _SpotCol;
                half _DustScale, _DustBump, _Sparkle, _MudTile, _MudTexOn;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv2 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv2 : TEXCOORD2; float3 positionOS : TEXCOORD3; float fog : TEXCOORD4; float3 normalOS : TEXCOORD5; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = i.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.normalOS = i.normalOS;
                o.uv2 = i.uv2;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // value noise in object space (sticks to the car while it drives)
            float hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash13(i), hash13(i + float3(1,0,0)), f.x), lerp(hash13(i + float3(0,1,0)), hash13(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash13(i + float3(0,0,1)), hash13(i + float3(1,0,1)), f.x), lerp(hash13(i + float3(0,1,1)), hash13(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float fbm(float3 p) { return vnoise(p) * 0.5 + vnoise(p * 2.07) * 0.3 + vnoise(p * 4.3) * 0.2; }

            half4 frag(Varyings i) : SV_Target
            {
                float4 init = SAMPLE_TEXTURE2D(_DirtInit, sampler_DirtInit, i.uv2);
                float4 cl = SAMPLE_TEXTURE2D(_CleanAcc, sampler_DirtInit, i.uv2);
                float foam = saturate(SAMPLE_TEXTURE2D(_FoamMask, sampler_DirtInit, i.uv2).r);
                float mud = saturate(init.r - cl.r);
                float grease = saturate(init.g - cl.g);
                float spots = saturate(cl.b - cl.a);
                // M4.6 D6: soft dirt mask from the UV2 dirt map; the dust itself is noisy, rough and bumpy
                float3 q = i.positionOS * _DustScale;
                float n1 = fbm(q), n2 = fbm(q * 3.1 + 7.0);
                float dirtMask = smoothstep(0.02, 0.55, max(mud, grease * 0.9));
                float dusty = dirtMask * lerp(0.75, 1.15, n1);

                // M4.7 A1: real mud (PolyHaven brown_mud_02), triplanar in object space so it sticks to the car
                float3 nOS = normalize(i.normalOS);
                float3 tw = pow(abs(nOS), 4); tw /= (tw.x + tw.y + tw.z);
                float3 mp = i.positionOS * _MudTile;
                half3 mudAlb = SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, mp.zy).rgb * tw.x + SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, mp.xz).rgb * tw.y + SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, mp.xy).rgb * tw.z;
                half4 mudMsk = SAMPLE_TEXTURE2D(_MudMask, sampler_MudTex, mp.zy) * tw.x + SAMPLE_TEXTURE2D(_MudMask, sampler_MudTex, mp.xz) * tw.y + SAMPLE_TEXTURE2D(_MudMask, sampler_MudTex, mp.xy) * tw.z;
                half3 tX = UnpackNormal(SAMPLE_TEXTURE2D(_MudNrm, sampler_MudTex, mp.zy));
                half3 tY = UnpackNormal(SAMPLE_TEXTURE2D(_MudNrm, sampler_MudTex, mp.xz));
                half3 tZ = UnpackNormal(SAMPLE_TEXTURE2D(_MudNrm, sampler_MudTex, mp.xy));
                // whiteout blend (tangent normals swizzled onto each projection axis)
                tX = half3(tX.xy + nOS.zy, abs(tX.z) * nOS.x);
                tY = half3(tY.xy + nOS.xz, abs(tY.z) * nOS.y);
                tZ = half3(tZ.xy + nOS.xy, abs(tZ.z) * nOS.z);
                float3 mudNOS = normalize(tX.zyx * tw.x + tY.xzy * tw.y + tZ.xyz * tw.z);
                float mudAmt = saturate(mud * 1.05) * _MudTexOn;

                half3 albedo = _BaseColor.rgb;
                half3 mudTone = _MudCol.rgb * lerp(0.7, 1.2, n2);
                mudTone = lerp(mudTone, mudAlb * lerp(0.85, 1.1, n2), _MudTexOn);
                albedo = lerp(albedo, mudTone, saturate(mud * 1.05) * lerp(0.8, 1.0, n1));
                albedo = lerp(albedo, _GreaseCol.rgb * lerp(0.8, 1.2, n2), saturate(grease) * 0.85);
                // freshly washed = still wet (water spots appear where the spray just hit, the towel dries them):
                // darker, glossier; dried spots leave a faint chalky tint
                // wet film = local average of the remaining water spots (5 taps) → smooth, not the 2.5 cm spot cells
                float2 tx = _CleanAcc_TexelSize.xy * 3.0;
                float4 c1 = SAMPLE_TEXTURE2D(_CleanAcc, sampler_DirtInit, i.uv2 + float2(tx.x, 0));
                float4 c2 = SAMPLE_TEXTURE2D(_CleanAcc, sampler_DirtInit, i.uv2 - float2(tx.x, 0));
                float4 c3 = SAMPLE_TEXTURE2D(_CleanAcc, sampler_DirtInit, i.uv2 + float2(0, tx.y));
                float4 c4 = SAMPLE_TEXTURE2D(_CleanAcc, sampler_DirtInit, i.uv2 - float2(0, tx.y));
                float spotsAvg = (spots + saturate(c1.b - c1.a) + saturate(c2.b - c2.a) + saturate(c3.b - c3.a) + saturate(c4.b - c4.a)) * 0.2;
                float wet = saturate(spotsAvg * 3.0);
                albedo = lerp(albedo, albedo * 0.82, wet * (1 - dirtMask));
                albedo = lerp(albedo, _SpotCol.rgb, spots * 0.25 * (1 - wet));
                albedo = lerp(albedo, half3(1, 1, 1), foam * 0.85);

                // normal: dust bumps (noise gradient) only where dirty
                float3 n = normalize(i.normalWS);
                float e = 0.03;
                float3 g = float3(fbm(q + float3(e, 0, 0)) - n1, fbm(q + float3(0, e, 0)) - n1, fbm(q + float3(0, 0, e)) - n1) / e;
                float3 gWS = TransformObjectToWorldDir(g, false);
                n = normalize(n - (gWS - n * dot(gWS, n)) * _DustBump * dusty * 0.02);
                n = normalize(lerp(n, TransformObjectToWorldNormal(mudNOS), mudAmt));

                float clean = 1 - dirtMask;
                float smooth = lerp(0.08 + 0.1 * n2, 0.86, clean);          // matte dust → polished paint
                smooth = lerp(smooth, 0.97, wet * clean);                   // wet film: mirror-like
                // M4.8 F3: towel-dried paint (cl.a = water spots wiped) is polished: high gloss + a slow twinkle
                float polish = saturate(cl.a * 3.0) * clean * (1.0 - wet);
                smooth = lerp(smooth, 0.95, polish);
                smooth = lerp(smooth, mudMsk.a * 0.8, mudAmt * (1 - foam));
                smooth = lerp(smooth, 0.35, foam);

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = n;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                input.bakedGI = SampleSH(n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0.08 * clean;
                s.smoothness = smooth;
                s.occlusion = lerp(lerp(1.0, 0.75 + 0.25 * n1, dirtMask), mudMsk.g, mudAmt);
                s.alpha = 1;
                // fresh-clean sparkle: tiny glints twinkling on the wet, clean paint
                float3 cell = floor(i.positionOS * 60.0);
                float glint = step(0.985, hash13(cell + floor(_Time.y * 3.0))) * wet * clean * _Sparkle
                            + step(0.993, hash13(floor(i.positionOS * 90.0) + floor(_Time.y * 1.5))) * polish * _Sparkle * 0.7;
                s.emission = glint * half3(1.6, 1.6, 1.7) * saturate(dot(n, input.viewDirectionWS) * 2);

                half4 col = UniversalFragmentPBR(input, s);
                col.rgb = MixFog(col.rgb, i.fog);
                return half4(col.rgb, 1);
            }
            ENDHLSL

        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
