Shader "EcosDelPanteon/UI/PixelMenuHighlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
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
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color*_Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex,i.uv);
                fixed luminance = dot(source.rgb,fixed3(0.2126,0.7152,0.0722));
                fixed mask = saturate((luminance-0.40)*4.0)*source.a;
                fixed4 result = fixed4(i.color.rgb,i.color.a*mask);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
