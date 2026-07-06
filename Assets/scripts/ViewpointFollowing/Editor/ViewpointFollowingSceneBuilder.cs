using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 視点追従実験シーン（ViewpointFollowing.unity）を自動構築するエディタスクリプト．
/// Unity メニューの「Tools > 視点追従実験 > 実験シーンを生成」から実行する．
///
/// 生成内容:
/// - Player プレハブ（既存の視野映像パイプラインをそのまま利用．視野反転などの不要な機能は無効化）
/// - GhostCamera（収録軌跡を再生して PlaybackEye RenderTexture に描画するカメラ）
/// - ExperimentRig（実験管理・収録・再生・切替・記録の各スクリプト，参照配線済み）
/// - 歩行コース環境（高密度/低密度の切替可能なオブジェクト群，床，開始/終了マーカー）
/// - PostProcessVolume（停止中の視野マスク用）
/// </summary>
public static class ViewpointFollowingSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ViewpointFollowing.unity";
    private const string LiveRTPath = "Assets/Textures/CenterEye.renderTexture";
    private const string PlaybackRTPath = "Assets/Textures/PlaybackEye.renderTexture";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PostProcessPrefabPath = "Assets/Prefabs/PostProcessVolume.prefab";

    // 環境オブジェクト用のカラーパレット（乱数を使わず決定論的に配色する）
    private static readonly Color[] Palette =
    {
        new Color(0.85f, 0.33f, 0.28f), // 赤
        new Color(0.27f, 0.55f, 0.85f), // 青
        new Color(0.95f, 0.77f, 0.25f), // 黄
        new Color(0.40f, 0.75f, 0.42f), // 緑
        new Color(0.70f, 0.50f, 0.85f), // 紫
    };
    private static readonly Dictionary<int, Material> materialCache = new Dictionary<int, Material>();

    [MenuItem("Tools/視点追従実験/実験シーンを生成")]
    public static void BuildScene()
    {
        // 未保存の変更があれば先にユーザーへ保存を促す
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        materialCache.Clear();

        // --- 1. 新規シーン（Directional Light 付き）を作成 ---
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Main Camera は Player プレハブ内のカメラと競合するため削除する
        GameObject mainCam = GameObject.Find("Main Camera");
        if (mainCam != null) Object.DestroyImmediate(mainCam);

        // --- 2. 床・環境・マーカーを構築 ---
        BuildFloorAndMarkers();
        GameObject envRich, envSparse;
        BuildEnvironment(out envRich, out envSparse);

        // --- 3. 収録映像用の RenderTexture（PlaybackEye）を用意 ---
        // ライブ映像用の CenterEye と同じ設定になるよう，アセットを複製して作る
        RenderTexture playbackRT = AssetDatabase.LoadAssetAtPath<RenderTexture>(PlaybackRTPath);
        if (playbackRT == null)
        {
            if (!AssetDatabase.CopyAsset(LiveRTPath, PlaybackRTPath))
            {
                EditorUtility.DisplayDialog("エラー", "RenderTexture の複製に失敗しました:\n" + LiveRTPath, "OK");
                return;
            }
            playbackRT = AssetDatabase.LoadAssetAtPath<RenderTexture>(PlaybackRTPath);
        }

        // --- 4. Player プレハブを配置し，不要な機能を無効化 ---
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            EditorUtility.DisplayDialog("エラー", "Player プレハブが見つかりません:\n" + PlayerPrefabPath, "OK");
            return;
        }
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = Vector3.zero; // 開始地点 = 原点，+z 方向へ歩行する

        // 視野反転（SetReversion）とコントローラ移動（PlayerInput）はこの実験では使わない
        foreach (var reversion in player.GetComponentsInChildren<SetReversion>(true)) reversion.enabled = false;
        foreach (var playerInput in player.GetComponentsInChildren<PlayerInput>(true)) playerInput.enabled = false;
        // OVRPlayerController / CharacterController も余計な動作の元なので無効化（引継ぎ資料の方針どおり）
        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
        {
            if (b != null && b.GetType().Name == "OVRPlayerController") b.enabled = false;
        }
        foreach (var charCtrl in player.GetComponentsInChildren<CharacterController>(true)) charCtrl.enabled = false;

        // 両眼視差は既存実験と同様に非対応（中央系のみ使用）
        var rig = player.GetComponentInChildren<OVRCameraRig>(true);
        if (rig != null) rig.usePerEyeCameras = false;
        // 左右眼用の Canvas は SetReversion を無効化した代わりに明示的に消しておく
        SetActiveIfFound(player.transform, "LeftCanvas", false);
        SetActiveIfFound(player.transform, "RightCanvas", false);

        // 映像パイプラインの構成要素を取得
        Transform centerEyeAnchor = FindDeep(player.transform, "CenterEyeAnchor");
        Transform centerEyeCapture = FindDeep(player.transform, "CenterEyeCapture");
        Transform centerRawImageTr = FindDeep(player.transform, "CenterRawImage");
        if (centerEyeAnchor == null || centerEyeCapture == null || centerRawImageTr == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "Player プレハブ内に CenterEyeAnchor / CenterEyeCapture / CenterRawImage が見つかりません．", "OK");
            return;
        }
        Camera captureCam = centerEyeCapture.GetComponent<Camera>();
        RawImage centerRawImage = centerRawImageTr.GetComponent<RawImage>();

        // --- 5. GhostCamera（収録映像の再レンダリング用カメラ）を作成 ---
        GameObject ghost = new GameObject("GhostCamera");
        ghost.transform.position = new Vector3(0f, 1.5f, 0f);
        Camera ghostCam = ghost.AddComponent<Camera>();
        ghostCam.CopyFrom(captureCam);                     // FOV・カリングマスク等をライブ映像用カメラに合わせる
        ghostCam.targetTexture = playbackRT;               // 出力先だけ PlaybackEye に変更
        ghostCam.stereoTargetEye = StereoTargetEyeMask.None; // HMD へ直接出力しない

        // --- 6. PostProcessVolume（停止中の視野マスク）を配置 ---
        GameObject postprocess = null;
        GameObject ppPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PostProcessPrefabPath);
        if (ppPrefab != null)
        {
            postprocess = (GameObject)PrefabUtility.InstantiatePrefab(ppPrefab);
            postprocess.name = "PostProcessVolume"; // 既存スクリプトの慣習に合わせて名前を固定
        }

        // --- 7. ExperimentRig（実験管理オブジェクト）を作成し，全参照を配線 ---
        GameObject rigGO = new GameObject("ExperimentRig");
        var recorder = rigGO.AddComponent<TrajectoryRecorder>();
        var trajPlayer = rigGO.AddComponent<TrajectoryPlayer>();
        var switcher = rigGO.AddComponent<ViewSwitcher>();
        var logger = rigGO.AddComponent<FollowingLogger>();
        var manager = rigGO.AddComponent<FollowingExperimentManager>();
        var envSwitcher = rigGO.AddComponent<EnvironmentSwitcher>();

        recorder.headAnchor = centerEyeAnchor;

        trajPlayer.ghostCamera = ghost.transform;

        switcher.rawImage = centerRawImage;
        // ライブ映像テクスチャ: RawImage に設定済みのもの（= CenterEye）を優先し，無ければアセットから取得
        switcher.liveTexture = centerRawImage.texture != null
            ? centerRawImage.texture
            : AssetDatabase.LoadAssetAtPath<RenderTexture>(LiveRTPath);
        switcher.playbackTexture = playbackRT;

        logger.headAnchor = centerEyeAnchor;
        logger.player = trajPlayer;
        logger.switcher = switcher;

        manager.recorder = recorder;
        manager.player = trajPlayer;
        manager.switcher = switcher;
        manager.followingLogger = logger;
        manager.postprocess = postprocess;

        envSwitcher.envRich = envRich;
        envSwitcher.envSparse = envSparse;

        // --- 8. シーンを保存 ---
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = rigGO;
        EditorUtility.DisplayDialog("視点追従実験シーンを生成しました",
            "保存先: " + ScenePath + "\n\n" +
            "使い方（詳細は docs/viewpoint-following/07 参照）:\n" +
            "1. Record モードで O キー → 歩行 → O → S で軌跡を収録\n" +
            "2. M キーで Follow モードへ切替\n" +
            "3. O キー → 歩行 → 自動停止 → S で追従データを保存\n\n" +
            "切替周波数は ExperimentRig の ViewSwitcher，\n環境密度は 1/2/3 キーで変更できます。",
            "OK");
    }

    /// <summary>
    /// 床と開始・終了マーカーを作成する
    /// </summary>
    private static void BuildFloorAndMarkers()
    {
        // 床（40m×40m，歩行コースの中心 z=10m あたりに配置）
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, 0f, 10f);
        floor.transform.localScale = new Vector3(4f, 1f, 4f);
        floor.GetComponent<Renderer>().sharedMaterial = GetMaterial(new Color(0.75f, 0.75f, 0.72f));

        // 開始地点マーカー（緑）と終了地点マーカー（赤）．コースは z = 0 → 10m の直線を想定
        CreateBox(null, "StartMarker", new Vector3(0f, 0.01f, 0f), new Vector3(1.5f, 0.02f, 0.3f), new Color(0.2f, 0.8f, 0.3f));
        CreateBox(null, "GoalMarker", new Vector3(0f, 0.01f, 10f), new Vector3(1.5f, 0.02f, 0.3f), new Color(0.9f, 0.25f, 0.2f));
    }

    /// <summary>
    /// 歩行コース沿いの環境オブジェクト群（高密度/低密度）を作成する．
    /// オプティカルフロー量の操作変数となるため，乱数を使わず決定論的に配置する．
    /// </summary>
    private static void BuildEnvironment(out GameObject rich, out GameObject sparse)
    {
        rich = new GameObject("Env_Rich");
        sparse = new GameObject("Env_Sparse");

        // --- 高密度: 通路脇（x=±1.5m）に 1m おきの柱 ＋ 外側（x=±4m）に 2m おきの球 ---
        for (int i = 0; i <= 10; i++)
        {
            float h = 0.4f + ((i * 3) % 5) * 0.25f; // 高さを決定論的に変化させる
            CreateBox(rich.transform, "PillarL_" + i, new Vector3(-1.5f, h / 2f, i), new Vector3(0.35f, h, 0.35f), Palette[i % Palette.Length]);
            CreateBox(rich.transform, "PillarR_" + i, new Vector3(1.5f, h / 2f, i), new Vector3(0.35f, h, 0.35f), Palette[(i + 2) % Palette.Length]);
        }
        for (int i = 0; i <= 5; i++)
        {
            CreateSphere(rich.transform, "SphereL_" + i, new Vector3(-4f, 0.5f, i * 2f), 1f, Palette[(i + 1) % Palette.Length]);
            CreateSphere(rich.transform, "SphereR_" + i, new Vector3(4f, 0.5f, i * 2f), 1f, Palette[(i + 3) % Palette.Length]);
        }

        // --- 低密度: 通路脇 5m おきの柱のみ ---
        for (int i = 0; i <= 2; i++)
        {
            CreateBox(sparse.transform, "PillarL_" + i, new Vector3(-1.5f, 0.5f, i * 5f), new Vector3(0.35f, 1f, 0.35f), Palette[i % Palette.Length]);
            CreateBox(sparse.transform, "PillarR_" + i, new Vector3(1.5f, 0.5f, i * 5f), new Vector3(0.35f, 1f, 0.35f), Palette[(i + 2) % Palette.Length]);
        }

        sparse.SetActive(false); // 初期状態は高密度（EnvironmentSwitcher が管理）
    }

    // ==================== 生成ヘルパー ====================

    private static void CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
    }

    private static void CreateSphere(Transform parent, string name, Vector3 pos, float diameter, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * diameter;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
    }

    /// <summary>
    /// 同じ色のマテリアルを使い回す（シーン内に埋め込まれる）
    /// </summary>
    private static Material GetMaterial(Color color)
    {
        int key = color.GetHashCode();
        Material mat;
        if (!materialCache.TryGetValue(key, out mat) || mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            mat.name = "EnvMat_" + materialCache.Count;
            mat.color = color;
            materialCache[key] = mat;
        }
        return mat;
    }

    /// <summary>
    /// 子孫を再帰的に探索して名前が一致する Transform を返す
    /// </summary>
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

    /// <summary>
    /// 名前で子孫オブジェクトを探し，見つかればアクティブ状態を設定する
    /// </summary>
    private static void SetActiveIfFound(Transform root, string name, bool active)
    {
        Transform t = FindDeep(root, name);
        if (t != null) t.gameObject.SetActive(active);
    }
}
