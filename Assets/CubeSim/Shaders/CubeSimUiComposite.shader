// CubeSimUiComposite.shader - lays the HUD (rendered on its own, cleared to transparent) over the
// finished game frame during headless capture. UI shaders blend straight alpha into a transparent
// target, which leaves colour premultiplied, so this composites with One / OneMinusSrcAlpha.
Shader "Hidden/CubeSim/UiComposite"
{
    Properties { _MainTex ("HUD", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" }
        ZTest Always ZWrite Off Cull Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                // Stored alpha is a squared coverage after straight-alpha blending; lift it back.
                c.a = sqrt(saturate(c.a));
                return c;
            }
            ENDCG
        }
    }
}
