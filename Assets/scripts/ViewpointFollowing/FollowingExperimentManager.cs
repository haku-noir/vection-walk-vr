using UnityEngine;

/// <summary>
/// 視点追従実験の全体進行を管理するクラス．
/// 「収録モード（Record）」と「実験モード（Follow）」の2モードを持つ．
///
/// ■ 収録モード: 実験者/被験者が歩行し，頭部軌跡を CSV に収録する
/// ■ 実験モード: 収録軌跡をゴーストカメラで再生し，ライブ映像と一定周波数で
/// 　交互に HMD へ提示しながら歩行させ，追従データを記録する
///
/// 操作方法（既存実験の操作体系を踏襲）:
/// - Oキー / Bボタン        : 実験の開始・停止（保存なし。練習・動作確認用）
/// - Pキー / Yボタン        : 自動保存つき開始（本番用。停止時にCSVを自動保存）
/// - 停止中に Sキー / 右中指トリガー : CSV 手動保存
/// - 停止中に Mキー / Aボタン       : モード切替（Record ⇔ Follow）
/// </summary>
public class FollowingExperimentManager : MonoBehaviour
{
    /// <summary>
    /// 実験モード
    /// </summary>
    public enum Mode
    {
        /// <summary>収録走：頭部軌跡を記録する</summary>
        Record,
        /// <summary>実験走：収録映像とライブ映像を交互提示し追従を測る</summary>
        Follow,
    }

    /// <summary>
    /// 現在のモード（停止中に M キー / A ボタンでも切替可能）
    /// </summary>
    [Tooltip("現在のモード（停止中に Mキー/Aボタン でも切替可能）")]
    public Mode mode = Mode.Record;

    [Header("参照（シーンビルダーが自動設定）")]
    /// <summary>軌跡の収録クラス</summary>
    [Tooltip("軌跡の収録クラス")]
    public TrajectoryRecorder recorder;
    /// <summary>軌跡の再生クラス</summary>
    [Tooltip("軌跡の再生クラス")]
    public TrajectoryPlayer player;
    /// <summary>映像切替クラス</summary>
    [Tooltip("映像切替クラス")]
    public ViewSwitcher switcher;
    /// <summary>追従データの記録クラス</summary>
    [Tooltip("追従データの記録クラス")]
    public FollowingLogger followingLogger;
    /// <summary>
    /// 停止中に視野へ色を付ける PostProcessVolume（既存実験と同じポーズ演出）
    /// </summary>
    [Tooltip("停止中に視野へ色を付ける PostProcessVolume")]
    public GameObject postprocess;

    /// <summary>実験実行中か（false = 一時停止中）</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Inspector からのモード変更を検知するための前回値</summary>
    private Mode lastMode;

    /// <summary>
    /// この試行の終了時に CSV を自動保存するか
    /// （Pキー / Yボタンで開始した場合に true．毎回ファイルが増えるのを避けるため，
    /// 通常の Oキー / Bボタン開始では自動保存しない）
    /// </summary>
    private bool autoSaveOnStop;

    private void Start()
    {
        // 既存実験と同様，停止状態（時間停止・視野マスク）から開始する
        SetRunning(false);
        // 収録映像用カメラはモードに応じて有効化する
        lastMode = mode;
        UpdateGhostCameraActive();
    }

