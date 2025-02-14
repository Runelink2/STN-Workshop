Shader "Custom/CRTSimpleSecondary" {
    Properties {
        _MainTex ("Main Texture", 2D) = "white" {}
        _OverlayTex ("Overlay Texture", 2D) = "white" {}
        _DetailTex ("Detail Texture", 2D) = "white" {}
        _FlickerAmount ("Flicker Amount", Range(0,1)) = 0.2
        _OverlayStrength ("Overlay Strength", Range(0,1)) = 0.5
        _EmissionStrength ("Emission Strength", Range(0,5)) = 1.0
        _OverlaySpeed ("Overlay Scroll Speed", Range(-5,5)) = 1.0
        _OverlayScale ("Overlay Scale", Range(0.01,100)) = 0.1
        _DetailStrength ("Detail Strength", Range(0,1)) = 0.5
        _GlassStrength ("Glass Strength", Range(0,1)) = 0.5
        _Glossiness ("Glossiness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows

        struct Input {
            float2 uv_MainTex;
            float2 uv_OverlayTex;
            float2 uv_DetailTex;
            float3 worldNormal;
            float3 viewDir;
        };

        sampler2D _MainTex;
        sampler2D _OverlayTex;
        sampler2D _DetailTex;
        float _FlickerAmount;
        float _OverlayStrength;
        float _EmissionStrength;
        float _OverlaySpeed;
        float _OverlayScale;
        float _DetailStrength;
        float _GlassStrength;
        float _Glossiness;
        float _Metallic;

        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 mainCol = tex2D(_MainTex, IN.uv_MainTex);
            float flicker = 1.0 + sin(_Time.y * 60.0) * _FlickerAmount;
            o.Albedo = mainCol.rgb * flicker;
            fixed4 overlayCol = tex2D(_OverlayTex, IN.uv_OverlayTex * _OverlayScale + _Time.y * _OverlaySpeed);
            o.Albedo = lerp(o.Albedo, overlayCol.rgb, _OverlayStrength);
            fixed4 detailCol = tex2D(_DetailTex, IN.uv_DetailTex);
            o.Albedo = lerp(o.Albedo, o.Albedo * detailCol.rgb, _DetailStrength);
            o.Emission = o.Albedo * _EmissionStrength;
            o.Smoothness = _Glossiness;
            o.Metallic = _GlassStrength;
        }
        ENDCG
    }
    FallBack "Standard"
}
