using UnityEngine;

/// <summary>
/// 四角錐ガイド（09 仕様）が使うレイヤ名・レイヤ番号を一元管理する静的クラス．
///
/// 錐は「相手の視野にだけ描く」必要があるため，2つの錐をそれぞれ専用レイヤに置き，
/// 箱カメラ（LiveBoxCam / GhostBoxCam）の Culling Mask で描き分ける（仕様 §2.1）．
/// 既存カメラ（ライブ映像・収録映像）の Culling Mask からは両レイヤとも除外する．
/// </summary>
/// <remarks>
/// レイヤ番号は ProjectSettings/TagManager.asset の空き（16 / 17）を使う．
/// レイヤ登録はエディタスクリプト（ConeGuideSceneUpgrader）が行う．
/// </remarks>
public static class ConeGuideLayers
{
    /// <summary>Cone_Other（頂点＝収録軌跡の視点）が乗るレイヤ名．ライブ映像側にのみ描画される</summary>
    public const string ConeOtherName = "ConeOther";

    /// <summary>Cone_Self（頂点＝ライブ頭部の視点）が乗るレイヤ名．収録映像側にのみ描画される</summary>
    public const string ConeSelfName = "ConeSelf";

    /// <summary>
    /// Cone_SelfRef（頂点＝観測者＝自分自身の完全固定リファレンス）が乗るレイヤ名．
    /// Cone_Other とは別レイヤにすることで，箱チャンネル（Cone_Other/Cone_Self）と
    /// ガイド4ストロークチャンネル（Cone_Other/Cone_SelfRef）が互いを巻き込まずに
    /// 独立した入力を持てるようにする（09 §3.7）．
    /// </summary>
    public const string ConeSelfRefName = "ConeSelfRef";

    /// <summary>ConeOther の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeOtherIndex = 16;

    /// <summary>ConeSelf の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeSelfIndex = 17;

    /// <summary>ConeSelfRef の既定レイヤ番号（TagManager の空き枠）</summary>
    public const int ConeSelfRefIndex = 18;

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

    /// <summary>Cone_Other 用レイヤ番号</summary>
    public static int OtherLayer { get { return Resolve(ConeOtherName, ConeOtherIndex); } }

    /// <summary>Cone_Self 用レイヤ番号</summary>
    public static int SelfLayer { get { return Resolve(ConeSelfName, ConeSelfIndex); } }

    /// <summary>Cone_SelfRef 用レイヤ番号</summary>
    public static int SelfRefLayer { get { return Resolve(ConeSelfRefName, ConeSelfRefIndex); } }

    /// <summary>3レイヤすべてを含むビットマスク（既存カメラから除外するときに使う）</summary>
    public static int GuideMask { get { return (1 << OtherLayer) | (1 << SelfLayer) | (1 << SelfRefLayer); } }
}
