Shader "EcosDelPanteon/UI/PixelMenuCutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _StencilComp ("Stencil Comparison",Float)=8
        _Stencil ("Stencil ID",Float)=0
        _StencilOp ("Stencil Operation",Float)=0
        _StencilWriteMask ("Stencil Write Mask",Float)=255
        _StencilReadMask ("Stencil Read Mask",Float)=255
        _ColorMask ("Color Mask",Float)=15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.local=v.vertex;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                o.color=v.color*_Color;
                return o;
            }
            float Glyph(float3 pixel)
            {
                float gold=step(0.08,pixel.r)*step(pixel.g*0.90,pixel.r)*step(pixel.b*1.2,pixel.g);
                float ivory=step(0.70,min(pixel.r,pixel.g))*step(0.45,pixel.b);
                return max(gold,ivory);
            }
            float4 frag(v2f i):SV_Target
            {
                float4 pixel=tex2D(_MainTex,i.uv);
                float ink=Glyph(pixel.rgb);
                // A single four-source-pixel lower-right shadow, never a stroke.
                float shadow=Glyph(tex2D(_MainTex,i.uv+float2(-4.0,4.0)*_MainTex_TexelSize.xy).rgb);
                float4 result=float4(lerp(float3(0.035,0.025,0.02),pixel.rgb*i.color.rgb,ink),max(ink,shadow)*i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
