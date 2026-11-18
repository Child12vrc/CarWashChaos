// TECH S04 — renders the vehicle mesh unwrapped into UV2 space and accumulates cleaning / re-dirt / foam.
// Vertex: clip pos = UV2*2-1 with Y multiplied by _FlipY (global, chosen per graphics API by DirtPainter.FlipSign
// and verified in DirtTest on D3D11/D3D12/Vulkan).
// Pass 0 Clean (Blend One One into _CleanAcc)  : R mudCleaned, G greaseCleaned, B spotsAdded, A spotsRemoved
// Pass 1 ReDirt (RevSub on RG of _CleanAcc)     : slime / pigeons / turret add dirt back
// Pass 2 Foam   (Blend One One into _FoamMask)  : foam layer (needed to clean grease)
// Pass 3 Init   (no blend into _DirtInit)        : procedural starting dirt + coverage alpha
// Pass 4 Fade   (RevSub into _FoamMask)          : foam decays over time
Shader "Hidden/CarWash/DirtUnwrapPaint"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        struct a2v { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv2 : TEXCOORD1; float2 uv3 : TEXCOORD2; };
        struct v2f { float4 pos : SV_POSITION; float3 objPos : TEXCOORD0; float2 uv2 : TEXCOORD1; float3 normal : TEXCOORD2; float hidden : TEXCOORD3; };

        float4 _Brushes[32];   // xyz object-space position, w radius
        float4 _BrushStr[32];  // per-channel strength (already * dt)
        int _BrushCount;
        sampler2D _DirtInit, _CleanPrev, _FoamMask;
        float _GreaseNeedsFoam, _SpotsFraction, _Seed, _FadeAmount, _FlipY;
        float4 _InitDirt;      // x mud, y grease (0..1)

        v2f vert(a2v v)
        {
            v2f o;
            float2 p = v.uv2 * 2.0 - 1.0;
            p.y *= _FlipY;   // set from C# per graphics API (see DirtPainter.FlipSign)
            o.pos = float4(p, 0.5, 1.0);
            o.objPos = v.vertex.xyz;
            o.uv2 = v.uv2;
            o.normal = v.normal;
            o.hidden = v.uv3.x; // UV3.x = 1: face no player can reach (baked by VehicleGlbBuilder)
            return o;
        }

        float hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float vnoise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n000 = hash13(i), n100 = hash13(i + float3(1,0,0)), n010 = hash13(i + float3(0,1,0)), n110 = hash13(i + float3(1,1,0));
            float n001 = hash13(i + float3(0,0,1)), n101 = hash13(i + float3(1,0,1)), n011 = hash13(i + float3(0,1,1)), n111 = hash13(i + float3(1,1,1));
            return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
        }

        float fbm(float3 p) { return vnoise(p) * 0.55 + vnoise(p * 2.03) * 0.3 + vnoise(p * 4.1) * 0.15; }

        float brush(int k, float3 objPos)
        {
            float d = distance(objPos, _Brushes[k].xyz);
            return 1.0 - smoothstep(_Brushes[k].w * 0.5, _Brushes[k].w, d);
        }

        float4 fragClean(v2f i) : SV_Target
        {
            float4 init = tex2D(_DirtInit, i.uv2);
            float4 prev = tex2D(_CleanPrev, i.uv2);
            float foam = tex2D(_FoamMask, i.uv2).r > 0.1 ? 1.0 : 0.0;
            float mudRem = max(0.0, init.r - prev.r);
            float greaseRem = max(0.0, init.g - prev.g);
            float spotMask = hash13(floor(i.objPos * 40.0) + _Seed) < _SpotsFraction ? 1.0 : 0.0;
            float4 acc = 0;
            for (int k = 0; k < _BrushCount; k++)
            {
                float4 s = _BrushStr[k] * brush(k, i.objPos);
                s.g *= lerp(1.0, foam, _GreaseNeedsFoam);
                float rm = min(s.r, mudRem); mudRem -= rm;
                float rg = min(s.g, greaseRem); greaseRem -= rg;
                acc.r += rm; acc.g += rg;
                acc.b += (rm + rg) * spotMask;
                float spotsLeft = max(0.0, prev.b + acc.b - prev.a - acc.a);
                acc.a += min(s.a, spotsLeft);
            }
            return acc;
        }

        float4 fragReDirt(v2f i) : SV_Target
        {
            float4 acc = 0;
            for (int k = 0; k < _BrushCount; k++) acc.rg += _BrushStr[k].rg * brush(k, i.objPos);
            return acc;
        }

        float4 fragFoam(v2f i) : SV_Target
        {
            float a = 0;
            for (int k = 0; k < _BrushCount; k++) a += _BrushStr[k].r * brush(k, i.objPos);
            return float4(a, 0, 0, 0);
        }

        float4 fragInit(v2f i) : SV_Target
        {
            float3 p = i.objPos * 1.7 + _Seed;
            float n1 = saturate(fbm(p) * 1.5 - 0.1);
            float n2 = saturate(fbm(p * 1.3 + 17.0) * 1.6 - 0.25);
            float lowBias = saturate(1.2 - i.objPos.y * 0.6); // more mud low on the body
            float cover = (i.normal.y < -0.6 || i.hidden > 0.5) ? 0.0 : 1.0; // underside / baked-unreachable faces → not counted
            float tucked = i.hidden > 0.5 ? 0.35 : 1.0; // not-counted crevices start only lightly dirty (they can never be cleaned)
            return float4(saturate(_InitDirt.x * lerp(0.55, 1.0, n1) * lerp(0.7, 1.0, lowBias) * tucked), saturate(_InitDirt.y * lerp(0.5, 1.0, n2) * tucked), 0, cover);
        }

        float4 fragFade(v2f i) : SV_Target { return float4(_FadeAmount, 0, 0, 0); }
        ENDCG

        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragClean
            #pragma target 3.5
            ENDCG }
        Pass { BlendOp RevSub Blend One One ColorMask RG
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragReDirt
            #pragma target 3.5
            ENDCG }
        Pass { Blend One One ColorMask R
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFoam
            #pragma target 3.5
            ENDCG }
        Pass { Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragInit
            #pragma target 3.5
            ENDCG }
        Pass { BlendOp RevSub Blend One One ColorMask R
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFade
            ENDCG }
    }
}
