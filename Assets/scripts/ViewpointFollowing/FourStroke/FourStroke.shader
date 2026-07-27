// 4ストローク見かけの運動（4-stroke apparent motion）の合成シェーダ．
// 現在フレーム C と過去/収録フレーム D をグレースケール化し，
// 中間グレー(0.5)を軸とした輝度反転（inv=±1）を掛けてから
// 台形波の合成比 αC/αD でクロスフェードする．
// 元実装: docs/fourstroke/color4st/winmain.cpp の FOURSTROKE_MODE
//   result = 0.5 + (src - 0.5) × inv
//   display = C×αC + D×αD + gray(0.5)×(1 - αC - αD)
Shader "Hidden/FourStroke"
{
    Properties
    {
        _CurrentTex ("Current (Live)", 2D) = "white" {}
        _DelayedTex ("Delayed (Past/Recorded)", 2D) = "gray" {}
        _AlphaC ("Alpha Current", Range(0, 1)) = 1
        _AlphaD ("Alpha Delayed", Range(0, 1)) = 0
        _InvertC ("Invert Current (+1/-1)", Float) = 1
        _InvertD ("Invert Delayed (+1/-1)", Float) = 1
        _Grayscale ("Grayscale (0/1)", Float) = 1
    }
    SubShader
    {
        // 全画面 Blit 用: カリング・深度は不要
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CurrentTex;
            sampler2D _DelayedTex;
            float _AlphaC;
            float _AlphaD;
            float _InvertC;
            float _InvertD;
            float _Grayscale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = tex2D(_CurrentTex, i.uv).rgb;
                fixed3 d = tex2D(_DelayedTex, i.uv).rgb;

                // グレースケール化（反転操作を輝度軸上で対称にするため，元実装と同じく既定ON）
                if (_Grayscale > 0.5)
                {
                    c = dot(c, fixed3(0.299, 0.587, 0.114)).xxx;
                    d = dot(d, fixed3(0.299, 0.587, 0.114)).xxx;
                }

                // 中間グレーを軸としたコントラスト反転（inv = +1: 無変換 / -1: 完全反転）
                c = 0.5 + (c - 0.5) * _InvertC;
                d = 0.5 + (d - 0.5) * _InvertD;

                // 台形波クロスフェード（合計が1に満たない分は一様グレーで埋める）
                float grayRatio = saturate(1.0 - _AlphaC - _AlphaD);
                fixed3 result = c * _AlphaC + d * _AlphaD + 0.5 * grayRatio;
                return fixed4(result, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
