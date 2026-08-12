using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
/// シーンは自動保存しない（既存シーンを勝手に上書きしないため）．
/// 内容を確認してから手動で保存すること．
/// </remarks>
public static class ConeGuideSceneUpgrader
{
    private const string ConeOtherObjectName = "Cone_Other";
    private const string ConeSelfObjectName = "Cone_Self";
    private const string ConeLineShaderPath =
        "Assets/scripts/ViewpointFollowing/ConeGuide/ConeLine.shader";

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
        EnsureCone(scene, ConeSelfObjectName,
            ConeGuide.ConeKind.Self, selfLayer, liveAnchor, coneShader);

        // --- 4. 既存カメラの Culling Mask から両レイヤを除外する ---
        int excluded = ExcludeConeLayersFromExistingCameras(scene, otherLayer, selfLayer);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = coneOther.gameObject;

        EditorUtility.DisplayDialog("錐ガイドを追加しました",
            "シーン: " + scene.name + "\n\n" +
            "レイヤ: " + ConeGuideLayers.ConeOtherName + " (" + otherLayer + ") / "
            + ConeGuideLayers.ConeSelfName + " (" + selfLayer + ")\n" +
            "Cone_Other → " + ghostAnchor.name + " に追従\n" +
            "Cone_Self  → " + liveAnchor.name + " に追従\n" +
            "既存カメラ " + excluded + " 台から錐レイヤを除外しました。\n\n" +
            "錐は箱カメラ（M2で追加）からのみ見えるため、現時点では Game ビューに映りません。\n" +
            "見た目を確認するには Scene ビュー、または\n" +
            "「Tools > 視点追従実験 > 錐ガイド: プレビュー表示を切替」を使ってください。\n\n" +
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

        Undo.RecordObject(cone, "Configure " + objectName);
        cone.kind = kind;
        cone.target = target;
        if (shader != null) cone.shader = shader;
        go.layer = layer;

        EditorUtility.SetDirty(cone);
        Debug.Log("[ConeGuideSceneUpgrader] " + objectName + (created ? " を作成しました" : " を更新しました")
            + "（追従対象: " + target.name + ", レイヤ: " + layer + "）");
        return cone;
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
