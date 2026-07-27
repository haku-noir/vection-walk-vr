using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 4ストローク歩行シーンの進行を管理するクラス．
/// 歩行中のライブ映像と，その数百ms前の「過去の自分の映像」を
/// 4ストローク合成（FourStrokeCompositor）して HMD に提示する．
/// 頭部軌跡は視点追従実験と同じ形式（trajectory_*.csv）で収録できる．
///
/// 操作方法（既存実験の操作体系を踏襲 + 元プログラムの調整キー）:
/// - Oキー / Bボタン        : 開始・停止（保存なし。練習・動作確認用）
/// - Pキー / Yボタン        : 自動保存つき開始（停止時に軌跡CSVを自動保存）
/// - 停止中に Sキー / 右中指トリガー : 軌跡CSVの手動保存
/// - 4キー : 4ストローク合成の ON/OFF（OFF時はライブ映像をそのまま表示）
/// - Vキー : 極性切替（Enhance → Reversal → Zero の巡回）
/// - ↑/↓ : 変調周波数 ±0.1Hz
/// - ←/→ : 遅延フレーム数 ∓1（@captureFps）
/// - 1/2/3 : 環境密度（EnvironmentSwitcher）
/// </summary>
public class FourStrokeSelfManager : MonoBehaviour
{
    [Header("4ストローク（シーンビルダーが自動設定）")]
    /// <summary>4ストローク合成器</summary>
    [Tooltip("4ストローク合成器")]
    public FourStrokeCompositor compositor;
    /// <summary>過去映像のリングバッファ</summary>
    [Tooltip("過去映像のリングバッファ")]
    public DelayedFrameBuffer delayBuffer;
    /// <summary>4ストローク合成を有効にするか（4キーでも切替可能）</summary>
    [Tooltip("4ストローク合成を有効にするか（4キーでも切替可能）")]
    public bool fourStrokeEnabled = true;

    [Header("映像出力")]
    /// <summary>視野として表示している UI（Player プレハブ内の CenterRawImage）</summary>
    [Tooltip("視野として表示しているUI（CenterRawImage）")]
    public RawImage rawImage;
    /// <summary>ライブ映像のテクスチャ（CenterEye RenderTexture）</summary>
    [Tooltip("ライブ映像のテクスチャ（CenterEye RenderTexture）")]
    public Texture liveTexture;

    [Header("収録・進行")]
    /// <summary>頭部軌跡の収録クラス</summary>
    [Tooltip("頭部軌跡の収録クラス")]
    public TrajectoryRecorder recorder;
    /// <summary>停止中に視野へ色を付ける PostProcessVolume（既存実験と同じポーズ演出）</summary>
    [Tooltip("停止中に視野へ色を付ける PostProcessVolume")]
    public GameObject postprocess;

    /// <summary>実行中か（false = 一時停止中）</summary>
    public bool IsRunning { get; private set; }

    /// <summary>この試行の終了時に軌跡CSVを自動保存するか（Pキー/Yボタン開始時のみ true）</summary>
    private bool autoSaveOnStop;

    private void Start()
    {
        // 既存実験と同様，停止状態（時間停止・視野マスク）から開始する
        SetRunning(false);
    }

    private void Update()
    {
        HandleTrialKeys();
        HandleFourStrokeKeys();
        ApplyVideoRouting();
    }

    /// <summary>
    /// 試行の開始・停止・保存キーを処理する（FollowingExperimentManager と同じ操作体系）
    /// </summary>
    private void HandleTrialKeys()
    {
        // --- 開始・停止のトグル（Oキー / Bボタン: 保存なしの開始） ---
        if (Input.GetKeyDown(KeyCode.O) || OVRInput.GetDown(OVRInput.RawButton.B))
        {
            if (IsRunning) StopTrial();
            else StartTrial(false);
        }

        // --- 自動保存つき開始（Pキー / Yボタン: 本番試行用） ---
        if (!IsRunning && (Input.GetKeyDown(KeyCode.P) || OVRInput.GetDown(OVRInput.RawButton.Y)))
        {
            StartTrial(true);
        }

        // --- 保存（停止中に Sキー / 右中指トリガー） ---
        if (!IsRunning && (Input.GetKeyDown(KeyCode.S) || OVRInput.GetDown(OVRInput.RawButton.RHandTrigger)))
        {
            if (recorder != null) recorder.SaveToCsv();
        }
    }

    /// <summary>
    /// 4ストロークの調整キーを処理する（元プログラムのキー体系に準拠）
    /// </summary>
    private void HandleFourStrokeKeys()
    {
        // ON/OFF（4キー）
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            fourStrokeEnabled = !fourStrokeEnabled;
            Debug.Log("[FourStrokeSelf] 4ストローク: " + (fourStrokeEnabled ? "ON" : "OFF"));
        }

        if (compositor == null) return;

