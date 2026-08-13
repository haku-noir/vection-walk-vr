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
/// - 停止中に 4 / V キー            : 背景の4ストローク ON/OFF・極性
///
/// 四角錐ガイド（09 仕様）の条件切替も停止中のみ有効:
/// - K : 錐ガイド経路の ON/OFF（オフなら従来と完全に同じ挙動）
/// - G : 背景チャンネル巡回（Live固定 → Ghost固定 → 矩形波交替 → 4ストローク）
/// - C : 箱チャンネル巡回（Off → Other → Self → 矩形波交替 → 4ストローク）
/// - B : 箱の4ストローク極性巡回（Enhance → Reversal → Zero）
/// - ↑/↓ : f_bg ±0.5Hz　／　←/→ : f_box ±0.5Hz
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

    /// <summary>
    /// Follow 開始時に，今立っている位置と向き（ヨー）を収録開始時点に自動で一致させるか．
    /// OVRCameraRig を回転・平行移動して合わせるため，被験者の立ち位置合わせが不要になる．
    /// </summary>
    [Tooltip("Follow開始時に、現在の立ち位置と向きを収録開始時点に自動で合わせる")]
    public bool alignToRecordingOnStart = true;

    [Header("初期オフセット（仕様 09 §3.4。整合の後にこの量だけずらす）")]
    /// <summary>
    /// 初期オフセット・横[m]．コース進行方向 +Z に対する右向き（+X）が正．
    /// </summary>
    [Tooltip("初期オフセット・横[m]（+X = コース進行方向に対して右）")]
    public float initialOffsetLateral = 0f;

    /// <summary>
    /// 初期オフセット・前後[m]．コース進行方向（+Z）が正＝収録より前に立つ．
    /// </summary>
    [Tooltip("初期オフセット・前後[m]（+Z = コース進行方向。収録より前に立つ）")]
    public float initialOffsetForward = 0f;

    /// <summary>
    /// 初期オフセット・ヨー[deg]．収録開始時の向きに対する右回りが正．
    /// </summary>
    [Tooltip("初期オフセット・ヨー[deg]（収録開始時の向きに対して右回りが正）")]
    public float initialOffsetYawDeg = 0f;

    /// <summary>
    /// 実際に適用された初期オフセット（x=横[m], y=ヨー[deg], z=前後[m]）．
    /// 整合をオフにした試行では適用されないので Vector3.zero のままになる．ロガーが記録する．
    /// </summary>
    public Vector3 AppliedOffset { get; private set; }

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
    /// 四角錐ガイドの2チャンネル合成器（09 仕様）．未設定なら錐ガイド関連の操作は無効．
    /// </summary>
    [Tooltip("錐ガイドの2チャンネル合成器（未設定なら錐ガイドの操作は無効）")]
    public ChannelCompositor channelCompositor;
    /// <summary>
    /// 停止中に視野へ色を付ける PostProcessVolume（既存実験と同じポーズ演出）
    /// </summary>
    [Tooltip("停止中に視野へ色を付ける PostProcessVolume")]
    public GameObject postprocess;

    /// <summary>
    /// 錐ガイド（背景＋箱の2チャンネル合成）を使うか．停止中に K キーで切替．
    /// オフの間は ChannelCompositor が無効化され，従来の ViewSwitcher 経路が
    /// そのまま動く（＝従来と完全に同じ挙動）．
    /// </summary>
    [Tooltip("錐ガイドを使うか（停止中に K キー。オフなら従来と完全に同じ挙動）")]
    public bool coneGuideEnabled = false;

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

        // TrajectoryPlayer の headAnchor が未配線ならレコーダーと同じものを流用する
        // （シーンを作り直さなくても PositionOnly / RotationOnly を使えるようにするため）
        if (player != null && player.headAnchor == null && recorder != null)
        {
            player.headAnchor = recorder.headAnchor;
        }

        // 4ストローク合成器が未配線なら自動で追加する
        // （シーンを作り直さなくても 4 キーで 4ストローク提示を使えるようにするため）
        if (switcher != null && switcher.fourStroke == null)
        {
            switcher.fourStroke = switcher.GetComponent<FourStrokeCompositor>();
            if (switcher.fourStroke == null)
            {
                switcher.fourStroke = switcher.gameObject.AddComponent<FourStrokeCompositor>();
            }
        }
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

        // 錐ガイド経路の有効・無効を反映する（Kキー・Inspector のどちらの変更にも追随）．
        // 収録走では素のライブ映像を見せたいので Follow モードでのみ有効にする
        if (channelCompositor != null)
        {
            bool wantCone = coneGuideEnabled && mode == Mode.Follow;
            if (channelCompositor.enabled != wantCone) channelCompositor.enabled = wantCone;
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

            // --- 4ストローク提示の ON/OFF（停止中に 4キー。試行中の条件変更を防ぐ） ---
            // 錐ガイド経路では背景チャンネルの 矩形波交替 ⇔ 4ストローク を切り替える
            // （キーの意味「背景の4ストローク ON/OFF」は従来と同じ）
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                if (ConeGuideActive)
                {
                    channelCompositor.bgMode =
                        channelCompositor.bgMode == ChannelCompositor.BackgroundMode.FourStroke
                            ? ChannelCompositor.BackgroundMode.SquareAlternate
                            : ChannelCompositor.BackgroundMode.FourStroke;
                    Debug.Log("[FollowingExperiment] 背景チャンネル: " + channelCompositor.bgMode);
                }
                else if (switcher != null)
                {
                    switcher.fourStrokeEnabled = !switcher.fourStrokeEnabled;
                    Debug.Log("[FollowingExperiment] 4ストローク提示: "
                        + (switcher.fourStrokeEnabled ? "ON" : "OFF"));
                }
            }

            // --- 4ストロークの極性巡回（停止中に Vキー: Enhance → Reversal → Zero） ---
            // 錐ガイド経路では「背景チャンネルの」極性を回す（箱の極性は B キー）
            if (Input.GetKeyDown(KeyCode.V))
            {
                if (ConeGuideActive)
                {
                    channelCompositor.bgPolarity = NextPolarity(channelCompositor.bgPolarity);
                    Debug.Log("[FollowingExperiment] 背景の4ストローク極性: " + channelCompositor.bgPolarity);
                }
                else if (switcher != null && switcher.fourStroke != null)
                {
                    switcher.fourStroke.polarity = NextPolarity(switcher.fourStroke.polarity);
                    Debug.Log("[FollowingExperiment] 4ストローク極性: " + switcher.fourStroke.polarity);
                }
            }

            HandleConeGuideKeys();
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

    /// <summary>錐ガイド経路が実際に動いているか</summary>
    private bool ConeGuideActive
    {
        get { return channelCompositor != null && channelCompositor.enabled; }
    }

    /// <summary>4ストローク極性の巡回順（Enhance → Reversal → Zero）</summary>
    private static FourStrokeCompositor.Polarity NextPolarity(FourStrokeCompositor.Polarity p)
    {
        switch (p)
        {
            case FourStrokeCompositor.Polarity.Enhance: return FourStrokeCompositor.Polarity.Reversal;
            case FourStrokeCompositor.Polarity.Reversal: return FourStrokeCompositor.Polarity.Zero;
            default: return FourStrokeCompositor.Polarity.Enhance;
        }
    }

    /// <summary>
    /// 錐ガイド（09 仕様）の条件切替キーを処理する．<b>停止中のみ</b>呼ばれる
    /// （試行中に条件が変わらないようにするため）．
    ///
    /// 既存の割り当て（O/P/S/M/4/V/1/2/3）とは衝突しない:
    /// - K   : 錐ガイド経路の ON/OFF
    /// - G   : 背景チャンネル巡回（Live固定 → Ghost固定 → 矩形波交替 → 4ストローク）
    /// - C   : 箱チャンネル巡回（Off → Other → Self → 矩形波交替 → 4ストローク）
    /// - B   : 箱の4ストローク極性巡回（Enhance → Reversal → Zero）
    /// - ↑/↓ : f_bg ±0.5Hz
    /// - ←/→ : f_box ±0.5Hz（同期がオンなら自動的に解除する）
    /// </summary>
    private void HandleConeGuideKeys()
    {
        // --- K: 錐ガイド経路の ON/OFF ---
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (channelCompositor == null)
            {
                Debug.LogWarning("[FollowingExperiment] ChannelCompositor が未設定のため錐ガイドを使えません。"
                    + "メニュー「Tools > 視点追従実験 > 錐ガイドを現在のシーンに追加」を実行してください。");
            }
            else
            {
                coneGuideEnabled = !coneGuideEnabled;
                Debug.Log("[FollowingExperiment] 錐ガイド: " + (coneGuideEnabled ? "ON" : "OFF"));
            }
        }

        if (!ConeGuideActive) return;

        // --- G: 背景チャンネル巡回 ---
        if (Input.GetKeyDown(KeyCode.G))
        {
            channelCompositor.bgMode = (ChannelCompositor.BackgroundMode)
                (((int)channelCompositor.bgMode + 1) % 4);
            Debug.Log("[FollowingExperiment] 背景チャンネル: " + channelCompositor.bgMode);
        }

        // --- C: 箱チャンネル巡回 ---
        if (Input.GetKeyDown(KeyCode.C))
        {
            channelCompositor.boxMode = (ChannelCompositor.BoxMode)
                (((int)channelCompositor.boxMode + 1) % 5);
            Debug.Log("[FollowingExperiment] 箱チャンネル: " + channelCompositor.boxMode);
        }

        // --- B: 箱の4ストローク極性巡回 ---
        if (Input.GetKeyDown(KeyCode.B))
        {
            channelCompositor.boxPolarity = NextPolarity(channelCompositor.boxPolarity);
            Debug.Log("[FollowingExperiment] 箱の4ストローク極性: " + channelCompositor.boxPolarity);
        }

        // --- ↑/↓: f_bg ±0.5Hz（ViewSwitcher.switchFrequency が周波数の持ち主） ---
        if (switcher != null)
        {
            float df = (Input.GetKeyDown(KeyCode.UpArrow) ? 0.5f : 0f)
                + (Input.GetKeyDown(KeyCode.DownArrow) ? -0.5f : 0f);
            if (df != 0f)
            {
                switcher.switchFrequency = Mathf.Clamp(switcher.switchFrequency + df, 0.1f, 10f);
                Debug.Log("[FollowingExperiment] f_bg: " + switcher.switchFrequency.ToString("F1") + " Hz");
            }
        }

        // --- ←/→: f_box ±0.5Hz ---
        {
            float df = (Input.GetKeyDown(KeyCode.RightArrow) ? 0.5f : 0f)
                + (Input.GetKeyDown(KeyCode.LeftArrow) ? -0.5f : 0f);
            if (df != 0f)
            {
                // 同期したままでは f_box を動かせないので、操作された時点で同期を外す
                if (channelCompositor.syncBoxFreqToBg)
                {
                    channelCompositor.syncBoxFreqToBg = false;
                    channelCompositor.boxFrequency = channelCompositor.BgFrequency;
                    Debug.Log("[FollowingExperiment] f_box の同期を解除しました（独立設定に切替）");
                }
                channelCompositor.boxFrequency =
                    Mathf.Clamp(channelCompositor.boxFrequency + df, 0.1f, 10f);
                Debug.Log("[FollowingExperiment] f_box: " + channelCompositor.boxFrequency.ToString("F1") + " Hz");
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
            // 今立っている位置・向きを収録開始時点に合わせる（開始誤差をゼロに揃える）
            AppliedOffset = Vector3.zero; // 整合をしない試行ではオフセットも適用されない
            if (alignToRecordingOnStart)
            {
                AlignToRecordingStart();
            }
            player.StartPlayback();
            switcher.mode = ViewSwitcher.SourceMode.Alternate;
            switcher.ResetPhase(); // 必ずライブ映像から提示を始める
            // 錐ガイド経路のときは2チャンネルの位相と箱の姿勢フィルタも揃えてリセットする
            if (channelCompositor != null && channelCompositor.enabled)
            {
                channelCompositor.ResetPhase();
            }
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
    /// 今立っている位置と向き（ヨー）を，収録軌跡の開始時点に一致させる．
    /// HMD 自体は動かせないため，親である OVRCameraRig を「現在の頭部位置を中心に」
    /// 回転させてから平行移動することで，仮想世界側を合わせる．
    /// </summary>
    /// <remarks>
    /// - 合わせるのは水平位置（XZ）とヨーのみ．高さ（Y）は被験者自身の目の高さを維持する
    ///   （収録者と身長が違っても床の高さが変わらないようにするため）．
    /// - これにより「収録時と同じ場所で視点リセットする」手順が不要になる．
    ///   現在向いている実方向が，仮想空間での収録開始方向（コース進行方向）になる．
    /// </remarks>
    private void AlignToRecordingStart()
    {
        Transform head = recorder != null ? recorder.headAnchor : null;
        if (head == null)
        {
            Debug.LogWarning("[FollowingExperiment] headAnchor が未設定のため開始地点合わせをスキップします");
            return;
        }
        OVRCameraRig rig = head.GetComponentInParent<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogWarning("[FollowingExperiment] OVRCameraRig が見つからないため開始地点合わせをスキップします");
            return;
        }
        Transform root = rig.transform;

        // 1) ヨーを合わせる: 現在の頭部位置を軸に rig ごと回転（頭部位置は変わらない）
        float deltaYaw = Mathf.DeltaAngle(head.eulerAngles.y, player.StartRotation.eulerAngles.y);
        root.RotateAround(head.position, Vector3.up, deltaYaw);

        // 2) 水平位置を合わせる: 頭部が収録開始位置（XZ）に来るよう rig を平行移動
        Vector3 delta = player.StartPosition - head.position;
        delta.y = 0f; // 高さは合わせない（被験者自身の目の高さを維持）
        root.position += delta;

        Debug.Log("[FollowingExperiment] 開始地点合わせ: Δyaw=" + deltaYaw.ToString("F1")
            + "° 移動=" + new Vector2(delta.x, delta.z).magnitude.ToString("F2") + "m");

        // 3) 初期オフセット（仕様 09 §3.4）: 整合の「後」に指定量だけずらす．
        //    誤差ゼロから始めるのではなく，与えた誤差からどう復帰するかを見る条件用
        ApplyInitialOffset(root, head);
    }

    /// <summary>
    /// 収録開始時点への整合が済んだ状態から，指定量だけ立ち位置・向きをずらす．
    /// ずらすのは仮想世界側（OVRCameraRig）なので，被験者は実空間で動かなくてよい．
    /// </summary>
    /// <remarks>
    /// 座標系はコース基準（+Z = 進行方向, +X = 進行方向に対して右）．
    /// 誤差の成分分解（errLat / errFwd / errYaw）と同じ軸なので，
    /// 与えた誤差と記録される誤差が直接対応する．
    /// </remarks>
    private void ApplyInitialOffset(Transform root, Transform head)
    {
        if (Mathf.Approximately(initialOffsetLateral, 0f)
            && Mathf.Approximately(initialOffsetForward, 0f)
            && Mathf.Approximately(initialOffsetYawDeg, 0f))
        {
            return; // オフセットなし（従来どおり誤差ゼロから開始）
        }

        // ヨー: 現在の頭部位置を軸に rig ごと回す（頭部の位置は変わらない）
        if (!Mathf.Approximately(initialOffsetYawDeg, 0f))
        {
            root.RotateAround(head.position, Vector3.up, initialOffsetYawDeg);
        }

        // 横・前後: rig を平行移動して頭部をコース座標系でずらす
        root.position += new Vector3(initialOffsetLateral, 0f, initialOffsetForward);

        AppliedOffset = new Vector3(initialOffsetLateral, initialOffsetYawDeg, initialOffsetForward);
        Debug.Log("[FollowingExperiment] 初期オフセット: 横 " + initialOffsetLateral.ToString("F2")
            + "m / 前後 " + initialOffsetForward.ToString("F2")
            + "m / ヨー " + initialOffsetYawDeg.ToString("F1") + "°");
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
            : "停止中（O:開始 / P:保存つき開始 / S:保存 / M:モード / 4:4ストローク / V:背景極性"
              + " / K:錐ガイド / G:背景 / C:箱 / B:箱極性 / ↑↓:f_bg / ←→:f_box）";
        string freq = switcher != null ? switcher.switchFrequency.ToString("F1") + " Hz" : "-";
        string loaded = (player != null && player.IsLoaded)
            ? "読込済 " + player.Duration.ToString("F1") + "s" : "未読込";
        string components = player != null ? player.playbackComponents.ToString() : "-";
        // 錐ガイド使用中は背景の4ストロークが背景チャンネル側の条件になるので，
        // 従来経路の状態を出すと条件を取り違える
        string fourStroke = ConeGuideActive ? "—（錐ガイドの背景チャンネル参照）"
            : (switcher != null && switcher.fourStrokeEnabled)
                ? "ON (" + (switcher.fourStroke != null ? switcher.fourStroke.polarity.ToString() : "?") + ")"
                : "OFF";
        GUI.Label(new Rect(10, 10, 1000, 20), "モード: " + mode + "  |  " + state);
        GUI.Label(new Rect(10, 30, 900, 20),
            "切替周波数: " + freq + "  |  軌跡: " + loaded + "  |  再生成分: " + components
            + "  |  4ストローク: " + fourStroke);

        int y = 50;

        // 錐ガイドの条件（09 仕様）。条件の取り違えを防ぐため実行中も出し続ける
        if (channelCompositor != null)
        {
            string cone = !coneGuideEnabled ? "OFF"
                : ConeGuideActive
                    ? "ON  背景=" + channelCompositor.bgMode
                        + (channelCompositor.bgMode == ChannelCompositor.BackgroundMode.FourStroke
                            ? "(" + channelCompositor.bgPolarity + ")" : "")
                        + "  箱=" + channelCompositor.boxMode
                        + (channelCompositor.boxMode == ChannelCompositor.BoxMode.FourStroke
                            ? "(" + channelCompositor.boxPolarity + ")" : "")
                        + "  f_box=" + channelCompositor.BoxFrequency.ToString("F1") + "Hz"
                        + (channelCompositor.syncBoxFreqToBg ? "(同期)" : "")
                    : "ON（Followモードで反映）";
            GUI.Label(new Rect(10, y, 1000, 20), "錐ガイド: " + cone);
            y += 20;
        }

        // 初期オフセット条件（0 のときは出さない）
        if (initialOffsetLateral != 0f || initialOffsetForward != 0f || initialOffsetYawDeg != 0f)
        {
            GUI.Label(new Rect(10, y, 900, 20),
                "初期オフセット: 横 " + initialOffsetLateral.ToString("F2")
                + "m / 前後 " + initialOffsetForward.ToString("F2")
                + "m / ヨー " + initialOffsetYawDeg.ToString("F1") + "°"
                + (alignToRecordingOnStart ? "" : "（整合オフのため適用されません）"));
            y += 20;
        }

        // Follow 実行中は，表示ソース・再生時刻・両者の位置を表示して動作確認しやすくする
        if (IsRunning && mode == Mode.Follow && switcher != null && player != null)
        {
            int source = ConeGuideActive ? channelCompositor.CurrentSource : switcher.CurrentSource;
            string src = source == 0 ? "ライブ" : "収録（デバッグ着色時はオレンジ）";
            GUI.Label(new Rect(10, y, 900, 20),
                "表示中: " + src + "  |  再生時刻: " + player.CurrentTime.ToString("F1") + " / " + player.Duration.ToString("F1") + " s");
            y += 20;
            if (recorder != null && recorder.headAnchor != null)
            {
                Vector3 diff = recorder.headAnchor.position - player.CurrentPosition;
                GUI.Label(new Rect(10, y, 900, 20),
                    "ライブ頭部: " + recorder.headAnchor.position.ToString("F2")
                    + "  |  収録位置: " + player.CurrentPosition.ToString("F2")
                    + "  |  誤差 横" + diff.x.ToString("F2") + " 前後" + diff.z.ToString("F2") + " m");
            }
        }
    }
#endif
}
