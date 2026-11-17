// TECH S04 readback: reduces _DirtInit/_CleanAcc to a small RGFloat target.
// R = Σ weighted dirt * coverage, G = Σ coverage, B = Σ mud, A = Σ grease (averaged over an NxN block per output texel).
Shader "Hidden/CarWash/DirtEval"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _DirtInit, _CleanAcc;
            float4 _SrcTexel;   // x = 1/srcSize
            int _Block;         // src texels per output texel (per axis)
            float3 _Weights;    // mud, grease, spots

            float4 frag(v2f_img i) : SV_Target
            {
                float4 sum = 0;
                float2 origin = i.uv - (0.5 * _Block) * _SrcTexel.x;
                for (int y = 0; y < _Block; y++)
                for (int x = 0; x < _Block; x++)
                {
                    float2 uv = origin + (float2(x, y) + 0.5) * _SrcTexel.x;
                    float4 init = tex2Dlod(_DirtInit, float4(uv, 0, 0));
                    float4 cl = tex2Dlod(_CleanAcc, float4(uv, 0, 0));
                    float cov = init.a > 0.5 ? 1.0 : 0.0;
                    float mud = saturate(init.r - cl.r);
                    float grease = saturate(init.g - cl.g);
                    float spots = saturate(cl.b - cl.a);
                    sum += float4(dot(_Weights, float3(mud, grease, spots)) * cov, cov, mud * cov, grease * cov);
                }
                return sum / (float)(_Block * _Block);
            }
            ENDCG
        }
    }
}
