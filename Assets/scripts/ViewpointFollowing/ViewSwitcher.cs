using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HMD に提示する映像ソースを，ライブ映像（CenterEyeCapture の出力）と
/// 収録映像（ゴーストカメラの出力）とで一定周波数で交互に切替えるクラス．
/// CenterRawImage の texture を差し替えるだけなので GPU 負荷はほぼゼロ．
/// </summary>
/// <remarks>
/// 切替周波数の定義: switchFrequency = 1 Hz のとき「ライブ→収録→ライブ」が1秒で1周
/// （各ソースが 0.5 秒ずつ表示される，デューティ比 50% の矩形波切替）．
/// 切替タイマーは Time.deltaTime で進むため，一時停止（timeScale=0）中は切替も止まる．
/// </remarks>
public class ViewSwitcher : MonoBehaviour
{
    /// <summary>
    /// 映像ソースの提示モード
    /// </summary>
    public enum SourceMode
    {
        /// <summary>ライブ映像のみ（収録走・統制条件用）</summary>
        LiveOnly,
        /// <summary>収録映像のみ（確認・統制条件用）</summary>
        PlaybackOnly,
        /// <summary>ライブ⇔収録を switchFrequency で交互切替（本実験条件）</summary>
        Alternate,
    }

    /// <summary>
    /// 視野として表示している UI（Player プレハブ内の CenterRawImage）
    /// </summary>
    [Tooltip("視野として表示しているUI（CenterRawImage）")]
    public RawImage rawImage;

    /// <summary>
    /// ライブ映像のテクスチャ（CenterEyeCapture のターゲットテクスチャ = CenterEye）
    /// </summary>
    [Tooltip("ライブ映像のテクスチャ（CenterEye RenderTexture）")]
    public Texture liveTexture;

    /// <summary>
    /// 収録映像のテクスチャ（ゴーストカメラのターゲットテクスチャ = PlaybackEye）
    /// </summary>
    [Tooltip("収録映像のテクスチャ（PlaybackEye RenderTexture）")]
    public Texture playbackTexture;

    /// <summary>
    /// 切替周波数[Hz]（1周期 = ライブ＋収録の1往復）
    /// </summary>
    [Tooltip("切替周波数[Hz]（1周期=ライブ+収録の1往復。各ソースは 1/(2f) 秒ずつ表示）")]
    [Range(0.1f, 10f)] public float switchFrequency = 1f;

    /// <summary>
    /// 現在の提示モード
    /// </summary>
    [Tooltip("現在の提示モード")]
    public SourceMode mode = SourceMode.LiveOnly;

    /// <summary>
    /// デバッグ用: 収録映像の表示中は視野をオレンジ色に着色して，どちらのソースが
    /// 表示されているかを判別できるようにする．**本番実験では必ずオフにすること．**
    /// </summary>
    [Tooltip("デバッグ用: 収録映像の表示中は視野をオレンジ色に着色する（本番実験ではオフ）")]
    public bool debugTint = true;

    [Header("4ストローク合成（オプション）")]
    /// <summary>
    /// 4ストローク合成器（未設定でも矩形波切替は従来どおり動作する．
    /// FollowingExperimentManager が起動時に自動配線する）
    /// </summary>
    [Tooltip("4ストローク合成器（未設定でも矩形波切替は動作する）")]
    public FourStrokeCompositor fourStroke;

    /// <summary>
    /// Alternate 時に矩形波切替の代わりに4ストローク合成
    /// （グレースケール化＋反転＋台形波クロスフェード）で提示するか．
    /// 変調周波数は switchFrequency を共用する．停止中に 4 キーでも切替できる．
    /// </summary>
    [Tooltip("Alternate時に矩形波切替の代わりに4ストローク合成で提示する（周波数はswitchFrequencyを共用）")]
    public bool fourStrokeEnabled = false;

    /// <summary>収録映像表示中のデバッグ着色</summary>
    private static readonly Color PlaybackTint = new Color(1f, 0.75f, 0.45f, 1f);

    /// <summary>
    /// 今表示しているソース（0 = ライブ, 1 = 収録）．ロガーが記録に使う．
    /// </summary>
    public int CurrentSource { get; private set; }

    private float elapsed;         // 現在のソースの表示継続時間
    private SourceMode lastMode;   // モード変更検知用
    private bool lastFourStroke;   // 4ストローク ON/OFF の変更検知用

    private void Start()
    {
        ResetPhase();
        lastMode = mode;
        lastFourStroke = fourStrokeEnabled;
    }

    private void Update()
    {
        // Inspector からモードや 4ストローク ON/OFF が変えられた場合にも即座に反映する
        if (mode != lastMode || fourStrokeEnabled != lastFourStroke)
        {
            ResetPhase();
            lastMode = mode;
            lastFourStroke = fourStrokeEnabled;
        }

        bool useFourStroke = mode == SourceMode.Alternate && fourStrokeEnabled && fourStroke != null;
        if (fourStroke != null)
        {
            fourStroke.enabled = useFourStroke; // 使わないときは無駄な合成（Blit）を止める
        }

        if (useFourStroke)
        {
            // 4ストローク合成: 矩形波切替の代わりに，ライブ(C)と収録(D)を
            // 反転＋台形波クロスフェードで合成した映像を提示する
            fourStroke.currentTexture = liveTexture;
            fourStroke.delayedTexture = playbackTexture;
            fourStroke.frequency = switchFrequency; // 周波数は切替周波数を共用
            if (rawImage != null && fourStroke.OutputTexture != null)
            {
                rawImage.texture = fourStroke.OutputTexture;
            }
            CurrentSource = fourStroke.DominantSource; // ロガー用: 支配的なソースを記録
        }
        else if (mode == SourceMode.Alternate)
        {
            // 一時停止中は deltaTime = 0 なので切替タイマーも自動的に止まる
            elapsed += Time.deltaTime;
            float halfPeriod = 0.5f / Mathf.Max(switchFrequency, 0.01f); // 各ソースの表示時間
            while (elapsed >= halfPeriod)
            {
                elapsed -= halfPeriod;
                CurrentSource = 1 - CurrentSource; // 0⇔1 をトグル
                ApplyTexture();
            }
        }

        // デバッグ着色は Inspector から実行中に切り替えられるよう毎フレーム反映する
        if (rawImage != null)
        {
            rawImage.color = (debugTint && CurrentSource == 1) ? PlaybackTint : Color.white;
        }
    }

    /// <summary>
    /// 切替位相をリセットする（実験開始時に呼び，必ずライブ映像から始める）
    /// </summary>
    public void ResetPhase()
    {
        elapsed = 0f;
        CurrentSource = (mode == SourceMode.PlaybackOnly) ? 1 : 0;
        ApplyTexture();
        // 4ストロークの変調位相も揃えてリセットする（ライブ提示から始まる）
        if (fourStroke != null)
        {
            fourStroke.ResetPhase();
        }
    }

    /// <summary>
    /// 現在のソースに対応するテクスチャを RawImage に適用する
    /// </summary>
    private void ApplyTexture()
    {
        if (rawImage == null) return;
        rawImage.texture = (CurrentSource == 0) ? liveTexture : playbackTexture;
    }
}
