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

    /// <summary>収録映像表示中のデバッグ着色</summary>
    private static readonly Color PlaybackTint = new Color(1f, 0.75f, 0.45f, 1f);

    /// <summary>
    /// 今表示しているソース（0 = ライブ, 1 = 収録）．ロガーが記録に使う．
    /// </summary>
    public int CurrentSource { get; private set; }

    private float elapsed;       // 現在のソースの表示継続時間
    private SourceMode lastMode; // モード変更検知用

    private void Start()
    {
        ResetPhase();
        lastMode = mode;
    }

    private void Update()
    {
        // Inspector からモードが変えられた場合にも即座に反映する
        if (mode != lastMode)
        {
            ResetPhase();
            lastMode = mode;
        }

        if (mode == SourceMode.Alternate)
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
