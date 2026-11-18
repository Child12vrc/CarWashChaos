// M4.7 E1 — ground surfaces (asphalt / concrete / grass) without visible tiling:
//  1) stochastic hex tiling (Heitz & Neyret 2018 / Mikkelsen's practical variant): 3 samples per pixel, each hex cell with a random
//     rotation + offset, blended with contrast so cells don't wash out; normals are rotated back per cell;
//  2) low-frequency macro variation (world-space value noise at ~17 m and ~53 m) on brightness, hue and roughness;
//  3) a detail layer: the same albedo / normal at a non-integer ratio (×3.71) for close-up grain, faded with distance;
//  4) distance-blended tiling scale: past _FarStart the UVs blend to ×_FarRatio (larger tiles) so far repeats disappear.
// World-space XZ UVs on the top face (meshes need no UVs); side faces use the mesh UV. PBR via UniversalFragmentPBR,
// DBuffer decals applied (wet patches, puddles, oil / cracks / tyre marks), SSAO, Forward+ light loop, fog.
Shader "CarWash/GroundStochastic"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _BumpMap ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Mask (R metal, G AO, A smoothness)", 2D) = "white" {}
        _BumpScale ("Normal strength", Float) = 1
        _Tile ("Tile size (m)", Float) = 4
        _Contrast ("Hex blend contrast", Range(1, 16)) = 6
        _RotJitter ("Cell rotation (0-1 of a turn)", Range(0, 1)) = 1
        _MacroScale ("Macro noise size (m)", Float) = 17
        _MacroStrength ("Macro brightness ±", Range(0, 0.5)) = 0.14
        _MacroHue ("Macro tint B", Color) = (0.92, 0.95, 1.05, 1)
        _MacroRough ("Macro roughness ±", Range(0, 0.5)) = 0.12
        _DetailRatio ("Detail ratio", Float) = 3.71
        _DetailStrength ("Detail strength", Range(0, 1)) = 0.35
        _FarRatio ("Far tiling ratio", Float) = 0.37
        _FarStart ("Far blend start (m)", Float) = 14
        _FarEnd ("Far blend end (m)", Float) = 40
        _SmoothnessScale ("Smoothness ×", Float) = 1
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            TEXTURE2D(_MaskMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _MacroHue;
                float _BumpScale, _Tile, _Contrast, _RotJitter, _MacroScale, _MacroStrength, _MacroRough;
                float _DetailRatio, _DetailStrength, _FarRatio, _FarStart, _FarEnd, _SmoothnessScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3; float fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = n.normalWS; o.tangentWS = float4(n.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                o.uv = i.uv; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // ---- noise ----
            float2 hash22(float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                float a = hash22(i).x, b = hash22(i + float2(1, 0)).x, c = hash22(i + float2(0, 1)).x, d = hash22(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // ---- hex / triangle grid (Heitz-Neyret) ----
            void TriangleGrid(float2 uv, out float3 w, out float2 v1, out float2 v2, out float2 v3)
            {
                uv *= 3.4641016; // 2·sqrt(3)
                float2 skewed = float2(uv.x - 0.57735027 * uv.y, 1.15470054 * uv.y);
                float2 b = floor(skewed);
                float3 t = float3(frac(skewed), 0); t.z = 1.0 - t.x - t.y;
                if (t.z > 0) { w = float3(t.z, t.y, t.x); v1 = b; v2 = b + float2(0, 1); v3 = b + float2(1, 0); }
                else { w = float3(-t.z, 1 - t.y, 1 - t.x); v1 = b + float2(1, 1); v2 = b + float2(1, 0); v3 = b + float2(0, 1); }
            }

            struct Taps { half3 albedo; half3 nrm; half4 mask; };

            // one rotated + offset sample of all three maps (gradients rotated too → no mip seams at cell edges)
            void Tap(float2 uv, float2 dx, float2 dy, float2 cell, float wgt, inout Taps acc)
            {
                float2 h = hash22(cell);
                float ang = (h.x - 0.5) * 6.2831853 * _RotJitter;
                float s, c; sincos(ang, s, c);
                float2x2 R = float2x2(c, -s, s, c);
                float2 ruv = mul(R, uv) + h * 7.31;
                float2 rdx = mul(R, dx), rdy = mul(R, dy);
                acc.albedo += SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, ruv, rdx, rdy).rgb * wgt;
                acc.mask += SAMPLE_TEXTURE2D_GRAD(_MaskMap, sampler_BaseMap, ruv, rdx, rdy) * wgt;
                half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BaseMap, ruv, rdx, rdy), _BumpScale);
                tn.xy = mul(transpose(R), tn.xy); // rotate the normal back into the surface frame
                acc.nrm += tn * wgt;
            }

            Taps Stochastic(float2 uv)
            {
                float2 dx = ddx(uv), dy = ddy(uv);
                float3 w; float2 v1, v2, v3;
                TriangleGrid(uv, w, v1, v2, v3);
                w = pow(max(w, 1e-4), _Contrast); w /= (w.x + w.y + w.z);
                Taps t = (Taps)0;
                Tap(uv, dx, dy, v1, w.x, t); Tap(uv, dx, dy, v2, w.y, t); Tap(uv, dx, dy, v3, w.z, t);
                return t;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 nGeo = normalize(i.normalWS);
                bool top = nGeo.y > 0.6;
                float2 uv = top ? i.positionWS.xz / _Tile : i.uv;
                float dist = distance(GetCameraPositionWS(), i.positionWS);

                Taps near = Stochastic(uv);
                float far = top ? smoothstep(_FarStart, _FarEnd, dist) : 0;
                Taps t = near;
                if (far > 0.001)
                {
                    Taps f = Stochastic(uv * _FarRatio + 31.7);
                    t.albedo = lerp(near.albedo, f.albedo, far); t.mask = lerp(near.mask, f.mask, far); t.nrm = lerp(near.nrm, f.nrm, far);
                }
                // detail layer at a non-integer ratio (close up only)
                float det = _DetailStrength * (1 - smoothstep(4, 18, dist));
                if (det > 0.001)
                {
                    float2 duv = uv * _DetailRatio + 0.37;
                    half3 da = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, duv).rgb;
                    half lum = dot(da, half3(0.299, 0.587, 0.114));
                    t.albedo *= lerp(1.0, saturate(lum * 2.0 + 0.0), det);                 // overlay-ish grain
                    half3 dn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, duv), _BumpScale);
                    t.nrm = half3(t.nrm.xy + dn.xy * det, t.nrm.z);
                }
                // macro variation (world space, independent of the tiling)
                float2 mp = i.positionWS.xz;
                float m1 = vnoise(mp / _MacroScale), m2 = vnoise(mp / (_MacroScale * 3.1) + 17.0);
                float macro = (m1 * 0.6 + m2 * 0.4) - 0.5;
                half3 albedo = t.albedo * _BaseColor.rgb * (1 + macro * 2 * _MacroStrength);
                albedo *= lerp(half3(1, 1, 1), _MacroHue.rgb, saturate(m2 * 1.4 - 0.2));
                half smooth = saturate(t.mask.a * _SmoothnessScale * (1 + macro * 2 * _MacroRough));

                // tangent frame: top faces use world X/Z (UV = world XZ), others the mesh tangent
                float3 T = top ? float3(1, 0, 0) : normalize(i.tangentWS.xyz);
                float3 B = top ? float3(0, 0, 1) : cross(nGeo, T) * i.tangentWS.w;
                T = normalize(T - nGeo * dot(T, nGeo)); B = normalize(B - nGeo * dot(B, nGeo));
                half3 tn = normalize(t.nrm);
                float3 n = normalize(T * tn.x + B * tn.y + nGeo * tn.z);

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = n;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                input.bakedGI = SampleSH(n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = t.mask.r;
                s.smoothness = smooth;
                s.occlusion = t.mask.g;
                s.normalTS = tn;
                s.alpha = 1;
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(i.positionCS, s, input);
                #endif
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
