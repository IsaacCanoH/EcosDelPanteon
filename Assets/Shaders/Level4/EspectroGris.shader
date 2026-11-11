Shader "EcosDelPanteon/2D/EspectroGris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        half4 _Color;
        half4 _RendererColor;

        struct Attributes
        {
            float3 positionOS : POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.color = v.color * _Color * _RendererColor;
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            return o;
        }

        half4 Frag(Varyings i) : SV_Target
        {
            half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            half brightness = saturate(dot(pixel.rgb, half3(0.2126, 0.7152, 0.0722)));
            #ifndef UNITY_COLORSPACE_GAMMA
                brightness = LinearToSRGB(brightness);
            #endif
            half gray = lerp(0.18, 0.61, brightness);
            #ifndef UNITY_COLORSPACE_GAMMA
                gray = SRGBToLinear(gray);
            #endif
            return half4(gray, gray, gray, pixel.a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
