// M4.6 B4: world-space TextMesh text (billboards) that is hidden by whatever stands in front of it. The built-in font
// material (GUI/Text Shader) tests depth with unity_GUIZTestMode = Always outside the GUI, so sign text showed through
// buildings / trees. Normal LEqual depth test, no depth write, alpha from the (dynamic) font atlas, fog applied.
Shader "CarWash/WorldText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Lighting Off Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            struct a2v { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(a2v v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = i.color; c.a *= tex2D(_MainTex, i.uv).a;
                UNITY_APPLY_FOG(unity_FogColor, c);
                return c;
            }
            ENDCG
        }
    }
}
