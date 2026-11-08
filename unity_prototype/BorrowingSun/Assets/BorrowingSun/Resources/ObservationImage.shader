Shader "Hidden/BorrowingSun/ObservationImage"
{
    Properties { _MainTex ("Illustration", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i) : SV_Target
            {
                // Sample sRGB once; the target performs the final display conversion.
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
