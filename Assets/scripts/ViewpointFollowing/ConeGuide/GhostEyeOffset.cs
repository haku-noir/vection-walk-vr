using UnityEngine;

/// <summary>
/// 収録視点（GhostCamera）側の左目用・右目用カメラに，ライブ視点（OVRCameraRig）側の
/// 実際のIPD（瞳孔間距離）と同じ左右オフセットを与え続ける（10 仕様，両眼立体視化の拡張）．
/// </summary>
/// <remarks>
/// GhostCamera は TrajectoryPlayer が収録軌跡どおりに動かす単一のカメラで，OVRCameraRig の
/// 一部ではないため，LeftEyeAnchor/RightEyeAnchor のような実機IPD追従を持たない．
/// 両眼立体視化では収録視点側もライブ視点側と同じ実IPDで左右分離する必要があるため，
/// このスクリプトがライブ側の実測IPDを毎フレーム読み取り，収録視点側の左目・右目
/// 子カメラのローカル位置（GhostCamera を原点とした左右オフセットのみ）に反映する．
///
/// ExecuteAlways によりエディタ編集中も動くため，実機がつながっていない間は
/// <see cref="fallbackIpd"/>（成人平均 IPD ≈63mm）へ自動的にフォールバックする．
/// </remarks>
[ExecuteAlways]
public class GhostEyeOffset : MonoBehaviour
{
    /// <summary>ライブ視点の左目アンカー（OVRCameraRig の LeftEyeAnchor）</summary>
    [Tooltip("ライブ視点の左目アンカー（OVRCameraRig の LeftEyeAnchor）")]
    public Transform liveLeftEye;

    /// <summary>ライブ視点の右目アンカー（OVRCameraRig の RightEyeAnchor）</summary>
    [Tooltip("ライブ視点の右目アンカー（OVRCameraRig の RightEyeAnchor）")]
    public Transform liveRightEye;

    /// <summary>収録視点側の左目用カメラ（GhostCamera の子）</summary>
    [Tooltip("収録視点側の左目用カメラ（GhostCamera の子）")]
    public Transform ghostLeftEye;

    /// <summary>収録視点側の右目用カメラ（GhostCamera の子）</summary>
    [Tooltip("収録視点側の右目用カメラ（GhostCamera の子）")]
    public Transform ghostRightEye;

    /// <summary>
    /// ライブ側から実測できない間（HMD未接続・起動直後等）に使う既定IPD[m]．
    /// 成人平均のIPD（約63mm）を既定値とする．
    /// </summary>
    [Tooltip("ライブ側から実測できない間に使う既定IPD[m]（成人平均≈0.063m）")]
    [Range(0.04f, 0.09f)] public float fallbackIpd = 0.063f;

    private void LateUpdate()
    {
        float ipd = fallbackIpd;
        if (liveLeftEye != null && liveRightEye != null)
        {
            float measured = Vector3.Distance(liveLeftEye.position, liveRightEye.position);
            // 未初期化（0付近）や明らかな異常値のときは既定値を使う
            if (measured > 0.01f) ipd = measured;
        }

        float half = ipd * 0.5f;
        if (ghostLeftEye != null) ghostLeftEye.localPosition = new Vector3(-half, 0f, 0f);
        if (ghostRightEye != null) ghostRightEye.localPosition = new Vector3(half, 0f, 0f);
    }
}
