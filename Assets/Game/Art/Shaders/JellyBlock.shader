Shader "NexZap/JellyBlock"
{
    Properties
    {
        [HideInInspector] _MainTex ("UV Reference", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0
        _Bulge ("Surface Curvature", Range(0,0.65)) = 0.28
        _RimStrength ("Soft Rim", Range(0,0.35)) = 0.1
        _IdleGlossStrength ("Always Visible Gloss", Range(0,0.4)) = 0.4
        _IdleGlossWidth ("Gloss Band Width", Range(0.05,0.6)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half _Bulge;
        half _RimStrength;
        half _IdleGlossStrength;
        half _IdleGlossWidth;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 centeredUv = IN.uv_MainTex * 2.0 - 1.0;
            float2 curvedXY = centeredUv * _Bulge;
            float curvedZ = sqrt(saturate(1.0 - dot(curvedXY, curvedXY)));

            o.Normal = normalize(float3(curvedXY, curvedZ));
            o.Albedo = _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = _Color.a;

            half fresnel = pow(1.0h - saturate(dot(normalize(IN.viewDir), o.Normal)), 3.0h);

            // A broad diagonal reflection remains visible while the block faces
            // the camera. Unlike a highlight sprite, it follows the jelly surface
            // and blends softly into the base colour.
            float diagonal = centeredUv.y + centeredUv.x * 0.28 - 0.42;
            half glossBand = 1.0h - smoothstep(0.0h, _IdleGlossWidth, abs(diagonal));
            half topFade = smoothstep(-0.2h, 0.75h, centeredUv.y);
            half sideFade = 1.0h - smoothstep(0.55h, 1.0h, abs(centeredUv.x));
            glossBand *= topFade * sideFade;

            o.Emission = _Color.rgb * fresnel * _RimStrength
                + lerp(_Color.rgb, 1.0h, 0.7h) * glossBand * _IdleGlossStrength;
        }
        ENDCG
    }

    FallBack "Standard"
}