        // 極性巡回（Vキー: Enhance → Reversal → Zero）
        if (Input.GetKeyDown(KeyCode.V))
        {
            compositor.polarity = NextPolarity(compositor.polarity);
            Debug.Log("[FourStrokeSelf] 極性: " + compositor.polarity);
        }

        // 変調周波数 ±0.1Hz（↑/↓）
        if (Input.GetKeyDown(KeyCode.UpArrow))
            compositor.frequency = Mathf.Clamp(compositor.frequency + 0.1f, 0.02f, 10f);
        if (Input.GetKeyDown(KeyCode.DownArrow))
            compositor.frequency = Mathf.Clamp(compositor.frequency - 0.1f, 0.02f, 10f);

        // 遅延フレーム数 ±1（←/→。元プログラムと同じく → で増加）
        if (delayBuffer != null)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
                delayBuffer.delayFrames = Mathf.Clamp(delayBuffer.delayFrames + 1, 0, 29);
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                delayBuffer.delayFrames = Mathf.Clamp(delayBuffer.delayFrames - 1, 0, 29);
        }
    }

    /// <summary>
    /// 映像の配線を毎フレーム反映する
    /// （4ストロークON: ライブ+遅延映像の合成を表示 / OFF: ライブをそのまま表示）
    /// </summary>
    private void ApplyVideoRouting()
    {
        bool useFourStroke = fourStrokeEnabled && compositor != null && delayBuffer != null;

        if (compositor != null) compositor.enabled = useFourStroke; // OFF中は無駄なBlitを止める

        if (useFourStroke)
        {
            compositor.currentTexture = liveTexture;
            compositor.delayedTexture = delayBuffer.DelayedTexture;
        }

        if (rawImage != null)
        {
            // 合成出力は初回のBlitまで null のことがあるため，その間はライブを表示する
            rawImage.texture = (useFourStroke && compositor.OutputTexture != null)
                ? compositor.OutputTexture
                : liveTexture;
        }
    }

    /// <summary>
    /// 極性の巡回順（元プログラムの V キーと同じ Enhance → Reversal → Zero）
    /// </summary>
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
    /// 試行を開始する（時間を動かし，視野マスクを外し，軌跡の収録を開始）
    /// </summary>
    private void StartTrial(bool autoSave)
    {
        if (compositor != null) compositor.ResetPhase(); // 必ず現在フレームの提示から始める
        if (recorder != null) recorder.StartRecording();
        autoSaveOnStop = autoSave;
        SetRunning(true);
        Debug.Log("[FourStrokeSelf] 開始 (" + (autoSave ? "自動保存あり" : "保存なし") + ")");
    }

    /// <summary>
    /// 試行を停止する（時間を止め，視野マスクを掛ける）
    /// </summary>
    private void StopTrial()
    {
        if (recorder != null) recorder.StopRecording();
        SetRunning(false);

        if (autoSaveOnStop)
        {
            autoSaveOnStop = false;
            if (recorder != null) recorder.SaveToCsv();
            Debug.Log("[FourStrokeSelf] 停止・自動保存しました");
        }
        else
        {
            Debug.Log("[FourStrokeSelf] 停止（Sキー/右中指トリガーで保存できます）");
        }
    }

    /// <summary>
    /// 実行状態を切り替える（Time.timeScale と視野マスクを連動．既存実験と同じ方式）
    /// </summary>
    private void SetRunning(bool running)
    {
        IsRunning = running;
        Time.timeScale = running ? 1f : 0f;
        if (postprocess != null) postprocess.SetActive(!running);
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ実行時のみ，画面左上に現在の状態を表示する（実験者用）
    /// </summary>
    private void OnGUI()
    {
        string state = IsRunning
            ? "実行中" + (autoSaveOnStop ? "（自動保存あり）" : "（保存なし）")
            : "停止中（O:開始 / P:自動保存つき開始 / S:保存）";
        GUI.Label(new Rect(10, 10, 700, 20), "4ストローク歩行  |  " + state);

        string fs = fourStrokeEnabled ? "ON" : "OFF";
        string freq = compositor != null ? compositor.frequency.ToString("F1") + " Hz" : "-";
        string polarity = compositor != null ? compositor.polarity.ToString() : "-";
        string delay = delayBuffer != null
            ? delayBuffer.delayFrames + "フレーム (" + (delayBuffer.DelaySeconds * 1000f).ToString("F0") + "ms)"
            : "-";
        GUI.Label(new Rect(10, 30, 700, 20),
            "4ストローク: " + fs + "  |  周波数: " + freq + "  |  極性: " + polarity + "  |  遅延: " + delay);
        GUI.Label(new Rect(10, 50, 700, 20),
            "4:ON/OFF  V:極性  ↑↓:周波数  ←→:遅延  1/2/3:環境密度");

        if (IsRunning && recorder != null)
        {
            GUI.Label(new Rect(10, 70, 700, 20), "収録サンプル数: " + recorder.SampleCount);
        }
    }
#endif
}
