using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 四角錐ガイド（09 仕様）を<b>現在開いているシーンに追加</b>するエディタスクリプト．
///
/// 既存のシーン生成メニュー（ViewpointFollowingSceneBuilder）とは独立しており，
/// <b>既存シーンを作り直さない</b>（上書き生成しない）．すでに構築済みの
/// ViewpointFollowing.unity に対して，足りない要素だけを差分で足していく
/// （何度実行しても重複しない＝冪等）．
///
/// メニュー: Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加
/// </summary>
/// <remarks>
/// 10 仕様（両眼立体視化）に伴い，このスクリプトは<b>左目用・右目用の完全な2セット</b>
/// （背景カメラ・箱カメラ・ガイドカメラ・RenderTexture・ChannelCompositor）を配線するよう
/// 全面的に書き換えた．
///
/// <b>両眼立体視インフラの出どころ</b>（10 §2.6）: <c>LeftEyeCapture</c> / <c>RightEyeCapture</c> /
/// <c>LeftEyeAnchor</c> / <c>RightEyeAnchor</c> / <c>LeftRawImage</c> / <c>RightRawImage</c> は
/// Player.prefab（<c>OVRCameraRig</c> を含む）に元々存在する休眠中のオブジェクトを再利用する
/// （別実験 ReversedVision 用に作られたもの）．制御コード（SetReversion.cs 等）には依存せず，
/// このスクリプトが独自に配線し直す．
///
/// <b>収録視点（GhostCamera）側の左右分離</b>は，GhostCamera が OVRCameraRig の一部ではなく
/// 実機IPDを持たないため，新設した <see cref="GhostEyeOffset"/> がライブ側の実測IPDを
/// 毎フレーム収録視点側の左目用・右目用カメラへ反映する．
///
/// <b>現時点では ViewpointFollowing.unity（HMD実験シーン）のみ対応</b>．再生確認シーン
/// （ViewpointFollowingReplay.unity）は OVRCameraRig を持たないため，両眼化は別途対応が必要
/// （10 仕様 §2.8 は決定事項だが，具体的な実装は未着手）．
///
/// シーンは自動保存しない（既存シーンを勝手に上書きしないため）．
/// 内容を確認してから手動で保存すること．
/// </remarks>
public static class ConeGuideSceneUpgrader
{
    private const string ConeOtherObjectName = "Cone_Other";
    private const string ConeSelfObjectName = "Cone_Self";
    private const string ConeLineShaderPath =
        "Assets/scripts/ViewpointFollowing/ConeGuide/ConeLine.shader";
    private const string ChannelCompositeShaderPath =
        "Assets/scripts/ViewpointFollowing/ConeGuide/ChannelComposite.shader";

    // RenderTexture の複製元テンプレート（解像度・形式を揃える）
    private const string LiveRTTemplatePath = "Assets/Textures/CenterEye.renderTexture";

    // 箱チャンネル用 RenderTexture（Cone_Other 全体 / Cone_Self）
    private const string LeftBoxOtherRTPath = "Assets/Textures/LeftBoxOther.renderTexture";
    private const string RightBoxOtherRTPath = "Assets/Textures/RightBoxOther.renderTexture";
    private const string LeftBoxSelfRTPath = "Assets/Textures/LeftBoxSelf.renderTexture";
    private const string RightBoxSelfRTPath = "Assets/Textures/RightBoxSelf.renderTexture";

    // ガイドチャンネル用 RenderTexture（近断面/遠断面/稜線）
    private const string LeftBoxOtherNearRTPath = "Assets/Textures/LeftBoxOtherNear.renderTexture";
    private const string RightBoxOtherNearRTPath = "Assets/Textures/RightBoxOtherNear.renderTexture";
    private const string LeftBoxOtherFarRTPath = "Assets/Textures/LeftBoxOtherFar.renderTexture";
    private const string RightBoxOtherFarRTPath = "Assets/Textures/RightBoxOtherFar.renderTexture";
    private const string LeftBoxOtherRidgeRTPath = "Assets/Textures/LeftBoxOtherRidge.renderTexture";
    private const string RightBoxOtherRidgeRTPath = "Assets/Textures/RightBoxOtherRidge.renderTexture";

    // 収録視点（GhostCamera）側の背景用 RenderTexture（ライブ側は既存 LeftEye/RightEye を再利用）
    private const string LeftPlaybackEyeRTPath = "Assets/Textures/LeftPlaybackEye.renderTexture";
    private const string RightPlaybackEyeRTPath = "Assets/Textures/RightPlaybackEye.renderTexture";

    // Player.prefab（OVRCameraRig）内に既に存在する両眼インフラの名前（10 §2.6）
    private const string LeftEyeCaptureName = "LeftEyeCapture";
    private const string RightEyeCaptureName = "RightEyeCapture";
    private const string LeftEyeAnchorName = "LeftEyeAnchor";
    private const string RightEyeAnchorName = "RightEyeAnchor";
    private const string LeftRawImageName = "LeftRawImage";
    private const string RightRawImageName = "RightRawImage";

    // 新設する収録視点側の左目用・右目用カメラ（GhostCamera の子）
    private const string LeftGhostCameraName = "LeftGhostCamera";
    private const string RightGhostCameraName = "RightGhostCamera";

    // 箱・ガイド用カメラの名前（Left/Right 前置，10 §2.9 命名規則）
    private const string LeftLiveBoxCamName = "LeftLiveBoxCam";
    private const string RightLiveBoxCamName = "RightLiveBoxCam";
    private const string LeftGhostBoxCamName = "LeftGhostBoxCam";
    private const string RightGhostBoxCamName = "RightGhostBoxCam";
    private const string LeftLiveBoxNearCamName = "LeftLiveBoxNearCam";
    private const string RightLiveBoxNearCamName = "RightLiveBoxNearCam";
    private const string LeftLiveBoxFarCamName = "LeftLiveBoxFarCam";
    private const string RightLiveBoxFarCamName = "RightLiveBoxFarCam";
    private const string LeftLiveBoxRidgeCamName = "LeftLiveBoxRidgeCam";
    private const string RightLiveBoxRidgeCamName = "RightLiveBoxRidgeCam";

