// 四角錐ガイド（09 仕様）の線を描くシェーダ．
//
// 線は LineRenderer のビルボードではなく「太さを持つ3Dジオメトリ（角柱）」として
// ConeGuide.cs が生成する．ここでの深度処理は仕様 §3.1 のとおり
// **ZWrite On / ZTest LEqual**（ZTest Always は使わない）とし，
// 線が交差する箇所で近い線が遠い線を隠す＝稜線オクルージョン（仕様 §1.6）を成立させる．
//
// 箱カメラは環境を描画せず独自の深度バッファを持つため，オクルージョンは錐の内部だけで働く．
// 環境の上への重畳は合成段（ChannelCompositor）が担保する．
//
// 出力アルファは常に 1（線の存在＝箱マスク）．箱カメラの背景は (0,0,0,0) でクリアされるので，
// 合成シェーダはアルファを boxMask としてそのまま使える（仕様 §2.3 の輝度変調方式）．
Shader "Hidden/ConeLine"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        // 角柱の裏面も描く（薄い形状で法線の向きに依存させないため）
        Cull Off
        // 稜線オクルージョンの要（仕様 §1.6 / §3.1）
        ZWrite On
        ZTest LEqual
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
