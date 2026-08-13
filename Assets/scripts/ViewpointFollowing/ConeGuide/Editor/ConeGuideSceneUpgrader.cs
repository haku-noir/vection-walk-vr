using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 四角錐ガイド（09 仕様）を<b>現在開いているシーンに追加</b>するエディタスクリプト．
///
/// 既存のシーン生成メニュー（ViewpointFollowingSceneBuilder）とは独立しており，
/// <b>既存シーンを作り直さない</b>（上書き生成しない）．すでに構築済みの
/// ViewpointFollowing.unity / ViewpointFollowingReplay.unity に対して，
/// 足りない要素だけを差分で足していく（何度実行しても重複しない＝冪等）．
///
/// メニュー: Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加
/// </summary>
/// <remarks>
/// M1 で行うこと:
/// - レイヤ ConeOther / ConeSelf を ProjectSettings/TagManager.asset に登録
/// - Cone_Other（頂点＝収録軌跡の視点）/ Cone_Self（頂点＝ライブ頭部）を作成し追従対象を配線
/// - 既存カメラの Culling Mask から両レイヤを除外する
///
/// M2 で行うこと:
/// - 箱用 RenderTexture（Box_Other / Box_Self，アルファ付き・深度付き）を用意
/// - LiveBoxCam / GhostBoxCam を各背景カメラの子として作成（透明クリア・該当レイヤのみ）
/// - ChannelCompositor を ViewSwitcher と同じオブジェクトに追加し，4入力を配線
///   （<b>既定は無効</b>．有効にしたときだけ新しい2チャンネル合成経路に切り替わる）
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

    // 箱用 RenderTexture（ライブ映像用 CenterEye の複製として作る＝解像度・形式を揃える）
    private const string LiveRTPath = "Assets/Textures/CenterEye.renderTexture";
    private const string BoxOtherRTPath = "Assets/Textures/BoxOther.renderTexture";
    private const string BoxSelfRTPath = "Assets/Textures/BoxSelf.renderTexture";

    private const string LiveBoxCamName = "LiveBoxCam";
    private const string GhostBoxCamName = "GhostBoxCam";

    /// <summary>箱カメラの名前（M2 で追加．Culling Mask の除外対象から外すために使う）</summary>
    private static readonly string[] BoxCameraNames = { "LiveBoxCam", "GhostBoxCam" };

    /// <summary>ライブ視点のカメラ／頭部アンカーの候補名（実験シーン / 再生確認シーン）</summary>
    private static readonly string[] LiveAnchorNames = { "CenterEyeAnchor", "LiveReplayCamera" };

    /// <summary>収録視点のカメラの候補名（実験シーン / 再生確認シーン）</summary>
    private static readonly string[] GhostAnchorNames = { "GhostCamera", "GhostReplayCamera" };

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
        if (otherLayer < 0 || selfLayer < 0)
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
                "\nViewpointFollowing.unity または ViewpointFollowingReplay.unity を開いて実行してください。",
                "OK");
            return;
        }

        Shader coneShader = AssetDatabase.LoadAssetAtPath<Shader>(ConeLineShaderPath);

        // --- 3. 錐を2つ用意する（既にあれば作り直さず配線だけ更新する） ---
        // Cone_Other: 頂点＝収録軌跡の視点。ライブ映像側にのみ描画される
        ConeGuide coneOther = EnsureCone(scene, ConeOtherObjectName,
            ConeGuide.ConeKind.Other, otherLayer, ghostAnchor, coneShader);
        // Cone_Self: 頂点＝ライブ頭部の視点。収録映像側にのみ描画される
        ConeGuide coneSelf = EnsureCone(scene, ConeSelfObjectName,
            ConeGuide.ConeKind.Self, selfLayer, liveAnchor, coneShader);

        // 2つの錐は同じ見えでなければならない（片方だけ変えると誤差ゼロでも差が残る）。
        // Cone_Self が Cone_Other から幾何・見た目・姿勢処理の条件を引く形にしておく
        if (coneSelf.mirrorFrom != coneOther)
        {
            Undo.RecordObject(coneSelf, "Sync cone parameters");
            coneSelf.mirrorFrom = coneOther;
            EditorUtility.SetDirty(coneSelf);
            Debug.Log("[ConeGuideSceneUpgrader] Cone_Self のパラメータを Cone_Other に同期させました"
                + "（以後は Cone_Other 側を編集してください）");
        }

        // --- 4. 箱用 RenderTexture（アルファ付き・深度付き）を用意する ---
        RenderTexture boxOtherRT = EnsureBoxRenderTexture(BoxOtherRTPath);
        RenderTexture boxSelfRT = EnsureBoxRenderTexture(BoxSelfRTPath);
        if (boxOtherRT == null || boxSelfRT == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "箱用 RenderTexture を用意できませんでした。\n複製元: " + LiveRTPath, "OK");
            return;
        }

        // --- 5. 箱カメラを背景カメラの子として作る（姿勢と投影を完全に一致させる） ---
        Camera liveBgCam = FindLiveCaptureCamera(scene);
        Camera ghostBgCam = ghostAnchor.GetComponent<Camera>();
        if (liveBgCam == null || ghostBgCam == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "背景カメラが見つかりません（ライブ: CenterEyeCapture / LiveReplayCamera、"
                + "収録: GhostCamera / GhostReplayCamera）。", "OK");
            return;
        }
        Camera liveBoxCam = EnsureBoxCamera(scene, LiveBoxCamName, liveBgCam, otherLayer, boxOtherRT);
        Camera ghostBoxCam = EnsureBoxCamera(scene, GhostBoxCamName, ghostBgCam, selfLayer, boxSelfRT);

        // --- 6. ChannelCompositor を用意して4入力を配線する（既定は無効） ---
        string compositorNote = EnsureCompositor(scene, coneOther, coneSelf,
            boxOtherRT, boxSelfRT, liveBoxCam, ghostBoxCam);

        // --- 7. 既存カメラの Culling Mask から両レイヤを除外する ---
        int excluded = ExcludeConeLayersFromExistingCameras(scene, otherLayer, selfLayer);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = coneOther.gameObject;

        EditorUtility.DisplayDialog("錐ガイドを追加しました",
            "シーン: " + scene.name + "\n\n" +
            "レイヤ: " + ConeGuideLayers.ConeOtherName + " (" + otherLayer + ") / "
            + ConeGuideLayers.ConeSelfName + " (" + selfLayer + ")\n" +
            "Cone_Other → " + ghostAnchor.name + " に追従（" + LiveBoxCamName + " が撮影）\n" +
            "Cone_Self  → " + liveAnchor.name + " に追従（" + GhostBoxCamName + " が撮影）\n" +
            "既存カメラ " + excluded + " 台から錐レイヤを除外しました。\n" +
            compositorNote + "\n\n" +
            "【使い方】ChannelCompositor のチェックを入れると2チャンネル合成経路に\n" +
            "切り替わります（オフの間は従来どおり ViewSwitcher が表示を担当）。\n" +
            "背景モード・箱モード・極性・周波数は Inspector で切り替えられます。\n\n" +
            "※シーンは自動保存していません。内容を確認して手動で保存してください。",
            "OK");
    }

    /// <summary>
    /// M1 の見た目確認用: ライブ視点のカメラに錐レイヤを一時的に映す／戻す．
    /// 本来 錐は箱カメラからのみ見えるため，このトグルは<b>確認専用</b>である．
    /// </summary>
    [MenuItem("Tools/視点追従実験/錐ガイド: プレビュー表示を切替")]
    public static void TogglePreview()
    {
        Scene scene = SceneManager.GetActiveScene();
        Camera liveCam = FindLiveCaptureCamera(scene);
        if (liveCam == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "ライブ映像のカメラ（CenterEyeCapture / LiveReplayCamera）が見つかりません。", "OK");
            return;
        }

        int mask = (1 << ConeGuideLayers.OtherLayer) | (1 << ConeGuideLayers.SelfLayer);
        bool showing = (liveCam.cullingMask & mask) != 0;

        Undo.RecordObject(liveCam, "Toggle Cone Guide Preview");
        liveCam.cullingMask = showing
            ? (liveCam.cullingMask & ~mask)   // 元に戻す（除外）
            : (liveCam.cullingMask | mask);   // プレビュー表示（両方の錐を映す）
        EditorUtility.SetDirty(liveCam);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[ConeGuideSceneUpgrader] 錐ガイドのプレビュー表示: "
            + (showing ? "OFF（本来の設定に戻しました）" : "ON（" + liveCam.name + " に両方の錐を映しています）")
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
            // 既定値は ConePoseFilter のフィールド初期値（ヨー=Raw / ピッチ=LPF 0.5Hz / ロール=Zero）
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
    /// 箱用 RenderTexture を用意する．ライブ映像用の CenterEye を複製して作るので
    /// 解像度・形式がライブ映像と揃う．<b>アルファ付き</b>（線の存在＝箱マスクを運ぶため）と
    /// <b>深度付き</b>（稜線オクルージョンに必要）であることを明示的に確認する．
    /// </summary>
    private static RenderTexture EnsureBoxRenderTexture(string assetPath)
    {
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
        if (rt == null)
        {
            if (!AssetDatabase.CopyAsset(LiveRTPath, assetPath))
            {
                Debug.LogError("[ConeGuideSceneUpgrader] RenderTexture の複製に失敗しました: " + LiveRTPath);
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
    /// 箱カメラを用意する．背景カメラの<b>子</b>として localPosition=0 / localRotation=identity で
    /// 置くことで，姿勢が常に背景カメラと完全一致する（仕様 §2.1）．
    /// 該当レイヤだけを描き，背景を透明でクリアする．
    /// </summary>
    private static Camera EnsureBoxCamera(Scene scene, string cameraName, Camera source,
        int layer, RenderTexture target)
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
        cam.CopyFrom(source);                              // FOV・near/far・投影を背景カメラに合わせる
        cam.cullingMask = 1 << layer;                      // その錐だけを描く
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 透明クリア（アルファ0＝箱がない場所）
        cam.targetTexture = target;
        cam.stereoTargetEye = StereoTargetEyeMask.None;    // HMD へ直接出力しない
        cam.depth = source.depth + 1;                      // 背景カメラの後に描く
        cam.useOcclusionCulling = false;                   // 錐しか描かないので不要
        cam.allowHDR = false;
        cam.allowMSAA = false;

        // 背景カメラの子にして姿勢を完全一致させる．
        // CopyFrom が投影行列を書き換える可能性があるため，Transform の確定は必ずこの後に行う
        Undo.SetTransformParent(go.transform, source.transform, "Parent " + cameraName);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        cam.ResetWorldToCameraMatrix();  // CopyFrom が持ち込みうる固定行列を捨て，親追従に戻す
        cam.ResetProjectionMatrix();
        cam.ResetAspect();               // targetTexture のアスペクトから計算し直させる

        EditorUtility.SetDirty(cam);
        Debug.Log("[ConeGuideSceneUpgrader] " + cameraName + (existing != null ? " を更新" : " を作成")
            + "しました（親: " + source.name + ", レイヤ: " + layer + ", 出力: " + target.name + "）");
        return cam;
    }

    /// <summary>
    /// ChannelCompositor を ViewSwitcher と同じオブジェクトに用意し，4入力を配線する．
    /// 表示先・背景テクスチャは既存 ViewSwitcher の配線をそのまま流用するため，
    /// シーン構成に依存した名前探索をしなくて済む．
    /// </summary>
    /// <returns>ダイアログに出す1行の結果メッセージ</returns>
    private static string EnsureCompositor(Scene scene, ConeGuide coneOther, ConeGuide coneSelf,
        RenderTexture boxOtherRT, RenderTexture boxSelfRT, Camera liveBoxCam, Camera ghostBoxCam)
    {
        List<ViewSwitcher> switchers = CollectComponents<ViewSwitcher>(scene);
        if (switchers.Count == 0)
        {
            Debug.LogWarning("[ConeGuideSceneUpgrader] ViewSwitcher が見つからないため ChannelCompositor は追加しませんでした");
            return "ChannelCompositor: ViewSwitcher が無いため未追加";
        }
        ViewSwitcher switcher = switchers[0];
        GameObject host = switcher.gameObject;

        ChannelCompositor compositor = host.GetComponent<ChannelCompositor>();
        bool created = compositor == null;
        if (created)
        {
            compositor = Undo.AddComponent<ChannelCompositor>(host);
            // 既定は無効。オンにしたときだけ新しい合成経路に切り替わる
            // （オフの間は ViewSwitcher の従来動作がそのまま残る）
            compositor.enabled = false;
        }

        Undo.RecordObject(compositor, "Configure ChannelCompositor");
        compositor.viewSwitcher = switcher;
        compositor.rawImage = switcher.rawImage;
        compositor.bgLiveTexture = switcher.liveTexture;
        compositor.bgGhostTexture = switcher.playbackTexture;
        compositor.boxOtherTexture = boxOtherRT;
        compositor.boxSelfTexture = boxSelfRT;
        compositor.coneOther = coneOther;
        compositor.coneSelf = coneSelf;
        compositor.liveBoxCamera = liveBoxCam;
        compositor.ghostBoxCamera = ghostBoxCam;
        if (compositor.shader == null)
        {
            compositor.shader = AssetDatabase.LoadAssetAtPath<Shader>(ChannelCompositeShaderPath);
        }

        EditorUtility.SetDirty(compositor);

        // 再生確認シーンでは ReplayPlayer が表示モードと連動して合成器を制御する
        // （錐ガイドは Reswitch＝収録後の再合成が土俵なので、そこでのみ有効になる）
        foreach (ReplayPlayer replay in CollectComponents<ReplayPlayer>(scene))
        {
            Undo.RecordObject(replay, "Wire ChannelCompositor");
            replay.channelCompositor = compositor;
            EditorUtility.SetDirty(replay);
            Debug.Log("[ConeGuideSceneUpgrader] ReplayPlayer に ChannelCompositor を配線しました"
                + "（ReplayPlayer > Cone Guide Enabled をオンにし、表示モードを Reswitch にすると有効）");
        }

        // 実験シーンでは FollowingExperimentManager が K キーで合成器を制御する
        foreach (FollowingExperimentManager mgr in CollectComponents<FollowingExperimentManager>(scene))
        {
            Undo.RecordObject(mgr, "Wire ChannelCompositor");
            mgr.channelCompositor = compositor;
            EditorUtility.SetDirty(mgr);
            Debug.Log("[ConeGuideSceneUpgrader] FollowingExperimentManager に ChannelCompositor を"
                + "配線しました（停止中に K キーで錐ガイドの ON/OFF）");
        }

        // ロガーは表示ソースと箱条件を合成器から、初期オフセット量を管理クラスから取る
        foreach (FollowingLogger logger in CollectComponents<FollowingLogger>(scene))
        {
            Undo.RecordObject(logger, "Wire ChannelCompositor");
            logger.compositor = compositor;
            if (logger.manager == null)
            {
                List<FollowingExperimentManager> mgrs = CollectComponents<FollowingExperimentManager>(scene);
                if (mgrs.Count > 0) logger.manager = mgrs[0];
            }
            EditorUtility.SetDirty(logger);
            Debug.Log("[ConeGuideSceneUpgrader] FollowingLogger に ChannelCompositor と"
                + " FollowingExperimentManager を配線しました");
        }

        Debug.Log("[ConeGuideSceneUpgrader] ChannelCompositor を " + host.name
            + (created ? " に追加しました（既定は無効）" : " で更新しました"));
        return "ChannelCompositor: " + host.name + (created ? " に追加（既定は無効）" : " を更新");
    }

    /// <summary>
    /// シーン内の既存カメラすべてから錐レイヤを除外する．
    /// 箱カメラ（M2 で追加）は錐を映すのが役目なので対象外にする．
    /// </summary>
    /// <returns>変更したカメラの台数</returns>
    private static int ExcludeConeLayersFromExistingCameras(Scene scene, int otherLayer, int selfLayer)
    {
        int mask = (1 << otherLayer) | (1 << selfLayer);
        int count = 0;

        foreach (Camera cam in CollectComponents<Camera>(scene))
        {
            if (System.Array.IndexOf(BoxCameraNames, cam.name) >= 0) continue; // 箱カメラは除外しない
            if ((cam.cullingMask & mask) == 0) continue;                        // 既に除外済み

            Undo.RecordObject(cam, "Exclude cone layers");
            cam.cullingMask &= ~mask;
            EditorUtility.SetDirty(cam);
            count++;
            Debug.Log("[ConeGuideSceneUpgrader] " + cam.name + " の Culling Mask から錐レイヤを除外しました");
        }
        return count;
    }

    /// <summary>
    /// ライブ映像を撮っているカメラを探す（プレビュー表示の対象）
    /// </summary>
    private static Camera FindLiveCaptureCamera(Scene scene)
    {
        foreach (string name in new[] { "CenterEyeCapture", "LiveReplayCamera" })
        {
            Transform t = FindFirst(scene, new[] { name });
            if (t != null)
            {
                Camera cam = t.GetComponent<Camera>();
                if (cam != null) return cam;
            }
        }
        return null;
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