    /// <summary>箱・ガイド用カメラの名前（Culling Mask の除外対象から外すために使う）</summary>
    private static readonly string[] BoxCameraNames =
    {
        LeftLiveBoxCamName, RightLiveBoxCamName,
        LeftGhostBoxCamName, RightGhostBoxCamName,
        LeftLiveBoxNearCamName, RightLiveBoxNearCamName,
        LeftLiveBoxFarCamName, RightLiveBoxFarCamName,
        LeftLiveBoxRidgeCamName, RightLiveBoxRidgeCamName,
    };

    /// <summary>ライブ視点のカメラ／頭部アンカーの候補名（実験シーン / 再生確認シーン）</summary>
    private static readonly string[] LiveAnchorNames = { "CenterEyeAnchor", "LiveReplayCamera" };

    /// <summary>収録視点のカメラの候補名（実験シーン / 再生確認シーン）</summary>
    private static readonly string[] GhostAnchorNames = { "GhostCamera", "GhostReplayCamera" };

    /// <summary>片目分のチャンネル入力一式（10 仕様，両眼立体視化）</summary>
    private struct EyeChannels
    {
        public GameObject host;
        public RawImage rawImage;
        public Texture bgLive;
        public Texture bgGhost;
        public RenderTexture boxOtherRT;
        public RenderTexture boxSelfRT;
        public RenderTexture boxOtherNearRT;
        public RenderTexture boxOtherFarRT;
        public RenderTexture boxOtherRidgeRT;
        public Camera liveBoxCam;
        public Camera ghostBoxCam;
        public Camera liveBoxNearCam;
        public Camera liveBoxFarCam;
        public Camera liveBoxRidgeCam;
    }

