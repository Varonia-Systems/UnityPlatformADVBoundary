Shader "VaroniaBackOffice/AdvBoundaryWall"
{
    Properties
    {
        // Ajout du Toggle pour la visibilité
        [Toggle] _IsVisible ("Is Visible", Float) = 1.0

        _Color          ("Base Color",      Color)  = (0, 1, 1, 1)
        _ProximityFade  ("Proximity Fade",  Float)  = 0.0
        _PulseSpeed     ("Pulse Speed",     Float)  = 1.5
        _PulseIntensity ("Pulse Intensity", Float)  = 0.3
        _FadeRadius     ("Fade Radius (m)", Float)  = 2.0
        _CamProjU       ("Cam Proj U",      Float)  = 0.5
        _CamWorldY      ("Cam World Y",     Float)  = 1.6
        _WallBottomY    ("Wall Bottom Y",   Float)  = 0.0
        _WallHeight     ("Wall Height",     Float)  = 2.5
        _SegmentA       ("Segment A",       Vector) = (0,0,0,0)
        _SegmentB       ("Segment B",       Vector) = (1,0,0,0)
        _CamPosWorld    ("Cam Pos World",   Vector) = (0,0,0,0)
        _CrossSize      ("Cross Size (m)",  Float)  = 0.15
        _CrossGap       ("Cross Gap (m)",   Float)  = 0.25
        _CrossThickness    ("Cross Arm Width (m)",     Float) = 0.15
        _CrossThicknessMin ("Cross Arm Width Min (m)", Float) = 0.15
        _CrossThicknessMax ("Cross Arm Width Max (m)", Float) = 0.25
        _CrossSharpness    ("Cross Sharpness",         Float) = 200.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "WALL_CURTAIN"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            // Déclaration de la variable de visibilité
            float  _IsVisible;

            fixed4 _Color;
            float  _ProximityFade;
            float  _PulseSpeed;
            float  _PulseIntensity;
            float  _FadeRadius;
            float  _CamProjU;
            float  _CamWorldY;
            float  _WallBottomY;
            float  _WallHeight;
            float4 _SegmentA;
            float4 _SegmentB;
            float4 _CamPosWorld;
            float  _CrossSize;
            float  _CrossGap;
            float  _CrossThickness;
            float  _CrossThicknessMin;
            float  _CrossThicknessMax;
            float  _CrossSharpness;

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 worldPos  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

         v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    // 1. Position pour le rendu écran (Projection)
    o.pos = UnityObjectToClipPos(v.vertex);

    // 2. VRAIE Position Mondiale (World Space) pour vos calculs de distance
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

    o.uv = v.uv;
    return o;
}

            float CrossPattern(float2 uvMeters, float crossSize, float gap, float thickness, float sharpness)
            {
                float cell_size  = crossSize + gap;
                float2 cell = frac(uvMeters / max(cell_size, 0.001)) * cell_size - cell_size * 0.5;

                float half_arm = crossSize * 0.5;
                float half_t   = thickness * 0.5;
                float softness = max(0.5 / max(sharpness, 1.0), 0.001);

                float h = smoothstep(half_t + softness, half_t - softness, abs(cell.y))
                        * smoothstep(half_arm + softness, half_arm - softness, abs(cell.x));

                float v = smoothstep(half_t + softness, half_t - softness, abs(cell.x))
                        * smoothstep(half_arm + softness, half_arm - softness, abs(cell.y));

                return saturate(max(h, v));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 camXZ  = _CamPosWorld.xz;
                float2 segA   = _SegmentA.xz;
                float2 segB   = _SegmentB.xz;
                float2 vertXZ = i.worldPos.xz;

                float2 ab = segB - segA;
                float  t  = clamp(dot(camXZ - segA, ab) / max(dot(ab, ab), 0.0001), 0.0, 1.0);
                float2 camProj = segA + t * ab;

                float distWorld = length(vertXZ - camProj);

                float hFade = 1.0 - smoothstep(_FadeRadius * 0.3, _FadeRadius, distWorld);
                hFade = smoothstep(0.0, 1.0, hFade);
                hFade = saturate(hFade);

                float camV  = saturate((_CamWorldY - _WallBottomY) / max(_WallHeight, 0.01));
                float distV = abs(i.uv.y - camV);
                float vFade = 1.0 - smoothstep(0.15, 0.5, distV);
                vFade = smoothstep(0.0, 1.0, vFade);

                float edgeGlow = (1.0 - smoothstep(0.0, 0.18, distV)) * 1.5;

                float pulse = sin(_Time.y * _PulseSpeed) * _PulseIntensity + (1.0 - _PulseIntensity);

                float2 segDir = normalize(_SegmentB.xz - _SegmentA.xz);
                float  uWorld = dot(i.worldPos.xz - _SegmentA.xz, segDir);
                float  vWorld = i.worldPos.y;
                float2 worldUV = float2(uWorld, vWorld);
                float dynThickness = lerp(_CrossThicknessMin, _CrossThicknessMax, _ProximityFade);
                float crosses  = CrossPattern(worldUV, _CrossSize, _CrossGap, dynThickness, _CrossSharpness);

                float baseAlpha = lerp(0.02, 1.0, crosses);
                
                // On applique _IsVisible au calcul de l'alpha final
                float alpha = baseAlpha * (vFade * 0.6 + edgeGlow * 0.4) * hFade * _ProximityFade * pulse * _IsVisible;
                alpha = saturate(alpha) * _Color.a;

                fixed3 col = _Color.rgb + edgeGlow * _Color.rgb * hFade * 0.8;

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}