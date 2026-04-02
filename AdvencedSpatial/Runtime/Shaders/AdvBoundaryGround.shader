Shader "VaroniaBackOffice/AdvBoundaryGround"
{
    Properties
    {
        // Ajout du Toggle (Case à cocher)
        [Toggle] _IsVisible ("Is Visible", Float) = 1.0
        
        _Color          ("Boundary Color",  Color)  = (0, 1, 1, 1)
        _ProximityFade  ("Proximity Fade",  Float)  = 0.0
        _PulseSpeed     ("Pulse Speed",     Float)  = 1.0
        _PulseIntensity ("Pulse Intensity", Float)  = 0.15
        _BaseAlpha      ("Base Alpha",      Float)  = 0.8
        _IntensityScale ("Intensity Scale",  Float)  = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "GROUND_LINE"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            // Déclaration de la variable
            float  _IsVisible;
            
            fixed4 _Color;
            float  _ProximityFade;
            float  _PulseSpeed;
            float  _PulseIntensity;
            float  _BaseAlpha;
            float  _IntensityScale;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Calcul de l'effet de pulse habituel
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseIntensity + (1.0 - _PulseIntensity);
                
                // On multiplie l'alpha par _IsVisible (qui vaut 0 ou 1)
                float alpha = saturate(_BaseAlpha * _IntensityScale * pulse * _IsVisible);

                fixed3 col = _Color.rgb * _IntensityScale;

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}