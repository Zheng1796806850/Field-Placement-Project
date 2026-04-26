Shader "UI/NightVisionCircleMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Center ("Center (viewport UV)", Vector) = (0.5, 0.5, 0, 0)
        _RadiusClear ("Radius Clear (fully visible inside)", Float) = 0.1
        _RadiusDark ("Radius Dark (full darkness outside)", Float) = 0.38
        _FalloffSharpness ("Falloff Sharpness", Float) = 2.2
        _FalloffCurve ("Falloff Curve (higher = slower darkening at inner edge)", Float) = 1.35
        _Darkness ("Darkness Alpha", Range(0,1)) = 0.95
        _Enabled ("Night Vision Enabled", Float) = 1
        _Aspect ("Camera Aspect (w/h)", Float) = 1.78
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _Center;
            float _RadiusClear;
            float _RadiusDark;
            float _FalloffSharpness;
            float _FalloffCurve;
            float _Darkness;
            float _Enabled;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texCol = tex2D(_MainTex, IN.texcoord);

                float2 uv = IN.texcoord;
                float2 delta = float2((uv.x - _Center.x) * _Aspect, uv.y - _Center.y);
                float dist = length(delta);

                float r0 = _RadiusClear;
                float r1 = max(_RadiusDark, r0 + 1e-4);
                float band = r1 - r0;

                float t = saturate((dist - r0) / max(band, 1e-5));
                float curve = max(_FalloffCurve, 0.01);
                float u = pow(t, curve);

                float k = max(_FalloffSharpness, 0.01);
                float denom = 1.0 - exp(-k);
                float shaped = (1.0 - exp(-k * u * u)) / max(denom, 1e-4);

                float maskAlpha = _Enabled * _Darkness * shaped * IN.color.a * texCol.a;

                fixed4 outCol = fixed4(0.0, 0.0, 0.0, maskAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outCol.a - 0.001);
                #endif

                return outCol;
            }
            ENDCG
        }
    }
}
