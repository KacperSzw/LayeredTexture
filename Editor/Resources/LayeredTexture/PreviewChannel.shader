Shader "Hidden/LayeredTexture/PreviewChannel"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Channel ("Channel", Int) = 0
        _SrgbDisplay ("sRGB Display", Int) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            int _Channel;
            int _SrgbDisplay;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata value)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(value.vertex);
                output.uv = value.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv);
                float3 display = color.rgb;

                if (_SrgbDisplay != 0)
                {
                    #ifndef UNITY_COLORSPACE_GAMMA
                    display = LinearToGammaSpace(display);
                    #endif
                }

                if (_Channel == 1)
                    return fixed4(display.rrr, 1.0);

                if (_Channel == 2)
                    return fixed4(display.ggg, 1.0);

                if (_Channel == 3)
                    return fixed4(display.bbb, 1.0);

                if (_Channel == 4)
                    return fixed4(color.aaa, 1.0);

                return fixed4(display, 1.0);
            }
            ENDCG
        }
    }
}
