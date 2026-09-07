using UnityEngine;

/// <summary>
/// 四角錐ガイド（09 仕様）が使うレイヤ名・レイヤ番号を一元管理する静的クラス．
///
/// 錐は「相手の視野にだけ描く」必要があるため，2つの錐をそれぞれ専用レイヤに置き，
/// 箱カメラ（LiveBoxCam / GhostBoxCam）の Culling Mask で描き分ける（仕様 §2.1）．
/// 既存カメラ（ライブ映像・収録映像）の Culling Mask からは両レイヤとも除外する．
///
/// <b>Cone_Other は近断面／遠断面／稜線をさらに3つのサブレイヤに分割する</b>（09 §3.7 拡張）．
/// ガイドチャンネルが近い箱・遠い箱それぞれに独立した「今 vs 数百ms前」の4ストロークを
/// 掛けられるようにするため。Cone_Self は分割しない（ガイドチャンネルの入力ではないため）。
///
/// <b>Far At Infinity が有効な遠断面は，さらに左目用／右目用の2レイヤに分かれる</b>（10 §3.3 拡張）。
/// 両眼立体視化に伴い，遠断面の再アンカリング（<see cref="ConeGuide"/> の FarAnchor）を
/// 左目・右目それぞれの実カメラ基準で個別に計算する必要があるため。Far At Infinity が
/// 無効な間は既定の <see cref="ConeOtherFarName"/> レイヤ1つのままで，左右レイヤは使わない。
/// </summary>
/// <remarks>
/// レイヤ番号は ProjectSettings/TagManager.asset の空き（16 以降）を使う．
/// レイヤ登録はエディタスクリプト（ConeGuideSceneUpgrader）が行う．
/// </remarks>
public static class ConeGuideLayers
{
    /// <summary>
    /// Cone_Other の近断面が乗るレイヤ名．ライブ映像側にのみ描画される．
    /// 分割前の「Cone_Other 全体」と同じレイヤ番号を引き継ぐ（後方互換）．
    /// </summary>
    public const string ConeOtherName = "ConeOther";

    /// <summary>Cone_Self（頂点＝ライブ頭部の視点）が乗るレイヤ名．収録映像側にのみ描画される</summary>
    public const string ConeSelfName = "ConeSelf";

    /// <summary>Cone_Other の遠断面が乗るレイヤ名（近断面とは独立に4ストロークを掛けるため）</summary>
    public const string ConeOtherFarName = "ConeOtherFar";

    /// <summary>
    /// Cone_Other の稜線（近断面と遠断面をつなぐ4本の側稜）が乗るレイヤ名．
    /// ガイドチャンネルの近断面/遠断面どちらの入力からも除外され，箱チャンネル
    /// （LiveBoxCam の合成マスク）にのみ含まれる＝4ストロークで明滅しない静的な線になる．
    /// </summary>
    public const string ConeOtherRidgeName = "ConeOtherRidge";

    /// <summary>
    /// Cone_Other の遠断面（左目用）が乗るレイヤ名．Far At Infinity 有効時のみ使う（10 §3.3 拡張）．
    /// </summary>
    public const string ConeOtherFarLeftName = "ConeOtherFarLeft";

    /// <summary>
    /// Cone_Other の遠断面（右目用）が乗るレイヤ名．Far At Infinity 有効時のみ使う（10 §3.3 拡張）．
    /// </summary>
    public const string ConeOtherFarRightName = "ConeOtherFarRight";

    /// <summary>ConeOther（近断面）の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeOtherIndex = 16;

    /// <summary>ConeSelf の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeSelfIndex = 17;

    /// <summary>ConeOtherFar の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeOtherFarIndex = 18;

    /// <summary>ConeOtherRidge の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeOtherRidgeIndex = 19;

    /// <summary>ConeOtherFarLeft の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeOtherFarLeftIndex = 20;

    /// <summary>ConeOtherFarRight の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeOtherFarRightIndex = 21;

    /// <summary>
    /// レイヤ名から番号を解決する（未登録なら既定番号にフォールバックし，警告を出す）
    /// </summary>
    public static int Resolve(string layerName, int fallbackIndex)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) return layer;

        Debug.LogWarning("[ConeGuideLayers] レイヤ \"" + layerName
            + "\" が未登録です。メニュー「Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加」を実行してください。");
        return fallbackIndex;
    }

    /// <summary>Cone_Other の近断面用レイヤ番号</summary>
    public static int OtherLayer { get { return Resolve(ConeOtherName, ConeOtherIndex); } }

    /// <summary>Cone_Self 用レイヤ番号</summary>
    public static int SelfLayer { get { return Resolve(ConeSelfName, ConeSelfIndex); } }

    /// <summary>Cone_Other の遠断面用レイヤ番号</summary>
    public static int OtherFarLayer { get { return Resolve(ConeOtherFarName, ConeOtherFarIndex); } }

    /// <summary>Cone_Other の稜線用レイヤ番号</summary>
    public static int OtherRidgeLayer { get { return Resolve(ConeOtherRidgeName, ConeOtherRidgeIndex); } }

    /// <summary>Cone_Other の遠断面（左目用，Far At Infinity 有効時のみ使用）のレイヤ番号</summary>
    public static int OtherFarLeftLayer { get { return Resolve(ConeOtherFarLeftName, ConeOtherFarLeftIndex); } }

    /// <summary>Cone_Other の遠断面（右目用，Far At Infinity 有効時のみ使用）のレイヤ番号</summary>
    public static int OtherFarRightLayer { get { return Resolve(ConeOtherFarRightName, ConeOtherFarRightIndex); } }

    /// <summary>全レイヤを含むビットマスク（既存カメラから除外するときに使う）</summary>
    public static int GuideMask
    {
        get
        {
            return (1 << OtherLayer) | (1 << SelfLayer) | (1 << OtherFarLayer) | (1 << OtherRidgeLayer)
                | (1 << OtherFarLeftLayer) | (1 << OtherFarRightLayer);
        }
    }
}
