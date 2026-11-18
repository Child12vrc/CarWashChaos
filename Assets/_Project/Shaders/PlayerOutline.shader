// Player colour outline (inverted hull): back faces pushed out along the normal, flat player colour (_BaseColor via
// MaterialPropertyBlock from PlayerNet). Drawn by a copy of each body SkinnedMeshRenderer that shares its bones, so it
// follows animation and ragdoll. No shadow caster pass → with ShadowsOnly (own body in first person) nothing is drawn.
Shader "CarWash/PlayerOutline"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Width ("Width (m)", Float) = 0.018
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" }
        Pass
        {
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BaseColor;
            float _Width;

            struct a2v { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(a2v v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                wp += wn * _Width;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                return o;
            }

            float4 frag(v2f i) : SV_Target { return float4(_BaseColor.rgb, 1.0); }
            ENDCG
        }
    }
}