    [MenuItem("Tools/視点追従実験/錐ガイドを現在のシーンに追加")]
    public static void UpgradeCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("エラー", "シーンが開かれていません。", "OK");
            return;
        }

        // --- 1. レイヤを登録する ---
        int otherLayer = EnsureLayer(ConeGuideLayers.ConeOtherName, ConeGuideLayers.ConeOtherIndex);
        int selfLayer = EnsureLayer(ConeGuideLayers.ConeSelfName, ConeGuideLayers.ConeSelfIndex);
        int otherFarLayer = EnsureLayer(ConeGuideLayers.ConeOtherFarName, ConeGuideLayers.ConeOtherFarIndex);
        int otherRidgeLayer = EnsureLayer(ConeGuideLayers.ConeOtherRidgeName, ConeGuideLayers.ConeOtherRidgeIndex);
        // Far At Infinity 用の左目/右目別レイヤ（10 §3.3 拡張。kind を問わず両方登録する）
        int otherFarLeftLayer = EnsureLayer(ConeGuideLayers.ConeOtherFarLeftName, ConeGuideLayers.ConeOtherFarLeftIndex);
        int otherFarRightLayer = EnsureLayer(ConeGuideLayers.ConeOtherFarRightName, ConeGuideLayers.ConeOtherFarRightIndex);
        int selfFarLeftLayer = EnsureLayer(ConeGuideLayers.ConeSelfFarLeftName, ConeGuideLayers.ConeSelfFarLeftIndex);
        int selfFarRightLayer = EnsureLayer(ConeGuideLayers.ConeSelfFarRightName, ConeGuideLayers.ConeSelfFarRightIndex);
        if (otherLayer < 0 || selfLayer < 0 || otherFarLayer < 0 || otherRidgeLayer < 0
            || otherFarLeftLayer < 0 || otherFarRightLayer < 0 || selfFarLeftLayer < 0 || selfFarRightLayer < 0)
        {
            EditorUtility.DisplayDialog("エラー",
                "レイヤの空きが足りません。ProjectSettings > Tags and Layers を確認してください。", "OK");
            return;
        }

        // --- 2. 追従対象（ライブ視点・収録視点）を探す ---
        Transform liveAnchor = FindFirst(scene, LiveAnchorNames);
        Transform ghostAnchor = FindFirst(scene, GhostAnchorNames);
        if (liveAnchor == null || ghostAnchor == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "このシーンには錐ガイドを追加できません。\n\n" +
                "見つからなかったもの:\n" +
                (liveAnchor == null ? "- ライブ視点（CenterEyeAnchor / LiveReplayCamera）\n" : "") +
                (ghostAnchor == null ? "- 収録視点（GhostCamera / GhostReplayCamera）\n" : "") +
                "\nViewpointFollowing.unity を開いて実行してください。",
                "OK");
            return;
        }
        Camera ghostBgCam = ghostAnchor.GetComponent<Camera>();
        if (ghostBgCam == null)
        {
            EditorUtility.DisplayDialog("エラー", "収録視点のカメラが見つかりません。", "OK");
            return;
        }

        // --- 2.5. 両眼立体視インフラ（Player.prefab 内，10 §2.6）を探す ---
        Camera leftEyeCaptureCam = FindCamera(scene, LeftEyeCaptureName);
        Camera rightEyeCaptureCam = FindCamera(scene, RightEyeCaptureName);
        Transform leftEyeAnchor = FindFirst(scene, new[] { LeftEyeAnchorName });
        Transform rightEyeAnchor = FindFirst(scene, new[] { RightEyeAnchorName });
        RawImage leftRawImage = FindRawImage(scene, LeftRawImageName);
        RawImage rightRawImage = FindRawImage(scene, RightRawImageName);

        if (leftEyeCaptureCam == null || rightEyeCaptureCam == null || leftEyeAnchor == null
            || rightEyeAnchor == null || leftRawImage == null || rightRawImage == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "両眼立体視に必要なオブジェクトが見つかりません。\n\n" +
                "見つからなかったもの:\n" +
                (leftEyeCaptureCam == null ? "- LeftEyeCapture\n" : "") +
                (rightEyeCaptureCam == null ? "- RightEyeCapture\n" : "") +
                (leftEyeAnchor == null ? "- LeftEyeAnchor\n" : "") +
                (rightEyeAnchor == null ? "- RightEyeAnchor\n" : "") +
                (leftRawImage == null ? "- LeftRawImage\n" : "") +
                (rightRawImage == null ? "- RightRawImage\n" : "") +
                "\nこれらは Player.prefab（OVRCameraRig を含む）に含まれています。\n" +
                "再生確認シーン（ViewpointFollowingReplay.unity）は OVRCameraRig を持たないため，\n" +
                "現時点では両眼化に対応していません。ViewpointFollowing.unity を開いて実行してください。",
                "OK");
            return;
        }

        Shader coneShader = AssetDatabase.LoadAssetAtPath<Shader>(ConeLineShaderPath);

        // --- 3. 錐を2つ用意する（既にあれば作り直さず配線だけ更新する） ---
        ConeGuide coneOther = EnsureCone(scene, ConeOtherObjectName,
            ConeGuide.ConeKind.Other, otherLayer, ghostAnchor, coneShader);
        ConeGuide coneSelf = EnsureCone(scene, ConeSelfObjectName,
            ConeGuide.ConeKind.Self, selfLayer, liveAnchor, coneShader);

        if (coneSelf.mirrorFrom != coneOther)
        {
            Undo.RecordObject(coneSelf, "Sync cone parameters");
            coneSelf.mirrorFrom = coneOther;
            EditorUtility.SetDirty(coneSelf);
            Debug.Log("[ConeGuideSceneUpgrader] Cone_Self のパラメータを Cone_Other に同期させました"
                + "（以後は Cone_Other 側を編集してください）");
        }

        EnsureDualColor(coneOther);
        EnsureDualColor(coneSelf);

        // --- 4. 収録視点側の左目用・右目用カメラを用意する（10 §2.6 拡張，新規） ---
        RenderTexture leftPlaybackRT = EnsureBoxRenderTexture(LeftPlaybackEyeRTPath);
        RenderTexture rightPlaybackRT = EnsureBoxRenderTexture(RightPlaybackEyeRTPath);
        if (leftPlaybackRT == null || rightPlaybackRT == null)
        {
            EditorUtility.DisplayDialog("エラー", "収録視点用 RenderTexture を用意できませんでした。", "OK");
            return;
        }
        Camera leftGhostCam = EnsureEnvironmentCamera(scene, LeftGhostCameraName, ghostAnchor, ghostBgCam,
            ghostBgCam.cullingMask, leftPlaybackRT);
        Camera rightGhostCam = EnsureEnvironmentCamera(scene, RightGhostCameraName, ghostAnchor, ghostBgCam,
            ghostBgCam.cullingMask, rightPlaybackRT);

        // GhostCamera は OVRCameraRig の一部ではなく実機IPDを持たないため，ライブ側の実測IPDを
        // 毎フレーム反映する（10 §2.6 拡張）
        EnsureGhostEyeOffset(ghostAnchor.gameObject, leftEyeAnchor, rightEyeAnchor,
            leftGhostCam.transform, rightGhostCam.transform);

        // ライブ側・収録側で環境の見え方（Culling Mask）を揃える
        // （LeftEyeCapture/RightEyeCapture は別実験用に作られた休眠オブジェクトのため）
        leftEyeCaptureCam.cullingMask = ghostBgCam.cullingMask;
        rightEyeCaptureCam.cullingMask = ghostBgCam.cullingMask;
        EditorUtility.SetDirty(leftEyeCaptureCam);
        EditorUtility.SetDirty(rightEyeCaptureCam);

        // --- 5. 箱用・ガイド用 RenderTexture を左右分用意する ---
        RenderTexture leftBoxOtherRT = EnsureBoxRenderTexture(LeftBoxOtherRTPath);
        RenderTexture rightBoxOtherRT = EnsureBoxRenderTexture(RightBoxOtherRTPath);
        RenderTexture leftBoxSelfRT = EnsureBoxRenderTexture(LeftBoxSelfRTPath);
        RenderTexture rightBoxSelfRT = EnsureBoxRenderTexture(RightBoxSelfRTPath);
        RenderTexture leftBoxOtherNearRT = EnsureBoxRenderTexture(LeftBoxOtherNearRTPath);
        RenderTexture rightBoxOtherNearRT = EnsureBoxRenderTexture(RightBoxOtherNearRTPath);
        RenderTexture leftBoxOtherFarRT = EnsureBoxRenderTexture(LeftBoxOtherFarRTPath);
        RenderTexture rightBoxOtherFarRT = EnsureBoxRenderTexture(RightBoxOtherFarRTPath);
        RenderTexture leftBoxOtherRidgeRT = EnsureBoxRenderTexture(LeftBoxOtherRidgeRTPath);
        RenderTexture rightBoxOtherRidgeRT = EnsureBoxRenderTexture(RightBoxOtherRidgeRTPath);
        if (leftBoxOtherRT == null || rightBoxOtherRT == null || leftBoxSelfRT == null || rightBoxSelfRT == null
            || leftBoxOtherNearRT == null || rightBoxOtherNearRT == null
            || leftBoxOtherFarRT == null || rightBoxOtherFarRT == null
            || leftBoxOtherRidgeRT == null || rightBoxOtherRidgeRT == null)
        {
            EditorUtility.DisplayDialog("エラー", "箱用 RenderTexture を用意できませんでした。", "OK");
            return;
        }

        // --- 6. 箱用・ガイド用カメラを左右分用意する ---
        // 箱チャンネル（Box_Other）: 近断面+稜線は常時，遠断面は「Far At Infinity 無効時の
        // ConeOtherFar」と「有効時のその目専用レイヤ」の両方を含めておく（同時に中身を持つのは
        // 常にどちらか一方だけなので安全。10 §3.3.1 参照）
        int otherBoxMaskLeft = (1 << otherLayer) | (1 << otherFarLayer) | (1 << otherRidgeLayer) | (1 << otherFarLeftLayer);
        int otherBoxMaskRight = (1 << otherLayer) | (1 << otherFarLayer) | (1 << otherRidgeLayer) | (1 << otherFarRightLayer);
        int selfBoxMaskLeft = (1 << selfLayer) | (1 << selfFarLeftLayer);
        int selfBoxMaskRight = (1 << selfLayer) | (1 << selfFarRightLayer);
        // ガイドチャンネルの遠断面入力も同様に，その目専用レイヤを含める
        int otherFarGuideMaskLeft = (1 << otherFarLayer) | (1 << otherFarLeftLayer);
        int otherFarGuideMaskRight = (1 << otherFarLayer) | (1 << otherFarRightLayer);

        Camera leftLiveBoxCam = EnsureBoxCamera(scene, LeftLiveBoxCamName, leftEyeCaptureCam, otherBoxMaskLeft, leftBoxOtherRT);
        Camera rightLiveBoxCam = EnsureBoxCamera(scene, RightLiveBoxCamName, rightEyeCaptureCam, otherBoxMaskRight, rightBoxOtherRT);
        Camera leftGhostBoxCam = EnsureBoxCamera(scene, LeftGhostBoxCamName, leftGhostCam, selfBoxMaskLeft, leftBoxSelfRT);
        Camera rightGhostBoxCam = EnsureBoxCamera(scene, RightGhostBoxCamName, rightGhostCam, selfBoxMaskRight, rightBoxSelfRT);

        Camera leftLiveBoxNearCam = EnsureBoxCamera(scene, LeftLiveBoxNearCamName, leftEyeCaptureCam, 1 << otherLayer, leftBoxOtherNearRT);
        Camera rightLiveBoxNearCam = EnsureBoxCamera(scene, RightLiveBoxNearCamName, rightEyeCaptureCam, 1 << otherLayer, rightBoxOtherNearRT);
        Camera leftLiveBoxFarCam = EnsureBoxCamera(scene, LeftLiveBoxFarCamName, leftEyeCaptureCam, otherFarGuideMaskLeft, leftBoxOtherFarRT);
        Camera rightLiveBoxFarCam = EnsureBoxCamera(scene, RightLiveBoxFarCamName, rightEyeCaptureCam, otherFarGuideMaskRight, rightBoxOtherFarRT);
        Camera leftLiveBoxRidgeCam = EnsureBoxCamera(scene, LeftLiveBoxRidgeCamName, leftEyeCaptureCam, 1 << otherRidgeLayer, leftBoxOtherRidgeRT);
        Camera rightLiveBoxRidgeCam = EnsureBoxCamera(scene, RightLiveBoxRidgeCamName, rightEyeCaptureCam, 1 << otherRidgeLayer, rightBoxOtherRidgeRT);

        // --- 6.5. Far At Infinity 用の Observer（この錐を実際に描画する左右カメラ）を配線する ---
        // Cone_Other はライブ視野（LeftLiveBoxCam/RightLiveBoxCam），
        // Cone_Self は収録視野（LeftGhostBoxCam/RightGhostBoxCam）に描かれる
        WireObservers(coneOther, leftLiveBoxCam.transform, rightLiveBoxCam.transform);
        WireObservers(coneSelf, leftGhostBoxCam.transform, rightGhostBoxCam.transform);

        // --- 7. ChannelCompositor を左目用・右目用にそれぞれ用意する（既定は無効） ---
        List<ViewSwitcher> switchers = CollectComponents<ViewSwitcher>(scene);
        ViewSwitcher switcher = switchers.Count > 0 ? switchers[0] : null;
        if (switcher == null)
        {
            Debug.LogWarning("[ConeGuideSceneUpgrader] ViewSwitcher が見つからないため f_bg の共有元が未設定です"
                + "（ChannelCompositor.bgFrequencyFallback が使われます）");
        }

        var leftChannels = new EyeChannels
        {
            host = leftRawImage.gameObject,
            rawImage = leftRawImage,
            bgLive = leftEyeCaptureCam.targetTexture,
            bgGhost = leftPlaybackRT,
            boxOtherRT = leftBoxOtherRT,
            boxSelfRT = leftBoxSelfRT,
            boxOtherNearRT = leftBoxOtherNearRT,
            boxOtherFarRT = leftBoxOtherFarRT,
            boxOtherRidgeRT = leftBoxOtherRidgeRT,
            liveBoxCam = leftLiveBoxCam,
            ghostBoxCam = leftGhostBoxCam,
            liveBoxNearCam = leftLiveBoxNearCam,
            liveBoxFarCam = leftLiveBoxFarCam,
            liveBoxRidgeCam = leftLiveBoxRidgeCam,
        };
        var rightChannels = new EyeChannels
        {
            host = rightRawImage.gameObject,
            rawImage = rightRawImage,
            bgLive = rightEyeCaptureCam.targetTexture,
            bgGhost = rightPlaybackRT,
            boxOtherRT = rightBoxOtherRT,
            boxSelfRT = rightBoxSelfRT,
            boxOtherNearRT = rightBoxOtherNearRT,
            boxOtherFarRT = rightBoxOtherFarRT,
            boxOtherRidgeRT = rightBoxOtherRidgeRT,
            liveBoxCam = rightLiveBoxCam,
            ghostBoxCam = rightGhostBoxCam,
            liveBoxNearCam = rightLiveBoxNearCam,
            liveBoxFarCam = rightLiveBoxFarCam,
            liveBoxRidgeCam = rightLiveBoxRidgeCam,
        };

        // 左目側をマスターとし，右目側は mirrorFrom で提示条件を引く（10 §2.7）
        ChannelCompositor leftCompositor = EnsureCompositorForEye("左目", leftChannels,
            coneOther, coneSelf, switcher, null);
        ChannelCompositor rightCompositor = EnsureCompositorForEye("右目", rightChannels,
            coneOther, coneSelf, switcher, leftCompositor);

        // 実験制御・ロガーへ左目用（マスター）・右目用の両方を配線する。
        // FollowingExperimentManager/ReplayPlayer が K キー等の ON/OFF 切替を両目へ反映する
        WireExperimentControl(scene, leftCompositor, rightCompositor);

        // --- 8. 既存カメラの Culling Mask から全レイヤを除外する ---
        int excluded = ExcludeConeLayersFromExistingCameras(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = coneOther.gameObject;

        EditorUtility.DisplayDialog("錐ガイド（両眼立体視）を追加しました",
            "シーン: " + scene.name + "\n\n" +
            "Cone_Other → " + ghostAnchor.name + " に追従\n" +
            "Cone_Self  → " + liveAnchor.name + " に追従\n\n" +
            "左目: " + LeftEyeCaptureName + " 系統一式（" + LeftLiveBoxCamName + " 等）\n" +
            "右目: " + RightEyeCaptureName + " 系統一式（" + RightLiveBoxCamName + " 等）\n" +
            "収録視点: " + LeftGhostCameraName + " / " + RightGhostCameraName
            + "（GhostEyeOffset がライブ側の実IPDに追従）\n\n" +
            "既存カメラ " + excluded + " 台から錐レイヤを除外しました。\n\n" +
            "【使い方】左目用 ChannelCompositor（" + LeftRawImageName + " 上）のチェックを入れると\n" +
            "両眼の3チャンネル合成経路に切り替わります。右目用は左目用の設定を自動的に追随します\n" +
            "（Mirror From で配線済み）。個別に変えたい場合のみ右目用を直接編集してください。\n\n" +
            "【旧・単眼オブジェクトについて】LiveBoxCam 等の単眼時代のオブジェクトが\n" +
            "シーンに残っている場合は、もう使われないため手動で削除してください。\n\n" +
            "実験中の錐ガイド ON/OFF（K キー）は FollowingExperimentManager /\n" +
            "ReplayPlayer が左目用・右目用の ChannelCompositor 両方に反映します。\n\n" +
            "【未対応】再生確認シーン（ViewpointFollowingReplay.unity）はまだ両眼化していません。\n\n" +
            "※シーンは自動保存していません。内容を確認して手動で保存してください。",
            "OK");
    }

    /// <summary>
    /// M1 の見た目確認用: ライブ視点のカメラに錐レイヤを一時的に映す／戻す．
    /// 本来 錐は箱カメラからのみ見えるため，このトグルは<b>確認専用</b>である．
    /// 見つかった左目・右目（またはレガシーの単眼）カメラすべてに対して切り替える．
    /// </summary>
    [MenuItem("Tools/視点追従実験/錐ガイド: プレビュー表示を切替")]
    public static void TogglePreview()
    {
        Scene scene = SceneManager.GetActiveScene();
        var previewCams = new List<Camera>();
        foreach (string name in new[] { LeftEyeCaptureName, RightEyeCaptureName, "CenterEyeCapture", "LiveReplayCamera" })
        {
            Camera cam = FindCamera(scene, name);
            if (cam != null) previewCams.Add(cam);
        }
        if (previewCams.Count == 0)
        {
            EditorUtility.DisplayDialog("エラー", "ライブ映像のカメラが見つかりません。", "OK");
            return;
        }

        int mask = ConeGuideLayers.GuideMask;
        bool showing = (previewCams[0].cullingMask & mask) != 0;

        foreach (Camera cam in previewCams)
        {
            Undo.RecordObject(cam, "Toggle Cone Guide Preview");
            cam.cullingMask = showing
                ? (cam.cullingMask & ~mask)   // 元に戻す（除外）
                : (cam.cullingMask | mask);   // プレビュー表示（両方の錐を映す）
            EditorUtility.SetDirty(cam);
        }
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[ConeGuideSceneUpgrader] 錐ガイドのプレビュー表示: "
            + (showing ? "OFF（本来の設定に戻しました）" : "ON（" + previewCams.Count + "台のカメラに錐を映しています）")
            + "\n※本番実験では必ず OFF に戻すこと。");
    }

    // ==================== 個別処理 ====================

    /// <summary>
    /// 指定名のレイヤを登録し，そのレイヤ番号を返す．
    /// 既に同名があればその番号を返す（冪等）．希望番号が埋まっていれば空きを探す．
    /// </summary>
    /// <returns>レイヤ番号（空きが無ければ -1）</returns>
    private static int EnsureLayer(string layerName, int preferredIndex)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[ConeGuideSceneUpgrader] TagManager.asset を読み込めません");
            return -1;
        }

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogError("[ConeGuideSceneUpgrader] TagManager.asset の layers を取得できません");
            return -1;
        }

        // 既に登録済みならそれを使う
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
        }

        // 希望番号が空いていればそこへ．埋まっていればユーザーレイヤ(8以降)の空きを探す
        int target = -1;
        if (preferredIndex < layers.arraySize
            && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferredIndex).stringValue))
        {
            target = preferredIndex;
        }
        else
        {
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    target = i;
                    break;
                }
            }
        }
        if (target < 0) return -1;

        layers.GetArrayElementAtIndex(target).stringValue = layerName;
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[ConeGuideSceneUpgrader] レイヤを登録しました: " + target + " = " + layerName);
        return target;
    }

    /// <summary>
    /// 錐オブジェクトを用意する（既にあれば作り直さず，参照とレイヤだけ更新する）．
    /// ユーザーが Inspector で調整した幾何パラメータを壊さないため，
    /// 新規作成時以外はパラメータに触れない．
    /// </summary>
    private static ConeGuide EnsureCone(Scene scene, string objectName,
        ConeGuide.ConeKind kind, int layer, Transform target, Shader shader)
    {
        Transform existing = FindFirst(scene, new[] { objectName });
        GameObject go;
        bool created = false;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer), typeof(ConeGuide));
            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            created = true;
        }

        ConeGuide cone = go.GetComponent<ConeGuide>();
        if (cone == null) cone = Undo.AddComponent<ConeGuide>(go);

        // 姿勢処理（ヨー/ピッチ/ロール）は錐ごとに持つ．追従対象が違えば LPF の状態も
        // 別であるべきなので，2つの錐で共有してはいけない
        ConePoseFilter filter = go.GetComponent<ConePoseFilter>();
        if (filter == null)
        {
            filter = Undo.AddComponent<ConePoseFilter>(go);
        }

        Undo.RecordObject(cone, "Configure " + objectName);
        cone.kind = kind;
        cone.target = target;
        cone.poseFilter = filter;
        if (shader != null) cone.shader = shader;
        go.layer = layer;

        EditorUtility.SetDirty(cone);
        Debug.Log("[ConeGuideSceneUpgrader] " + objectName + (created ? " を作成しました" : " を更新しました")
            + "（追従対象: " + target.name + ", レイヤ: " + layer
            + ", 姿勢処理: ヨー=" + filter.yawMode + " ピッチ=" + filter.pitchMode
            + " ロール=" + filter.rollMode + "）");
        return cone;
    }

    /// <summary>
    /// 近断面=シアン／遠断面=マゼンタの色分け（Dual Color Mode）を有効化する。
    /// 既に ON ならログを出さず何もしない（冪等）。ユーザーが後で OFF に戻すのは自由。
    /// </summary>
    private static void EnsureDualColor(ConeGuide cone)
    {
        if (cone.dualColorMode) return;
        Undo.RecordObject(cone, "Enable dual color mode");
        cone.dualColorMode = true;
        EditorUtility.SetDirty(cone);
        Debug.Log("[ConeGuideSceneUpgrader] " + cone.name + " の Dual Color Mode を有効化しました"
            + "（近断面=シアン/遠断面=マゼンタ）。箱の4ストローク選択時は自動的に無効化されます。");
    }

    /// <summary>
    /// 錐の左目用・右目用 Observer（この錐を実際に描画するカメラの Transform）を配線する．
    /// Far At Infinity（未使用時は無視される）のために必要．冪等（同じなら何もしない）．
    /// </summary>
    private static void WireObservers(ConeGuide cone, Transform observerLeft, Transform observerRight)
    {
        if (cone.observerLeft == observerLeft && cone.observerRight == observerRight) return;
        Undo.RecordObject(cone, "Wire " + cone.name + " observers");
        cone.observerLeft = observerLeft;
        cone.observerRight = observerRight;
        EditorUtility.SetDirty(cone);
        Debug.Log("[ConeGuideSceneUpgrader] " + cone.name + " の Observer を配線しました: "
            + "L=" + observerLeft.name + " R=" + observerRight.name);
    }

    /// <summary>
    /// 収録視点側の左目用・右目用カメラに，ライブ側の実測IPDを反映する <see cref="GhostEyeOffset"/>
    /// を用意する（10 §2.6 拡張）．
    /// </summary>
    private static void EnsureGhostEyeOffset(GameObject host, Transform liveLeftEye, Transform liveRightEye,
        Transform ghostLeftEye, Transform ghostRightEye)
    {
        GhostEyeOffset offset = host.GetComponent<GhostEyeOffset>();
        bool created = offset == null;
        if (created) offset = Undo.AddComponent<GhostEyeOffset>(host);

        Undo.RecordObject(offset, "Configure GhostEyeOffset");
        offset.liveLeftEye = liveLeftEye;
        offset.liveRightEye = liveRightEye;
        offset.ghostLeftEye = ghostLeftEye;
        offset.ghostRightEye = ghostRightEye;
        EditorUtility.SetDirty(offset);
        Debug.Log("[ConeGuideSceneUpgrader] " + host.name
            + (created ? " に GhostEyeOffset を追加しました" : " の GhostEyeOffset を更新しました")
            + "（ライブ側の実IPDを収録視点側にも反映）");
    }

    /// <summary>
    /// 箱用 RenderTexture を用意する．ライブ映像用の CenterEye を複製して作るので
    /// 解像度・形式がライブ映像と揃う．<b>アルファ付き</b>（線の存在＝箱マスクを運ぶため）と
    /// <b>深度付き</b>（稜線オクルージョンに必要）であることを明示的に確認する．
    /// </summary>
    private static RenderTexture EnsureBoxRenderTexture(string assetPath)
    {
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
        if (rt == null)
        {
            if (!AssetDatabase.CopyAsset(LiveRTTemplatePath, assetPath))
            {
                Debug.LogError("[ConeGuideSceneUpgrader] RenderTexture の複製に失敗しました: " + LiveRTTemplatePath);
                return null;
            }
            rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
            Debug.Log("[ConeGuideSceneUpgrader] 箱用 RenderTexture を作成しました: " + assetPath);
        }
        if (rt == null) return null;

        bool changed = false;
        if (rt.IsCreated()) rt.Release(); // 生成済みだと形式を変更できない

        // アルファ付きでないと箱マスクを運べない（合成シェーダが a を参照する）
        if (rt.graphicsFormat != GraphicsFormat.R8G8B8A8_UNorm
            && rt.graphicsFormat != GraphicsFormat.R8G8B8A8_SRGB)
        {
            rt.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            changed = true;
        }
        // 深度が無いと錐の内部で ZWrite/ZTest が効かず稜線オクルージョンが成立しない
        if (rt.depth < 24)
        {
            rt.depth = 24;
            changed = true;
        }
        if (changed)
        {
            EditorUtility.SetDirty(rt);
            AssetDatabase.SaveAssets();
            Debug.Log("[ConeGuideSceneUpgrader] " + assetPath + " をアルファ付き・深度付きに設定しました");
        }
        return rt;
    }

    /// <summary>
    /// 箱・ガイド用カメラを用意する．<paramref name="source"/> の<b>子</b>として
    /// localPosition=0 で置くことで，その目の実際の視点と姿勢が完全に一致する（透明クリア）．
    /// </summary>
    private static Camera EnsureBoxCamera(Scene scene, string cameraName, Camera source,
        int cullingMask, RenderTexture target)
    {
        return EnsureChildCamera(scene, cameraName, source.transform, source, cullingMask, target,
            CameraClearFlags.SolidColor, new Color(0f, 0f, 0f, 0f), lockLocalPosition: true);
    }

    /// <summary>
    /// 収録視点側の左目用・右目用の環境カメラ（GhostCamera の子）を用意する．
    /// 箱カメラと異なり，ローカル位置は <see cref="GhostEyeOffset"/> が毎フレーム設定するため
    /// ここでは固定しない．クリア方式は <paramref name="source"/>（GhostCamera）と同じにする
    /// （透明クリアではなく，通常の環境描画）．
    /// </summary>
    private static Camera EnsureEnvironmentCamera(Scene scene, string cameraName, Transform parent, Camera source,
        int cullingMask, RenderTexture target)
    {
        return EnsureChildCamera(scene, cameraName, parent, source, cullingMask, target,
            source.clearFlags, source.backgroundColor, lockLocalPosition: false);
    }

    /// <summary>
    /// 子カメラを用意する共通処理．<paramref name="source"/> から FOV・near/far・投影を
    /// コピーし，<paramref name="parent"/> の子として配置する．
    /// </summary>
    private static Camera EnsureChildCamera(Scene scene, string cameraName, Transform parent, Camera source,
        int cullingMask, RenderTexture target, CameraClearFlags clearFlags, Color backgroundColor,
        bool lockLocalPosition)
    {
        Transform existing = FindFirst(scene, new[] { cameraName });
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(cameraName, typeof(Camera));
            Undo.RegisterCreatedObjectUndo(go, "Create " + cameraName);
        }

        go.layer = 0; // カメラ自身のレイヤは描画に影響しない

        Camera cam = go.GetComponent<Camera>();
        if (cam == null) cam = Undo.AddComponent<Camera>(go);

        Undo.RecordObject(cam, "Configure " + cameraName);
        cam.CopyFrom(source);                              // FOV・near/far・投影を元カメラに合わせる
        cam.cullingMask = cullingMask;                      // 指定レイヤだけを描く
        cam.clearFlags = clearFlags;
        cam.backgroundColor = backgroundColor;
        cam.targetTexture = target;
        cam.stereoTargetEye = StereoTargetEyeMask.None;    // HMD へ直接出力しない
        cam.depth = source.depth + 1;                      // 元カメラの後に描く
        cam.useOcclusionCulling = false;
        cam.allowHDR = false;
        cam.allowMSAA = false;

        // 親の子にして姿勢を追従させる．CopyFrom が投影行列を書き換える可能性があるため，
        // Transform の確定は必ずこの後に行う
        Undo.SetTransformParent(go.transform, parent, "Parent " + cameraName);
        if (lockLocalPosition) go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        cam.ResetWorldToCameraMatrix();  // CopyFrom が持ち込みうる固定行列を捨て，親追従に戻す
        cam.ResetProjectionMatrix();
        cam.ResetAspect();               // targetTexture のアスペクトから計算し直させる

        EditorUtility.SetDirty(cam);
        Debug.Log("[ConeGuideSceneUpgrader] " + cameraName + (existing != null ? " を更新" : " を作成")
            + "しました（親: " + parent.name + ", Culling Mask: " + cullingMask + ", 出力: " + target.name + "）");
        return cam;
    }

    /// <summary>
    /// 片目分の ChannelCompositor を用意して配線する．
    /// </summary>
    /// <param name="eyeLabel">ログ・デバッグ用のラベル（"左目"／"右目"）</param>
    /// <param name="ch">この目のチャンネル入力一式</param>
    /// <param name="mirrorFrom">
    /// 提示条件の同期元（10 §2.7）．左目側（マスター）には null を渡す．
    /// </param>
    private static ChannelCompositor EnsureCompositorForEye(string eyeLabel, EyeChannels ch,
        ConeGuide coneOther, ConeGuide coneSelf, ViewSwitcher viewSwitcher, ChannelCompositor mirrorFrom)
    {
        ChannelCompositor compositor = ch.host.GetComponent<ChannelCompositor>();
        bool created = compositor == null;
        if (created)
        {
            compositor = Undo.AddComponent<ChannelCompositor>(ch.host);
            // 既定は無効。オンにしたときだけ新しい合成経路に切り替わる
            compositor.enabled = false;
        }

        Undo.RecordObject(compositor, "Configure ChannelCompositor (" + eyeLabel + ")");
        compositor.viewSwitcher = viewSwitcher;
        compositor.rawImage = ch.rawImage;
        compositor.bgLiveTexture = ch.bgLive;
        compositor.bgGhostTexture = ch.bgGhost;
        compositor.boxOtherTexture = ch.boxOtherRT;
        compositor.boxSelfTexture = ch.boxSelfRT;
        compositor.guideNearNowTexture = ch.boxOtherNearRT;
        compositor.guideFarNowTexture = ch.boxOtherFarRT;
        compositor.guideRidgeNowTexture = ch.boxOtherRidgeRT;
        compositor.coneOther = coneOther;
        compositor.coneSelf = coneSelf;
        compositor.liveBoxCamera = ch.liveBoxCam;
        compositor.ghostBoxCamera = ch.ghostBoxCam;
        compositor.guideNearCamera = ch.liveBoxNearCam;
        compositor.guideFarCamera = ch.liveBoxFarCam;
        compositor.guideRidgeCamera = ch.liveBoxRidgeCam;
        compositor.mirrorFrom = mirrorFrom;
        if (compositor.shader == null)
        {
            compositor.shader = AssetDatabase.LoadAssetAtPath<Shader>(ChannelCompositeShaderPath);
        }

        // ガイドチャンネル: 近断面・遠断面・稜線それぞれの専用テクスチャを一定レートで
        // リングバッファへ複製し，数百ms前の状態を取り出せるようにする（09 §3.7 拡張）
        var claimed = new HashSet<DelayedFrameBuffer>();
        compositor.guideNearDelayBuffer = EnsureDelayBuffer(ch.host, ch.boxOtherNearRT, eyeLabel + "近断面", claimed);
        compositor.guideFarDelayBuffer = EnsureDelayBuffer(ch.host, ch.boxOtherFarRT, eyeLabel + "遠断面", claimed);
        compositor.guideRidgeDelayBuffer = EnsureDelayBuffer(ch.host, ch.boxOtherRidgeRT, eyeLabel + "稜線", claimed);

        EditorUtility.SetDirty(compositor);
        Debug.Log("[ConeGuideSceneUpgrader] ChannelCompositor（" + eyeLabel + "）を " + ch.host.name
            + (created ? " に追加しました（既定は無効）" : " で更新しました"));
        return compositor;
    }

    /// <summary>
    /// ガイドチャンネル用の遅延バッファを用意する．指定したソーステクスチャ（近断面/遠断面/稜線の
    /// 専用RT）を一定レートでリングバッファへ複製し，数百ms前の状態を取り出せるようにする．
    ///
    /// host には近断面用・遠断面用・稜線用の3つの DelayedFrameBuffer が同居するため，
    /// <paramref name="claimed"/> でこの呼び出し内ですでに割り当て済みのコンポーネントを除外しつつ，
    /// sourceTexture が一致する既存コンポーネントを優先的に再利用する（冪等）．
    /// </summary>
    private static DelayedFrameBuffer EnsureDelayBuffer(GameObject host, RenderTexture sourceRT,
        string label, HashSet<DelayedFrameBuffer> claimed)
    {
        DelayedFrameBuffer buffer = null;
        foreach (DelayedFrameBuffer existing in host.GetComponents<DelayedFrameBuffer>())
        {
            if (claimed.Contains(existing)) continue;
            if (existing.sourceTexture == sourceRT) { buffer = existing; break; }
        }
        if (buffer == null)
        {
            foreach (DelayedFrameBuffer existing in host.GetComponents<DelayedFrameBuffer>())
            {
                if (claimed.Contains(existing)) continue;
                buffer = existing;
                break;
            }
        }

        bool created = buffer == null;
        if (created) buffer = Undo.AddComponent<DelayedFrameBuffer>(host);
        claimed.Add(buffer);

        Undo.RecordObject(buffer, "Configure " + label + " guide delay buffer");
        buffer.sourceTexture = sourceRT;
        EditorUtility.SetDirty(buffer);
        Debug.Log("[ConeGuideSceneUpgrader] " + host.name
            + (created ? " に" + label + "用の遅延バッファを追加しました" : " の" + label + "用の遅延バッファを更新しました")
            + "（" + buffer.delayFrames + "フレーム@" + buffer.captureFps + "fps ≈ "
            + (buffer.DelaySeconds * 1000f).ToString("F0") + "ms 遅延）");
        return buffer;
    }

    /// <summary>
    /// 実験制御・ロガーへ左目用（マスター）・右目用の ChannelCompositor を配線する．
    /// </summary>
    /// <remarks>
    /// <see cref="ChannelCompositor.mirrorFrom"/> はパラメータの同期のみを行い，
    /// コンポーネント自体の有効/無効（enabled）は同期できない（無効化されたコンポーネントは
    /// LateUpdate 自体が呼ばれないため）．そのため <c>FollowingExperimentManager</c> /
    /// <c>ReplayPlayer</c> 側に <c>channelCompositorRight</c> フィールドを追加し，
    /// K キー等での ON/OFF 切替時に両目へ反映するようにした（10 仕様拡張）．
    /// </remarks>
    private static void WireExperimentControl(Scene scene, ChannelCompositor masterCompositor,
        ChannelCompositor rightCompositor)
    {
        foreach (ReplayPlayer replay in CollectComponents<ReplayPlayer>(scene))
        {
            Undo.RecordObject(replay, "Wire ChannelCompositor");
            replay.channelCompositor = masterCompositor;
            replay.channelCompositorRight = rightCompositor;
            EditorUtility.SetDirty(replay);
        }

        foreach (FollowingExperimentManager mgr in CollectComponents<FollowingExperimentManager>(scene))
        {
            Undo.RecordObject(mgr, "Wire ChannelCompositor");
            mgr.channelCompositor = masterCompositor;
            mgr.channelCompositorRight = rightCompositor;
            EditorUtility.SetDirty(mgr);
        }

        foreach (FollowingLogger logger in CollectComponents<FollowingLogger>(scene))
        {
            Undo.RecordObject(logger, "Wire ChannelCompositor");
            logger.compositor = masterCompositor;
            if (logger.manager == null)
            {
                List<FollowingExperimentManager> mgrs = CollectComponents<FollowingExperimentManager>(scene);
                if (mgrs.Count > 0) logger.manager = mgrs[0];
            }
            EditorUtility.SetDirty(logger);
        }

        Debug.Log("[ConeGuideSceneUpgrader] 実験制御・ロガーに左目用 ChannelCompositor を配線しました"
            + "（右目用への有効/無効の反映は未対応）");
    }

    /// <summary>
    /// シーン内の既存カメラすべてから錐の全レイヤを除外する．
    /// 箱・ガイド用カメラ（<see cref="BoxCameraNames"/>）は錐を映すのが役目なので対象外にする．
    /// </summary>
    /// <returns>変更したカメラの台数</returns>
    private static int ExcludeConeLayersFromExistingCameras(Scene scene)
    {
        int mask = ConeGuideLayers.GuideMask;
        int count = 0;

        foreach (Camera cam in CollectComponents<Camera>(scene))
        {
            if (System.Array.IndexOf(BoxCameraNames, cam.name) >= 0) continue; // 箱・ガイド用カメラは除外しない
            if ((cam.cullingMask & mask) == 0) continue;                        // 既に除外済み

            Undo.RecordObject(cam, "Exclude cone layers");
            cam.cullingMask &= ~mask;
            EditorUtility.SetDirty(cam);
            count++;
            Debug.Log("[ConeGuideSceneUpgrader] " + cam.name + " の Culling Mask から錐レイヤを除外しました");
        }
        return count;
    }

    // ==================== 探索ヘルパー ====================

    /// <summary>
    /// シーン内（非アクティブを含む）から候補名のいずれかに一致する Transform を返す．
    /// 候補は先頭から順に探し，最初に見つかったものを返す．
    /// </summary>
    private static Transform FindFirst(Scene scene, string[] names)
    {
        foreach (string name in names)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);
                if (found != null) return found;
            }
        }
        return null;
    }

    /// <summary>子孫を再帰的に探索して名前が一致する Transform を返す</summary>
    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>指定名の Transform を探し，その Camera コンポーネントを返す（無ければ null）</summary>
    private static Camera FindCamera(Scene scene, string name)
    {
        Transform t = FindFirst(scene, new[] { name });
        return t != null ? t.GetComponent<Camera>() : null;
    }

    /// <summary>指定名の Transform を探し，その RawImage コンポーネントを返す（無ければ null）</summary>
    private static RawImage FindRawImage(Scene scene, string name)
    {
        Transform t = FindFirst(scene, new[] { name });
        return t != null ? t.GetComponent<RawImage>() : null;
    }

    /// <summary>シーン内（非アクティブを含む）の全 T を集める</summary>
    private static List<T> CollectComponents<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            list.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return list;
    }
}