    private void Update()
    {
        // Inspector から直接モードを変更された場合にも GhostCamera の有効状態を追随させる
        // （Mキー/Aボタン以外の経路でモードが変わると GhostCamera が無効のままになるのを防ぐ）
        if (mode != lastMode)
        {
            lastMode = mode;
            UpdateGhostCameraActive();
            Debug.Log("[FollowingExperiment] モード切替: " + mode);
        }

        // --- 開始・停止のトグル（Oキー / Bボタン: 保存なしの開始） ---
        if (Input.GetKeyDown(KeyCode.O) || OVRInput.GetDown(OVRInput.RawButton.B))
        {
            if (IsRunning)
            {
                StopTrial();
            }
            else
            {
                StartTrial(false);
            }
        }

        // --- 自動保存つき開始（Pキー / Yボタン: 本番試行用） ---
        // 停止時（再生終了による自動停止を含む）に CSV を自動保存する
        if (!IsRunning && (Input.GetKeyDown(KeyCode.P) || OVRInput.GetDown(OVRInput.RawButton.Y)))
        {
            StartTrial(true);
        }

        if (!IsRunning)
        {
            // --- 保存（停止中に Sキー / 右中指トリガー） ---
            if (Input.GetKeyDown(KeyCode.S) || OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
            {
                SaveCurrentData();
            }

            // --- モード切替（停止中に Mキー / Aボタン） ---
            if (Input.GetKeyDown(KeyCode.M) || OVRInput.GetDown(OVRInput.RawButton.A))
            {
                mode = (mode == Mode.Record) ? Mode.Follow : Mode.Record;
                // GhostCamera の有効化とログ出力は Update 冒頭のモード変更検知に任せる
            }
        }
        else
        {
            // 実験モードで軌跡を最後まで再生し終えたら自動停止する
            if (mode == Mode.Follow && player != null && player.IsFinished)
            {
                Debug.Log("[FollowingExperiment] 軌跡の再生が終了したため自動停止します");
                StopTrial();
            }
        }
    }

    /// <summary>
    /// 試行を開始する（時間を動かし，視野マスクを外す）
    /// </summary>
    /// <param name="autoSave"> true なら試行の終了時に CSV を自動保存する </param>
    private void StartTrial(bool autoSave)
    {
        if (mode == Mode.Record)
        {
            // 収録走：ライブ映像のみ提示し，軌跡の記録を開始
            switcher.mode = ViewSwitcher.SourceMode.LiveOnly;
            switcher.ResetPhase();
            recorder.StartRecording();
        }
        else // Mode.Follow
        {
            // 実験走：軌跡を読み込み，再生・交互提示・追従記録を開始
            if (!player.EnsureLoaded())
            {
                Debug.LogWarning("[FollowingExperiment] 軌跡ファイルがないため開始できません．先に Record モードで収録してください．");
                return;
            }
            player.StartPlayback();
            switcher.mode = ViewSwitcher.SourceMode.Alternate;
            switcher.ResetPhase(); // 必ずライブ映像から提示を始める
            followingLogger.StartLogging();
        }

        autoSaveOnStop = autoSave;
        SetRunning(true);
        Debug.Log("[FollowingExperiment] 開始 (" + mode + (autoSave ? ", 自動保存あり" : ", 保存なし") + ")");
    }

    /// <summary>
    /// 試行を停止する（時間を止め，視野マスクを掛ける）．データはメモリに保持される．
    /// </summary>
    private void StopTrial()
    {
        if (mode == Mode.Record)
        {
            recorder.StopRecording();
        }
        else
        {
            player.StopPlayback();
            followingLogger.StopLogging();
        }

        SetRunning(false);

        // 自動保存つき開始（Pキー / Yボタン）だった場合はここで CSV を保存する
        if (autoSaveOnStop)
        {
            autoSaveOnStop = false;
            SaveCurrentData();
            Debug.Log("[FollowingExperiment] 停止・自動保存しました");
        }
        else
        {
            Debug.Log("[FollowingExperiment] 停止（Sキー/右中指トリガーで保存できます）");
        }
    }

    /// <summary>
    /// 現在のモードに応じたデータを CSV 保存する
    /// </summary>
    private void SaveCurrentData()
    {
        if (mode == Mode.Record)
        {
            recorder.SaveToCsv();
        }
        else
        {
            followingLogger.SaveToCsv();
        }
    }

    /// <summary>
    /// 実行状態を切り替える（Time.timeScale と視野マスクを連動）
    /// </summary>
    private void SetRunning(bool running)
    {
        IsRunning = running;
        // 既存実験と同じ方式: 停止中は世界の時間を止め，PostProcess で視野に色を付ける
        Time.timeScale = running ? 1f : 0f;
        if (postprocess != null)
        {
            postprocess.SetActive(!running);
        }
    }

    /// <summary>
    /// ゴーストカメラは Follow モードでのみ動かす（Record 中の無駄な描画を避ける）
    /// </summary>
    private void UpdateGhostCameraActive()
    {
        if (player != null && player.ghostCamera != null)
        {
            player.ghostCamera.gameObject.SetActive(mode == Mode.Follow);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ実行時のみ，画面左上に現在の状態を表示する（実験者用）
    /// </summary>
    private void OnGUI()
    {
        string state = IsRunning
            ? "実行中" + (autoSaveOnStop ? "（自動保存あり）" : "（保存なし）")
            : "停止中（O:開始 / P:自動保存つき開始 / S:保存 / M:モード切替）";
        string freq = switcher != null ? switcher.switchFrequency.ToString("F1") + " Hz" : "-";
        string loaded = (player != null && player.IsLoaded)
            ? "読込済 " + player.Duration.ToString("F1") + "s" : "未読込";
        GUI.Label(new Rect(10, 10, 600, 20), "モード: " + mode + "  |  " + state);
        GUI.Label(new Rect(10, 30, 600, 20), "切替周波数: " + freq + "  |  軌跡: " + loaded);

        // Follow 実行中は，表示ソース・再生時刻・両者の位置を表示して動作確認しやすくする
        if (IsRunning && mode == Mode.Follow && switcher != null && player != null)
        {
            string src = switcher.CurrentSource == 0 ? "ライブ" : "収録（デバッグ着色時はオレンジ）";
            GUI.Label(new Rect(10, 50, 700, 20),
                "表示中: " + src + "  |  再生時刻: " + player.CurrentTime.ToString("F1") + " / " + player.Duration.ToString("F1") + " s");
            if (recorder != null && recorder.headAnchor != null)
            {
                GUI.Label(new Rect(10, 70, 700, 20),
                    "ライブ頭部: " + recorder.headAnchor.position.ToString("F2") + "  |  収録位置: " + player.CurrentPosition.ToString("F2"));
            }
        }
    }
#endif
}
