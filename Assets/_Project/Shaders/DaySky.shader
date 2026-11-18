// M4.9 G1 + G2 — the lot's sky, driven every frame by DayNightCycle (local clock from DayManager's synced day start):
// zenith → horizon gradient + ground, sun disc + glow, moon disc, twinkling stars that fade in at night (hidden by clouds),
// and drifting fbm clouds projected on a high plane, lit from the sun side and tinted by the time of day.
Shader "CarWash/DaySky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.25, 0.45, 0.8, 1)
        _Horizon ("Horizon", Color) = (0.7, 0.8, 0.9, 1)
        _Ground ("Ground", Color) = (0.4, 0.42, 0.45, 1)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.85, 1)
        _SunDir ("Sun direction (to the sun)", Vector) = (0.3, 0.7, 0.3, 0)
        _MoonDir ("Moon direction", Vector) = (-0.4, 0.5, -0.6, 0)
        _Stars ("Stars (0..1)", Float) = 0
        _Moon ("Moon (0..1)", Float) = 0
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.45
        _CloudSpeed ("Cloud speed", Float) = 0.012
        _CloudLit ("Cloud lit colour", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud shade colour", Color) = (0.62, 0.68, 0.78, 1)
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Ground, _SunColor, _CloudLit, _CloudShade;
                float4 _SunDir, _MoonDir;
                float _Stars, _Moon, _CloudCover, _CloudSpeed, _Exposure;
            CBUFFER_END

            struct A { float4 pos : POSITION; };
            struct V { float4 cs : SV_POSITION; float3 dir : TEXCOORD0; };

            V vert(A v) { V o; o.cs = TransformObjectToHClip(v.pos.xyz); o.dir = v.pos.xyz; return o; }

            float hash12(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash12(i), hash12(i + float2(1, 0)), f.x), lerp(hash12(i + float2(0, 1)), hash12(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p) { float s = 0, a = 0.5; [unroll] for (int k = 0; k < 5; k++) { s += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; } return s; }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz), moon = normalize(_MoonDir.xyz);
                float up = d.y;
                // gradient: horizon band → zenith; below the horizon fades to the ground colour
                float hz = pow(saturate(1.0 - abs(up)), 6.0);
                half3 col = up >= 0 ? lerp(_Zenith.rgb, _Horizon.rgb, pow(saturate(1.0 - up), 3.0)) : lerp(_Ground.rgb, _Horizon.rgb, pow(saturate(1.0 + up * 4.0), 2.0));
                // warm glow around the sun (bigger near the horizon = sunset band)
                float sd = saturate(dot(d, sun));
                col += _SunColor.rgb * (pow(sd, 8.0) * 0.35 + pow(sd, 64.0) * 0.6) * saturate(sun.y * 4.0 + 0.6);
                col += _SunColor.rgb * hz * pow(sd, 3.0) * 0.4 * saturate(1.0 - sun.y * 2.0) * saturate(sun.y * 8.0 + 1.0);
                // stars (only above the horizon), twinkle
                if (_Stars > 0.001 && up > 0)
                {
                    float3 c = floor(d * 180.0);
                    float h = hash13(c);
                    float3 cp = (c + 0.5 + (float3(hash13(c + 7.1), hash13(c + 3.3), hash13(c + 5.7)) - 0.5) * 0.6) / 180.0;
                    float star = smoothstep(0.0024, 0.0, length(d - normalize(cp))) * step(0.993, h);
                    star *= 0.6 + 0.4 * sin(_Time.y * (2.0 + h * 5.0) + h * 40.0);
                    col += star * _Stars * saturate(up * 6.0) * (0.8 + 0.6 * frac(h * 13.0));
                }
                // moon disc + halo
                float md = dot(d, moon);
                col += _Moon * (smoothstep(0.9993, 0.9996, md) * half3(0.95, 0.95, 0.88) * 1.4 + pow(saturate(md), 200.0) * half3(0.25, 0.3, 0.4));
                // sun disc (on top of the glow)
                col += _SunColor.rgb * smoothstep(0.9988, 0.9993, sd) * 4.0 * saturate(sun.y * 10.0 + 0.5);
                // clouds on a plane ~ above the lot
                if (up > 0.0)
                {
                    float2 uv = d.xz / (up + 0.12) * 1.6;
                    float2 wind = float2(1.0, 0.35) * _Time.y * _CloudSpeed;
                    float n = fbm(uv + wind) * 0.75 + fbm(uv * 2.3 - wind * 1.7) * 0.25;
                    float cover = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.32, n);
                    // light from the sun side: sample a step toward the sun, thinner there = lit edge
                    float n2 = fbm(uv + wind + normalize(sun.xz + 1e-4) * 0.12);
                    float lit = saturate(0.55 + (n - n2) * 3.0);
                    half3 cc = lerp(_CloudShade.rgb, _CloudLit.rgb, lit);
                    cc += _SunColor.rgb * pow(sd, 6.0) * 0.5 * (1.0 - cover * 0.5);   // silver lining toward the sun
                    float fade = saturate(up * 5.0);                                  // thin out near the horizon
                    col = lerp(col, cc, cover * fade * 0.92);
                }
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
