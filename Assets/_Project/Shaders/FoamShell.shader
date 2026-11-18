// M4.8 F1 — thick foam on the car: N shell layers of the body mesh pushed out along the normal by the UV2 foam mask
// (_FoamMask from DirtPainter through the body renderer's MaterialPropertyBlock). Each material in the renderer's list
// is one shell (_Shell = 1..N). Bubbles = object-space Worley cells (sphere normals); outer shells keep only the bigger
// bubbles so the foam rounds off; shells sag down in world space (gravity) more where the foam is older (thinner mask)
// with slowly creeping drips. Wrap lighting + soft white "subsurface" + a little sparkle. Alpha-clipped, opaque queue.
Shader "CarWash/FoamShell"
{
    Properties
    {
        _Shell ("Shell index (1..N)", Float) = 1
        _ShellCount ("Shell count", Float) = 6
        _FoamHeight ("Max foam height (m)", Float) = 0.1
        _BubbleScale ("Bubble cells per m", Float) = 34
        _Sag ("Gravity sag", Float) = 1.6
        [NoScaleOffset] _FoamMask ("Foam (UV2, from the painter)", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Pass
        {
            Name "FoamShell"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_FoamMask); SAMPLER(sampler_FoamMask);
            CBUFFER_START(UnityPerMaterial)
                float _Shell, _ShellCount, _FoamHeight, _BubbleScale, _Sag;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; float3 os : TEXCOORD1; float3 nws : TEXCOORD2; float foam : TEXCOORD3; float h : TEXCOORD4; float fog : TEXCOORD5; };

            float hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float3 hash33(float3 p) { return float3(hash13(p), hash13(p + 17.1), hash13(p + 41.7)); }
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash13(i), hash13(i + float3(1,0,0)), f.x), lerp(hash13(i + float3(0,1,0)), hash13(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash13(i + float3(0,0,1)), hash13(i + float3(1,0,1)), f.x), lerp(hash13(i + float3(0,1,1)), hash13(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            V vert(A v)
            {
                V o;
                float foam = SAMPLE_TEXTURE2D_LOD(_FoamMask, sampler_FoamMask, v.uv2, 0).r;
                UNITY_SETUP_INSTANCE_ID(v);
                #if defined(UNITY_INSTANCING_ENABLED)
                    float shell = unity_InstanceID + 1;   // DirtPainter draws ShellCount instances
                #else
                    float shell = _Shell;
                #endif
                float h = shell / max(1.0, _ShellCount);
                float lumpy = 0.55 + 0.45 * vnoise(v.pos.xyz * 9.0);
                float thick = saturate(foam * 2.5) * _FoamHeight * lumpy;   // the painter's foam values are light (≈0.2–0.6): ×2.5
                float3 os = v.pos.xyz + v.n * thick * h;
                float3 ws = TransformObjectToWorld(os);
                // gravity: older (thinner) foam slumps more; drips creep down slowly where the noise says so
                float age = 1.0 - saturate(foam * 2.5);
                float drip = smoothstep(0.55, 0.95, vnoise(float3(ws.x * 6.0, ws.y * 1.5 + _Time.y * 0.06, ws.z * 6.0)));
                ws.y -= thick * h * h * _Sag * (0.4 + age) * (1.0 + 3.0 * drip);
                o.ws = ws; o.os = os; o.cs = TransformWorldToHClip(ws);
                o.nws = TransformObjectToWorldNormal(v.n);
                o.foam = saturate(foam * 2.5); o.h = h;
                o.fog = ComputeFogFactor(o.cs.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                if (i.foam < 0.04) discard;
                // Worley bubbles in object space (they stay put on the car)
                float3 p = i.os * _BubbleScale;
                float3 cell = floor(p), f = frac(p);
                float best = 9.0; float3 bestOff = 0; float bestR = 0.5;
                [unroll] for (int z = -1; z <= 1; z++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float3 g = float3(x, y, z);
                    float3 rnd = hash33(cell + g);
                    float3 d = g + rnd - f;
                    float r = 0.35 + 0.45 * rnd.x;               // bubble radius in cells
                    float dd = length(d) / r;
                    if (dd < best) { best = dd; bestOff = d; bestR = r; }
                }
                // outer shells keep only the bigger / closer bubbles; thin foam keeps fewer layers
                float keep = 1.0 - best;
                // bottom shells = a solid foam layer; upper shells break up into round bubbles; thin foam loses its upper shells
                float need = saturate((i.h - 0.3) * 1.4) + (1.0 - saturate(i.foam * 1.3)) * 0.7;
                if (i.h > 0.34 || i.foam < 0.5) clip(keep - need);
                // sphere normal of the bubble, blended with the surface normal
                float3 bn = normalize(TransformObjectToWorldDir(-bestOff, false));
                float3 n = normalize(lerp(normalize(i.nws), bn, 0.55));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
                float3 v = normalize(GetWorldSpaceViewDir(i.ws));
                float wrap = saturate(dot(n, L.direction) * 0.5 + 0.5);
                float sss = pow(saturate(dot(-L.direction, v)), 3.0) * 0.35;        // light through the foam
                float spec = pow(saturate(dot(reflect(-L.direction, n), v)), 48.0) * 0.6;
                float ao = lerp(0.72, 1.0, saturate(keep * 2.0)) * lerp(0.85, 1.0, i.h);
                half3 base = lerp(half3(0.86, 0.9, 0.97), half3(1, 1, 1), i.h);
                half3 col = base * (SampleSH(n) * 0.9 + L.color * (wrap * L.shadowAttenuation * 0.75 + sss)) * ao + spec * L.color;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
