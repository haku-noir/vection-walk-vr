// 四角錐ガイド（09 仕様 §2.2 / §3.7）の3チャンネル独立合成シェーダ．
//
// 背景・箱・ガイドの3チャンネルを独立に合成して1枚に出す:
//   BG_Live / BG_Ghost     … 環境のみを描いた背景（アルファ不使用）
//   Box_Other / Box_Self   … 錐のみを描いた透明背景のRT（アルファ = 箱マスク）
//   Guide_NearNow / Guide_NearDelayed … 近断面のみの「今」/「数百ms前」
//   Guide_FarNow / Guide_FarDelayed   … 遠断面のみの「今」/「数百ms前」
//   Guide_RidgeNow / Guide_RidgeDelayed … 稜線のみの「今」/「数百ms前」
//                                 （いずれも透明背景のRT，アルファ = ガイドマスク。
//                                 *Now は専用カメラの実況テクスチャをそのまま再利用し，
//                                 *Delayed は DelayedFrameBuffer が同じテクスチャから
//                                 複製した過去フレーム。4ストローク歩行シーンと同じ
//                                 「今 vs 過去の自分」の仕組みを近断面・遠断面・稜線
//                                 それぞれに適用したもの。3者は独立した重みを持つため，
//                                 任意の組合せで ON/OFF できる。09 §3.7 拡張）
//
// 背景は既存 FourStroke.shader と同じ扱い（グレースケール化・中間グレー軸の輝度反転・
// 台形波クロスフェード）。矩形波交替は重みが 1/0 になるだけの特殊ケースとして同じ式で処理する。
//
// 箱・ガイドはいずれも仕様 §2.3 の<b>輝度変調方式</b>で重ねる:
//   final = BG + boxMask × boxSign × boxΔ + guideMask × guideSign × guideΔ
// 透明背景上の疎なワイヤフレームには「反転すべき地の面積」が無く通常の反転では
// 負相関を作れないため、下地に対する輝度変調として描く。反転位相で sign = −1 とすることで、
// 下地が何であっても線と地のコントラストが反転する。
// 4ストロークの ON/OFF によらず同じ式（OFF 時は sign = +1 固定）なので、
// 条件間で見えの原理が変わらない。箱とガイドは独立した加算項なので，
// 一方の状態がもう一方の合成結果に影響することはない。
//
// 別色モード（仕様 §3.1、比較条件用）は箱チャンネルにのみ適用される。輝度変調ではなく
// 線の色をアルファブレンドする経路に切り替わる（箱の4ストロークとは排他）。
// ガイドチャンネルは常に輝度変調（4ストロークの原理上，色分けとの併用意義が薄いため）。
//
// ※既存の FourStroke.shader は変更していない（別ファイルとして追加）。
Shader "Hidden/ChannelComposite"
{
    Properties
    {
        _BgLive ("BG Live", 2D) = "black" {}
        _BgGhost ("BG Ghost", 2D) = "black" {}
        _BoxOther ("Box Other", 2D) = "black" {}
        _BoxSelf ("Box Self", 2D) = "black" {}
        _GuideNearNow ("Guide Near Now", 2D) = "black" {}
        _GuideNearDelayed ("Guide Near Delayed", 2D) = "black" {}
        _GuideFarNow ("Guide Far Now", 2D) = "black" {}
        _GuideFarDelayed ("Guide Far Delayed", 2D) = "black" {}
        _GuideRidgeNow ("Guide Ridge Now", 2D) = "black" {}
        _GuideRidgeDelayed ("Guide Ridge Delayed", 2D) = "black" {}

        _BgWeightLive ("BG Weight Live", Range(0, 1)) = 1
        _BgWeightGhost ("BG Weight Ghost", Range(0, 1)) = 0
        _BgInvert ("BG Invert (+1/-1)", Float) = 1
        _BgGrayscale ("BG Grayscale (0/1)", Float) = 0

        _BoxWeightOther ("Box Weight Other", Range(0, 1)) = 0
        _BoxWeightSelf ("Box Weight Self", Range(0, 1)) = 0
        _BoxSign ("Box Sign (+1/-1)", Float) = 1
        _BoxDelta ("Box Luminance Delta", Range(0, 1)) = 0.35
        _BoxColorBlend ("Box Color Blend (0/1)", Float) = 0

        _GuideWeightNearNow ("Guide Weight Near Now", Range(0, 1)) = 0
        _GuideWeightNearDelayed ("Guide Weight Near Delayed", Range(0, 1)) = 0
        _GuideWeightFarNow ("Guide Weight Far Now", Range(0, 1)) = 0
        _GuideWeightFarDelayed ("Guide Weight Far Delayed", Range(0, 1)) = 0
        _GuideWeightRidgeNow ("Guide Weight Ridge Now", Range(0, 1)) = 0
        _GuideWeightRidgeDelayed ("Guide Weight Ridge Delayed", Range(0, 1)) = 0
        _GuideSign ("Guide Sign (+1/-1)", Float) = 1
        _GuideDelta ("Guide Luminance Delta", Range(0, 1)) = 0.35
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

            sampler2D _BgLive;
            sampler2D _BgGhost;
            sampler2D _BoxOther;
            sampler2D _BoxSelf;
            sampler2D _GuideNearNow;
            sampler2D _GuideNearDelayed;
            sampler2D _GuideFarNow;
            sampler2D _GuideFarDelayed;
            sampler2D _GuideRidgeNow;
            sampler2D _GuideRidgeDelayed;

            float _BgWeightLive;
            float _BgWeightGhost;
            float _BgInvert;
            float _BgGrayscale;

            float _BoxWeightOther;
            float _BoxWeightSelf;
            float _BoxSign;
            float _BoxDelta;
            float _BoxColorBlend;

            float _GuideWeightNearNow;
            float _GuideWeightNearDelayed;
            float _GuideWeightFarNow;
            float _GuideWeightFarDelayed;
            float _GuideWeightRidgeNow;
            float _GuideWeightRidgeDelayed;
            float _GuideSign;
            float _GuideDelta;

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
                // ---------- 背景チャンネル ----------
                fixed3 live = tex2D(_BgLive, i.uv).rgb;
                fixed3 ghost = tex2D(_BgGhost, i.uv).rgb;

                // グレースケール化（4ストローク時のみ。反転を輝度軸上で対称にするため）
                if (_BgGrayscale > 0.5)
                {
                    live = dot(live, fixed3(0.299, 0.587, 0.114)).xxx;
                    ghost = dot(ghost, fixed3(0.299, 0.587, 0.114)).xxx;
                }

                // 中間グレーを軸としたコントラスト反転（+1: 無変換 / -1: 完全反転）
                live = 0.5 + (live - 0.5) * _BgInvert;
                ghost = 0.5 + (ghost - 0.5) * _BgInvert;

                // 台形波クロスフェード（合計が1に満たない分は一様グレーで埋める）
                float grayRatio = saturate(1.0 - _BgWeightLive - _BgWeightGhost);
                fixed3 bg = live * _BgWeightLive + ghost * _BgWeightGhost + 0.5 * grayRatio;

                // ---------- 箱チャンネル ----------
                fixed4 other = tex2D(_BoxOther, i.uv);
                fixed4 self = tex2D(_BoxSelf, i.uv);

                // アルファ（線の存在）に提示重みを掛けたものが箱マスク
                float maskOther = other.a * _BoxWeightOther;
                float maskSelf = self.a * _BoxWeightSelf;
                float maskSum = maskOther + maskSelf;
                float mask = saturate(maskSum);

                fixed3 result;
                if (_BoxColorBlend > 0.5)
                {
                    // 別色モード: 線の色をそのまま重ねる（4ストロークとは排他）
                    fixed3 boxColor = maskSum > 1e-5
                        ? (other.rgb * maskOther + self.rgb * maskSelf) / maskSum
                        : fixed3(0, 0, 0);
                    result = lerp(bg, boxColor, mask);
                }
                else
                {
                    // 既定: 下地に対する輝度変調（仕様 §2.3）
                    result = bg + mask * _BoxSign * _BoxDelta;
                }

                // ---------- ガイドチャンネル（近断面・遠断面・稜線: 今 ⇔ 数百ms前, 拡張 09 §3.7） ----------
                // 箱チャンネルとは独立した加算項。常に輝度変調のみ（別色モードは無い）。
                // 近断面・遠断面・稜線は独立したマスクとして計算し，合算して1つの加算項にする
                // （任意の組合せだけ ON にすれば，その部分だけに4ストロークが掛かる）
                fixed4 guideNearNow = tex2D(_GuideNearNow, i.uv);
                fixed4 guideNearDelayed = tex2D(_GuideNearDelayed, i.uv);
                float guideNearMask = saturate(guideNearNow.a * _GuideWeightNearNow
                    + guideNearDelayed.a * _GuideWeightNearDelayed);

                fixed4 guideFarNow = tex2D(_GuideFarNow, i.uv);
                fixed4 guideFarDelayed = tex2D(_GuideFarDelayed, i.uv);
                float guideFarMask = saturate(guideFarNow.a * _GuideWeightFarNow
                    + guideFarDelayed.a * _GuideWeightFarDelayed);

                fixed4 guideRidgeNow = tex2D(_GuideRidgeNow, i.uv);
                fixed4 guideRidgeDelayed = tex2D(_GuideRidgeDelayed, i.uv);
                float guideRidgeMask = saturate(guideRidgeNow.a * _GuideWeightRidgeNow
                    + guideRidgeDelayed.a * _GuideWeightRidgeDelayed);

                float guideMask = saturate(guideNearMask + guideFarMask + guideRidgeMask);
                result += guideMask * _GuideSign * _GuideDelta;

                return fixed4(saturate(result), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
