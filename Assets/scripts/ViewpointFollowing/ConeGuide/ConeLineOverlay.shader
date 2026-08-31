// 自分基準リファレンス（Cone_SelfRef）専用の線シェーダ．
//
// 通常の Hidden/ConeLine は稜線オクルージョン（仕様 §1.6）のため ZWrite On / ZTest LEqual
// を使うが，これは同じ箱カメラ・同じ深度バッファに描かれる他の錐（Cone_Other 等）との
// 間でも深度テストが働いてしまう。Cone_SelfRef は誤差ゼロ付近で Cone_Other の断面と
// 画面上でほぼ重なるため，奥行きの値によっては互いに隠し合ってしまう（見えなくなる）。
//
// Cone_SelfRef は「常に画面に固定されたリファレンス」であり，他の錐との前後関係を
// 気にする必要がない（自分自身の内部に前後多義性の解消すべき曖昧さも無い）ため，
// ZTest Always（常に手前として描く）・ZWrite Off（他の描画の深度を汚さない）とし，
// レンダーキューも通常の Geometry より後ろにずらして「常に一番手前・最後に描かれる」
// ようにする。
Shader "Hidden/ConeLineOverlay"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+50" }

        // 角柱の裏面も描く（薄い形状で法線の向きに依存させないため）
        Cull Off
        // 常に手前に描く（他の錐との深度競合を避ける）。自分自身の内部にも
        // 前後多義性は無い（誤差に依存しない固定形状のため）ので ZTest LEqual は不要
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 色は頂点カラーで運ぶ（単色モード=全頂点同色 / 別色モード=断面ごとの色）
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // アルファは常に 1（箱マスクとして合成段が参照する）
                return fixed4(i.color.rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
